import fs from "node:fs/promises";
import { readFileSync } from "node:fs";
import { builtinModules } from "node:module";
import path from "node:path";
import ts from "typescript";
import { CodeFact, EvidenceTiers, FactTypes, FileInventoryItem, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { hash } from "../util/Hash";
import { extractEntityShapeFacts } from "./Base44EntityShapeExtractor";

const entityOperations = new Set(["list", "filter", "get", "create", "update", "delete", "deleteMany", "bulkCreate", "importEntities", "subscribe", "upsert"]);
const primitiveRoots = new Set([
  "auth", "entities", "functions", "integrations",
  "analytics", "appLogs", "users", "asServiceRole",
  "Analytics", "AppLogs"
]);
const base44FactoryNames = new Set(["createClient", "createClientFromRequest"]);
const axiosMethods = new Set(["request", "get", "post", "put", "patch", "delete", "head", "options"]);

export async function extractBase44Facts(manifest: ScanManifest, inventory: readonly FileInventoryItem[]): Promise<CodeFact[]> {
  const facts: CodeFact[] = [];
  const sourceItems = inventory.filter((file) => !file.skipped && /\.[jt]sx?$/.test(file.relativePath) && !file.relativePath.endsWith(".d.ts"));
  const migrationItems = inventory.filter((file) => !file.skipped && file.relativePath.endsWith(".sql") && isMigrationPath(file.relativePath));
  const aliasDiscovery = await buildAliasMaps(sourceItems);
  const frontendPackageAuthority = await loadFrontendPackageAuthority(inventory);
  for (const item of inventory.filter((file) => !file.skipped)) {
    if (item.relativePath.endsWith(".sql")) {
      continue;
    }
    if (!/\.[jt]sx?$/.test(item.relativePath) || item.relativePath.endsWith(".d.ts")) {
      continue;
    }
    const text = await fs.readFile(item.absolutePath, "utf8");
    const source = aliasDiscovery.contexts.get(item.relativePath)?.source
      ?? ts.createSourceFile(item.absolutePath, text, ts.ScriptTarget.Latest, true, scriptKind(item.relativePath));
    const aliases = aliasDiscovery.aliasesByFile.get(item.relativePath) ?? new Map<string, string[]>();
    const factoryAliases = aliasDiscovery.factoryAliasesByFile.get(item.relativePath) ?? new Set<string>();
    const injectedParameters = aliasDiscovery.injectedParametersByFile.get(item.relativePath) ?? new Map<number, string[]>();
    const sdkIdentity = resolveSdkIdentity(
      aliasDiscovery.sdkAuthorityRootsByFile.get(item.relativePath) ?? [],
      frontendPackageAuthority
    );
    const base44Context = aliases.size > 0
      || injectedParameters.size > 0
      || source.statements.some((statement) => ts.isImportDeclaration(statement)
        && ts.isStringLiteral(statement.moduleSpecifier)
        && isBase44SdkPackageImport(statement.moduleSpecifier.text))
      || /(^|\/)base44\/(functions|entities)\//.test(item.relativePath);
    for (const statement of source.statements) {
      if (!ts.isImportDeclaration(statement) || !ts.isStringLiteral(statement.moduleSpecifier)) continue;
      const requested = statement.moduleSpecifier.text;
      if (!isBase44SdkPackageImport(requested)) continue;
      const clause = statement.importClause;
      facts.push(fact(manifest, FactTypes.Base44SdkImport, RuleIds.Base44SdkImport, statement, source, item.relativePath, requested, {
        requestedPackage: requested,
        requestedVersion: packageVersion(requested),
        importKind: clause?.isTypeOnly ? "type-only" : "runtime",
        sourceFileSha256: hash(text, 64)
      }));
    }
    visit(source, source, item.relativePath, text, aliases, factoryAliases, injectedParameters, base44Context, manifest, facts, sdkIdentity, aliasDiscovery.contexts);
    if (isFunctionEntry(item.relativePath)) {
      const name = functionName(item.relativePath);
      facts.push(fact(manifest, FactTypes.Base44FunctionSurface, RuleIds.Base44FunctionSurface, source, source, item.relativePath, name, {
        functionName: name,
        handlerKind: text.includes("Deno.serve") ? "deno-serve" : "unknown",
        sourceFileSha256: hash(text, 64)
      }, text.includes("Deno.serve") ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier4Unknown));
    }
    if (/base44\/(functions|entities)\//.test(item.relativePath)) {
      facts.push(fact(manifest, FactTypes.Base44CustomerBoundary, RuleIds.Base44CustomerBoundary, source, source, item.relativePath, item.relativePath, {
        logicOwnership: "customer-authored",
        surfaceKind: item.relativePath.includes("/functions/") ? "function" : "entity",
        sourceFileSha256: hash(text, 64)
      }, EvidenceTiers.Tier2Structural));
    }
  }
  ensureSelectorEvidenceAuthorityFacts(manifest, facts, aliasDiscovery.contexts);
  const hasBase44Signal = facts.some((candidate) => candidate.factType === FactTypes.Base44SdkImport
    || candidate.factType === FactTypes.Base44FunctionSurface
    || candidate.factType === FactTypes.Base44CustomerBoundary);
  if (hasBase44Signal) {
    for (const item of migrationItems) facts.push(await sqlFact(manifest, item));
  }
  return facts;
}

function visit(node: ts.Node, source: ts.SourceFile, filePath: string, text: string, aliases: Map<string, string[]>, factoryAliases: Set<string>, injectedParameters: Map<number, string[]>, base44Context: boolean, manifest: ScanManifest, facts: CodeFact[], sdkIdentity: SdkIdentityResolution, contexts: Map<string, SourceContext>): void {
  if (ts.isCallExpression(node)) {
    const chain = expressionChain(node.expression);
    const binding = chain ? resolveRuntimeAlias(chain[0], node, source, aliases, factoryAliases, injectedParameters, new Set()) : null;
    if (chain && binding) addSdkCall([...binding.prefix, ...chain.slice(1)], node, source, filePath, text, manifest, facts,
      binding.kind, sdkIdentity, contexts);
    if (chain && !binding) addComputedEntityAliasSdkCall(node, source, filePath, text, manifest, facts, aliases,
      factoryAliases, injectedParameters, sdkIdentity, contexts);
    if (!chain) addComputedEntitySdkCall(node, source, filePath, text, manifest, facts, aliases, factoryAliases,
      injectedParameters, sdkIdentity, contexts);
    if (base44Context && chain?.join(".") === "Deno.env.get") {
      const name = stringArgument(node.arguments[0]);
      facts.push(fact(manifest, FactTypes.Base44EnvironmentAccess, RuleIds.Base44EnvironmentAccess, node, source, filePath, name ?? "dynamic", {
        environmentName: name ?? "dynamic",
        accessKind: name ? "static" : "dynamic",
        sourceFileSha256: hash(text, 64)
      }, name ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown));
    }
    if (base44Context && (chain?.at(-1) === "fetch" || (chain?.[0] === "axios" && chain.length === 2 && axiosMethods.has(chain[1])))) {
      const literal = stringArgument(node.arguments[0]);
      const origin = literal ? safeOrigin(literal) : null;
      facts.push(fact(manifest, FactTypes.Base44HttpTarget, RuleIds.Base44HttpTarget, node, source, filePath, origin ? `origin-${hash(origin, 20)}` : "dynamic", {
        targetKind: origin ? "static-origin" : "dynamic",
        originSha256: origin ? hash(origin, 64) : "",
        scheme: origin ? new URL(origin).protocol.replace(":", "") : "unknown",
        sourceFileSha256: hash(text, 64)
      }, origin ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown));
    }
  }
  ts.forEachChild(node, (child) => visit(child, source, filePath, text, aliases, factoryAliases, injectedParameters, base44Context, manifest, facts, sdkIdentity, contexts));
}

function addSdkCall(chain: string[], node: ts.CallExpression, source: ts.SourceFile, filePath: string, text: string, manifest: ScanManifest, facts: CodeFact[], clientBindingKind: string, sdkIdentity: SdkIdentityResolution, contexts: Map<string, SourceContext>): void {
  const rootIndex = chain.findIndex((part) => primitiveRoots.has(part));
  if (rootIndex < 0) return;
  const relative = chain.slice(rootIndex);
  const capability = relative.join(".");
  const clientBindingEvidence: Record<string, string> = clientBindingKind === "callsite-proven-parameter" ? { clientBindingKind } : {};
  const entitiesIndex = sdkRootIndex(relative, "entities");
  const isEntityOperation = entitiesIndex >= 0
    && entityOperations.has(relative[entitiesIndex + 2])
    && entitiesIndex + 3 === relative.length;
  if (entitiesIndex >= 0
    && entityOperations.has(relative[entitiesIndex + 2])
    && entitiesIndex + 3 === relative.length) {
    const reachability = dormantEntityCallsiteDisposition(node, source, filePath, contexts);
    if (reachability) {
      const entityName = relative[entitiesIndex + 1];
      const operationName = relative[entitiesIndex + 2];
      const selector = staticEntitySelector(entityName, node, source, filePath);
      const primitiveCapability = relative.join(".");
      facts.push(fact(manifest, FactTypes.Base44SdkPrimitive, RuleIds.Base44SdkPrimitive, node, source, filePath, primitiveCapability, {
        capability: primitiveCapability,
        ...clientBindingEvidence,
        ...exactEntityCallsiteProperties(node, source),
        primitiveRoot: relative[0],
        sourceFileSha256: hash(text, 64)
      }));
      const disposition = buildEntityCallsiteDisposition(reachability, node, source, filePath,
        [entityName], operationName, selector, sdkIdentity, [primitiveCapability]);
      facts.push(fact(manifest, FactTypes.Base44EntityCallsiteDisposition, RuleIds.Base44EntityCallsiteDisposition,
        node, source, filePath, `${filePath}:${disposition.callableName}:${operationName}`, {
          callsiteDispositionJson: JSON.stringify(disposition),
          entitySelectorJson: JSON.stringify(selector),
          operationEvidenceIdsJson: JSON.stringify(disposition.operationEvidenceIds),
          operationName,
          sdkIdentityGap: sdkIdentity.gap ?? "",
          sdkIdentityJson: sdkIdentity.identity ? JSON.stringify(sdkIdentity.identity) : "",
          sourceFileSha256: hash(text, 64)
        }, sdkIdentity.identity ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown));
      return;
    }
  }
  facts.push(fact(manifest, FactTypes.Base44SdkPrimitive, RuleIds.Base44SdkPrimitive, node, source, filePath, capability, {
    capability,
    ...clientBindingEvidence,
    ...(isEntityOperation ? exactEntityCallsiteProperties(node, source) : {}),
    primitiveRoot: relative[0],
    sourceFileSha256: hash(text, 64)
  }));
  const functionsIndex = sdkRootIndex(relative, "functions");
  if (functionsIndex >= 0 && relative[functionsIndex + 1] === "invoke" && functionsIndex + 2 === relative.length) {
    const functionName = stringArgument(node.arguments[0]);
    facts.push(fact(manifest, FactTypes.Base44FunctionInvocation, RuleIds.Base44FunctionInvocation, node, source, filePath, functionName ?? "dynamic", {
      functionName: functionName ?? "dynamic",
      ...clientBindingEvidence,
      bindingKind: functionName ? "static" : "dynamic",
      sourceFileSha256: hash(text, 64)
    }, functionName ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown));
  }
  if (isEntityOperation) {
    const entityName = relative[entitiesIndex + 1];
    const operationName = relative[entitiesIndex + 2];
    const computedFields = sourceBoundComputedFields(node, { filePath, source, contexts });
    addEntityOperation(node, source, filePath, text, manifest, facts, clientBindingKind, sdkIdentity,
      entityName, operationName, staticEntitySelector(entityName, node, source, filePath), contexts, computedFields);
  }
}

interface EntitySelectorEvidence {
  filePath: string;
  sourceFileSha256: string;
  startLine: number;
  endLine: number;
  snippetSha256: string;
  derivation: "literal" | "array-element" | "object-property" | "caller-argument" | "set-membership";
}

interface EntitySelectorContract {
  schemaVersion: "88mph.base44-entity-selector.v1";
  kind: "static-member" | "finite-source-domain" | "unresolved";
  candidates: string[];
  evidence: EntitySelectorEvidence[];
  gap: "" | "entity-selector-dynamic-unresolved";
}

function staticEntitySelector(entityName: string, node: ts.CallExpression, source: ts.SourceFile, filePath: string): EntitySelectorContract {
  const method = unwrapAliasExpression(node.expression);
  const entityAccess = ts.isPropertyAccessExpression(method) ? unwrapAliasExpression(method.expression) : method;
  const entity = ts.isPropertyAccessExpression(entityAccess) ? entityAccess.name : method;
  return {
    schemaVersion: "88mph.base44-entity-selector.v1",
    kind: "static-member",
    candidates: [entityName],
    evidence: [selectorEvidence(entity, source, filePath, "literal")],
    gap: ""
  };
}

function addEntityOperation(
  node: ts.CallExpression,
  source: ts.SourceFile,
  filePath: string,
  text: string,
  manifest: ScanManifest,
  facts: CodeFact[],
  clientBindingKind: string,
  sdkIdentity: SdkIdentityResolution,
  entityName: string,
  operationName: string,
  selector: EntitySelectorContract,
  contexts: Map<string, SourceContext>,
  computedQueryFields: Record<string, string[]> = {}
): void {
  const clientBindingEvidence: Record<string, string> = clientBindingKind === "callsite-proven-parameter" ? { clientBindingKind } : {};
  const operationEvidenceId = entityOperationEvidenceId(node, source, filePath, entityName, operationName);
  const selectorJson = JSON.stringify(selector);
  const selectorGap = selector.gap;
  const operationFact = fact(manifest, FactTypes.Base44EntityOperation, RuleIds.Base44EntityOperation, node, source, filePath, entityName, {
    entityName,
    entitySelectorGap: selectorGap,
    entitySelectorJson: selectorJson,
    ...clientBindingEvidence,
    operationEvidenceId,
    operationName,
    ...exactEntityCallsiteProperties(node, source),
    sdkIdentityGap: sdkIdentity.gap ?? "",
    sdkIdentityJson: sdkIdentity.identity ? JSON.stringify(sdkIdentity.identity) : "",
    sourceFileSha256: hash(text, 64)
  }, sdkIdentity.identity && !selectorGap ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown);
  facts.push(operationFact);
  const runtimeDeferredOuterKind = sourceBoundPayloadOuterKind(node, operationName, { filePath, source, contexts });
  facts.push(...extractEntityShapeFacts({
    manifest, node, source, filePath, sourceText: text, entityName, operationName, operationEvidenceId,
    entitySelectorGap: selectorGap,
    entitySelectorJson: selectorJson,
    computedQueryFields,
    arrayIntrinsicsPristine: arrayIntrinsicsArePristine(contexts),
    runtimeDeferredOuterKind,
    sdkIdentityGap: sdkIdentity.gap ?? "",
    sdkIdentityJson: sdkIdentity.identity ? JSON.stringify(sdkIdentity.identity) : ""
  }));
}

function entityOperationEvidenceId(
  node: ts.CallExpression,
  source: ts.SourceFile,
  filePath: string,
  entityName: string,
  operationName: string
): string {
  const operationStartLine = source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1;
  const operationEndLine = source.getLineAndCharacterOfPosition(node.getEnd()).line + 1;
  return `operation-${hash([
    filePath,
    String(operationStartLine),
    String(operationEndLine),
    String(node.getStart(source)),
    String(node.getEnd()),
    entityName,
    operationName,
    hash(node.getText(source), 64)
  ].join("|"), 20)}`;
}

function exactEntityCallsiteProperties(node: ts.CallExpression, source: ts.SourceFile): Record<string, string> {
  return {
    callsiteStartOffset: String(node.getStart(source)),
    callsiteEndOffset: String(node.getEnd()),
    callsiteSnippetSha256: hash(node.getText(source), 64)
  };
}

function sourceBoundPayloadOuterKind(
  call: ts.CallExpression,
  operationName: string,
  context: SelectorEvaluationContext
): "object" | undefined {
  const argumentIndex = operationName === "update" ? 1
    : operationName === "create" || operationName === "bulkCreate" ? 0 : -1;
  if (argumentIndex < 0) return undefined;
  const argument = call.arguments[argumentIndex];
  if (!argument || ts.isSpreadElement(argument)) return undefined;
  const values = evaluateSelectorValues(argument, context, new Set());
  if (!values?.length) return undefined;
  if (values.every((value) => value.kind === "object")) return "object";
  return undefined;
}

interface StaticSelectorValue {
  kind: "string" | "boolean" | "undefined" | "object" | "array" | "opaque";
  value?: string | boolean;
  properties?: Map<string, StaticSelectorValue[]>;
  openProperties?: boolean;
  elements?: StaticSelectorValue[];
  evidence: EntitySelectorEvidence[];
}

interface SelectorEvaluationContext {
  filePath: string;
  source: ts.SourceFile;
  contexts: Map<string, SourceContext>;
  parameterValues?: Map<ts.ParameterDeclaration, StaticSelectorValue[]>;
  bindingValues?: Map<ts.VariableDeclaration, StaticSelectorValue[]>;
}

function addComputedEntitySdkCall(
  node: ts.CallExpression,
  source: ts.SourceFile,
  filePath: string,
  text: string,
  manifest: ScanManifest,
  facts: CodeFact[],
  aliases: Map<string, string[]>,
  factoryAliases: Set<string>,
  injectedParameters: Map<number, string[]>,
  sdkIdentity: SdkIdentityResolution,
  contexts: Map<string, SourceContext>
): void {
  const methodAccess = unwrapAliasExpression(node.expression);
  if (!ts.isPropertyAccessExpression(methodAccess) || !entityOperations.has(methodAccess.name.text)) return;
  const entityAccess = unwrapAliasExpression(methodAccess.expression);
  if (!ts.isElementAccessExpression(entityAccess) || !entityAccess.argumentExpression
    || ts.isStringLiteralLike(entityAccess.argumentExpression)) return;
  const entitiesChain = expressionChain(unwrapAliasExpression(entityAccess.expression));
  if (!entitiesChain) return;
  const binding = resolveRuntimeAlias(entitiesChain[0], node, source, aliases, factoryAliases, injectedParameters, new Set());
  if (!binding) return;
  const relative = [...binding.prefix, ...entitiesChain.slice(1)];
  const entitiesIndex = sdkRootIndex(relative, "entities");
  if (entitiesIndex < 0 || entitiesIndex + 1 !== relative.length) return;

  emitComputedEntityCall(node, entityAccess.argumentExpression, relative, binding, source, filePath, text,
    manifest, facts, sdkIdentity, contexts);
}

function addComputedEntityAliasSdkCall(
  node: ts.CallExpression,
  source: ts.SourceFile,
  filePath: string,
  text: string,
  manifest: ScanManifest,
  facts: CodeFact[],
  aliases: Map<string, string[]>,
  factoryAliases: Set<string>,
  injectedParameters: Map<number, string[]>,
  sdkIdentity: SdkIdentityResolution,
  contexts: Map<string, SourceContext>
): void {
  const methodAccess = unwrapAliasExpression(node.expression);
  if (!ts.isPropertyAccessExpression(methodAccess) || !entityOperations.has(methodAccess.name.text)) return;
  const owner = unwrapAliasExpression(methodAccess.expression);
  if (!ts.isIdentifier(owner)) return;
  const declaration = resolveLexicalBinding(owner.text, owner, source);
  if (declaration?.kind !== "variable" || !declaration.node.initializer) return;
  const alias = computedEntityAlias(declaration.node.initializer, declaration.node, source, aliases, factoryAliases,
    injectedParameters, contexts);
  if (!alias) return;
  const resolvedSelectors = entityAliasBindingSelectorValues(declaration.node, owner, alias, source, filePath,
    aliases, factoryAliases, injectedParameters, contexts);
  emitComputedEntityCall(node, resolvedSelectors ? undefined : alias.selector, alias.relative, alias.binding,
    source, filePath, text, manifest, facts, sdkIdentity, contexts, resolvedSelectors ?? undefined);
}

function computedEntityAlias(
  input: ts.Expression,
  use: ts.Node,
  source: ts.SourceFile,
  aliases: Map<string, string[]>,
  factoryAliases: Set<string>,
  injectedParameters: Map<number, string[]>,
  contexts: Map<string, SourceContext>
): { selector: ts.Expression; relative: string[]; binding: RuntimeAlias } | null {
  const expression = unwrapSelectorExpression(input);
  if (ts.isElementAccessExpression(expression) && expression.argumentExpression) {
    const entitiesChain = expressionChain(unwrapAliasExpression(expression.expression));
    if (!entitiesChain) return null;
    const binding = resolveRuntimeAlias(entitiesChain[0], use, source, aliases, factoryAliases, injectedParameters, new Set());
    if (!binding) return null;
    const relative = [...binding.prefix, ...entitiesChain.slice(1)];
    const entitiesIndex = sdkRootIndex(relative, "entities");
    return entitiesIndex >= 0 && entitiesIndex + 1 === relative.length
      ? { selector: expression.argumentExpression, relative, binding }
      : null;
  }
  if (!ts.isCallExpression(expression)) return null;
  const current = contexts.get(filePathForSource(source, contexts));
  if (!current) return null;
  const target = resolveCallableTarget(expression.expression, current, contexts);
  if (!target) return null;
  const returned = returnedExpression(target);
  if (!returned) return null;
  const entityAccess = unwrapSelectorExpression(returned);
  if (!ts.isElementAccessExpression(entityAccess) || !entityAccess.argumentExpression) return null;
  const chain = expressionChain(unwrapAliasExpression(entityAccess.expression));
  if (!chain) return null;
  const targetContext = contexts.get(filePathForSource(target.getSourceFile(), contexts));
  if (!targetContext) return null;
  const binding = resolveRuntimeAlias(chain[0], entityAccess, targetContext.source, targetContext.aliases,
    targetContext.factoryAliases, new Map(), new Set());
  if (!binding) return null;
  const selector = unwrapAliasExpression(entityAccess.argumentExpression);
  if (!ts.isIdentifier(selector)) return null;
  const parameterIndex = target.parameters.findIndex((parameter) => ts.isIdentifier(parameter.name) && parameter.name.text === selector.text);
  const argument = parameterIndex >= 0 ? expression.arguments[parameterIndex] : undefined;
  if (!argument || ts.isSpreadElement(argument)) return null;
  const relative = [...binding.prefix, ...chain.slice(1)];
  const entitiesIndex = sdkRootIndex(relative, "entities");
  return entitiesIndex >= 0 && entitiesIndex + 1 === relative.length
    ? { selector: argument, relative, binding }
    : null;
}

function entityAliasBindingSelectorValues(
  declaration: ts.VariableDeclaration,
  use: ts.Identifier,
  initialAlias: { selector: ts.Expression; relative: string[]; binding: RuntimeAlias },
  source: ts.SourceFile,
  filePath: string,
  aliases: Map<string, string[]>,
  factoryAliases: Set<string>,
  injectedParameters: Map<number, string[]>,
  contexts: Map<string, SourceContext>
): StaticSelectorValue[] | null {
  if (!ts.isIdentifier(declaration.name)) return null;
  if (isImmutableVariable(declaration, use) && selectorBindingIsUnmutated(declaration, source, use)) {
    return evaluateSelectorValues(initialAlias.selector, { filePath, source, contexts }, new Set());
  }
  const values: StaticSelectorValue[] = [];
  const initial = evaluateSelectorValues(initialAlias.selector, { filePath, source, contexts }, new Set());
  if (!initial) return null;
  values.push(...initial);
  const bindingName = declaration.name.text;
  let unsafe = false;
  const scope = executionRoot(declaration.name);
  const usePosition = use.getStart(source);
  const visit = (node: ts.Node): void => {
    if (unsafe || node.getStart(source) > usePosition) return;
    if (ts.isIdentifier(node) && node !== declaration.name && node !== use && node.text === bindingName
      && resolveLexicalBinding(bindingName, node, source)?.node === declaration) {
      const assignment = node.parent;
      if (ts.isBinaryExpression(assignment) && assignment.left === node
        && assignment.operatorToken.kind === ts.SyntaxKind.EqualsToken) {
        const resolved = entityAliasAssignmentSelectorValues(assignment.right, source, filePath, aliases,
          factoryAliases, injectedParameters, contexts, initialAlias.relative);
        if (!resolved) { unsafe = true; return; }
        values.push(...resolved);
        return;
      }
      if ((ts.isPropertyAccessExpression(assignment) || ts.isElementAccessExpression(assignment))
        && assignment.expression === node) return;
      unsafe = true;
      return;
    }
    ts.forEachChild(node, visit);
  };
  visit(scope);
  return unsafe ? null : normalizeStaticValues(values);
}

function entityAliasAssignmentSelectorValues(
  input: ts.Expression,
  source: ts.SourceFile,
  filePath: string,
  aliases: Map<string, string[]>,
  factoryAliases: Set<string>,
  injectedParameters: Map<number, string[]>,
  contexts: Map<string, SourceContext>,
  expectedRelative: string[]
): StaticSelectorValue[] | null {
  const expression = unwrapSelectorExpression(input);
  const direct = computedEntityAlias(expression, expression, source, aliases, factoryAliases, injectedParameters, contexts);
  if (direct && JSON.stringify(direct.relative) === JSON.stringify(expectedRelative)) {
    return evaluateSelectorValues(direct.selector, { filePath, source, contexts }, new Set());
  }
  if (!ts.isCallExpression(expression)) return null;
  const caller = contextForSource(source, contexts);
  const wrapper = resolveCallableTarget(expression.expression, caller, contexts);
  if (!wrapper || !wrapper.body) return null;
  const callbackIndex = wrapper.parameters.findIndex((parameter) => ts.isIdentifier(parameter.name)
    && transparentCallbackReturnBinding(wrapper, parameter.name.text));
  const callback = callbackIndex >= 0 ? expression.arguments[callbackIndex] : undefined;
  if (!callback || ts.isSpreadElement(callback)
    || (!ts.isArrowFunction(callback) && !ts.isFunctionExpression(callback))) return null;
  const returns = callableReturnExpressions(callback);
  if (returns.length === 0) return null;
  const values: StaticSelectorValue[] = [];
  for (const returned of returns) {
    const aliasesInReturn = nullableEntityAliases(returned, callback.getSourceFile(), filePath, aliases,
      factoryAliases, injectedParameters, contexts, expectedRelative);
    if (!aliasesInReturn) return null;
    values.push(...aliasesInReturn);
  }
  return values.length ? normalizeStaticValues(values) : null;
}

function transparentCallbackReturnBinding(owner: ts.FunctionLikeDeclaration, parameterName: string): boolean {
  if (!owner.body || !ts.isBlock(owner.body)) return false;
  const returnedNames = new Set<string>();
  let unsafe = false;
  const visit = (node: ts.Node): void => {
    if (unsafe || (node !== owner.body && ts.isFunctionLike(node))) return;
    if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name) && node.initializer
      && ts.isCallExpression(unwrapSelectorExpression(node.initializer))) {
      const call = unwrapSelectorExpression(node.initializer) as ts.CallExpression;
      const callee = unwrapAliasExpression(call.expression);
      if (ts.isIdentifier(callee) && callee.text === parameterName && call.arguments.length === 0) returnedNames.add(node.name.text);
    }
    if (ts.isReturnStatement(node) && node.expression) {
      const returned = unwrapAliasExpression(node.expression);
      if (returned.kind === ts.SyntaxKind.NullKeyword) return;
      if (!ts.isIdentifier(returned) || !returnedNames.has(returned.text)) unsafe = true;
    }
    ts.forEachChild(node, visit);
  };
  visit(owner.body);
  return !unsafe && returnedNames.size === 1;
}

function nullableEntityAliases(
  input: ts.Expression,
  source: ts.SourceFile,
  filePath: string,
  aliases: Map<string, string[]>,
  factoryAliases: Set<string>,
  injectedParameters: Map<number, string[]>,
  contexts: Map<string, SourceContext>,
  expectedRelative: string[]
): StaticSelectorValue[] | null {
  const expression = unwrapSelectorExpression(input);
  if (expression.kind === ts.SyntaxKind.NullKeyword || expression.kind === ts.SyntaxKind.UndefinedKeyword) return [];
  if (ts.isConditionalExpression(expression)) {
    const left = nullableEntityAliases(expression.whenTrue, source, filePath, aliases, factoryAliases,
      injectedParameters, contexts, expectedRelative);
    const right = nullableEntityAliases(expression.whenFalse, source, filePath, aliases, factoryAliases,
      injectedParameters, contexts, expectedRelative);
    return left && right ? normalizeStaticValues([...left, ...right]) : null;
  }
  const direct = computedEntityAlias(expression, expression, source, aliases, factoryAliases, injectedParameters, contexts);
  if (!direct || JSON.stringify(direct.relative) !== JSON.stringify(expectedRelative)) return null;
  return evaluateSelectorValues(direct.selector, { filePath, source, contexts }, new Set());
}

function emitComputedEntityCall(
  node: ts.CallExpression,
  selectorExpression: ts.Expression | undefined,
  relative: string[],
  binding: RuntimeAlias,
  source: ts.SourceFile,
  filePath: string,
  text: string,
  manifest: ScanManifest,
  facts: CodeFact[],
  sdkIdentity: SdkIdentityResolution,
  contexts: Map<string, SourceContext>,
  resolvedOverride?: StaticSelectorValue[]
): void {
  const methodAccess = unwrapAliasExpression(node.expression);
  if (!ts.isPropertyAccessExpression(methodAccess)) return;

  const operationName = methodAccess.name.text;
  const resolved = resolvedOverride ?? (selectorExpression
    ? evaluateSelectorValues(selectorExpression, { filePath, source, contexts }, new Set()) : null);
  const candidates = resolved ? selectorStrings(resolved) : [];
  const evidence = resolved ? normalizeSelectorEvidence(resolved.flatMap((value) => value.evidence)) : [];
  const selector: EntitySelectorContract = candidates.length > 0 ? {
    schemaVersion: "88mph.base44-entity-selector.v1",
    kind: "finite-source-domain",
    candidates,
    evidence,
    gap: ""
  } : {
    schemaVersion: "88mph.base44-entity-selector.v1",
    kind: "unresolved",
    candidates: [],
    evidence,
    gap: "entity-selector-dynamic-unresolved"
  };
  const primitivePrefix = relative[0] === "asServiceRole" ? "asServiceRole.entities" : "entities";
  const reachability = dormantEntityCallsiteDisposition(node, source, filePath, contexts);
  if (reachability) {
    const entityNames = candidates.length > 0 ? candidates : ["dynamic"];
    const primitiveCapabilities = entityNames.map((entityName) => `${primitivePrefix}.${entityName}.${operationName}`);
    for (const capability of primitiveCapabilities) {
      facts.push(fact(manifest, FactTypes.Base44SdkPrimitive, RuleIds.Base44SdkPrimitive, node, source, filePath, capability, {
        capability,
        ...(binding.kind === "callsite-proven-parameter" ? { clientBindingKind: binding.kind } : {}),
        ...exactEntityCallsiteProperties(node, source),
        primitiveRoot: relative[0],
        sourceFileSha256: hash(text, 64)
      }, candidates.length > 0 ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown));
    }
    const disposition = buildEntityCallsiteDisposition(reachability, node, source, filePath,
      entityNames, operationName, selector, sdkIdentity, primitiveCapabilities);
    facts.push(fact(manifest, FactTypes.Base44EntityCallsiteDisposition, RuleIds.Base44EntityCallsiteDisposition,
      node, source, filePath, `${filePath}:${disposition.callableName}:${operationName}`, {
        callsiteDispositionJson: JSON.stringify(disposition),
        entitySelectorJson: JSON.stringify(selector),
        operationEvidenceIdsJson: JSON.stringify(disposition.operationEvidenceIds),
        operationName,
        sdkIdentityGap: sdkIdentity.gap ?? "",
        sdkIdentityJson: sdkIdentity.identity ? JSON.stringify(sdkIdentity.identity) : "",
        sourceFileSha256: hash(text, 64)
      }, sdkIdentity.identity ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown));
    return;
  }
  if (candidates.length === 0) {
    const capability = `${primitivePrefix}.dynamic.${operationName}`;
    facts.push(fact(manifest, FactTypes.Base44SdkPrimitive, RuleIds.Base44SdkPrimitive, node, source, filePath, capability, {
      capability,
      ...(binding.kind === "callsite-proven-parameter" ? { clientBindingKind: binding.kind } : {}),
      ...exactEntityCallsiteProperties(node, source),
      primitiveRoot: relative[0],
      sourceFileSha256: hash(text, 64)
    }, EvidenceTiers.Tier4Unknown));
    addEntityOperation(node, source, filePath, text, manifest, facts, binding.kind, sdkIdentity,
      "dynamic", operationName, selector, contexts);
    return;
  }
  const correlatedFields = selectorExpression
    ? correlatedComputedQueryFields(node, selectorExpression, candidates, { filePath, source, contexts }) : {};
  const sourceBoundFields = sourceBoundComputedFields(node, { filePath, source, contexts });
  for (const entityName of candidates) {
    const capability = `${primitivePrefix}.${entityName}.${operationName}`;
    facts.push(fact(manifest, FactTypes.Base44SdkPrimitive, RuleIds.Base44SdkPrimitive, node, source, filePath, capability, {
      capability,
      ...(binding.kind === "callsite-proven-parameter" ? { clientBindingKind: binding.kind } : {}),
      ...exactEntityCallsiteProperties(node, source),
      primitiveRoot: relative[0],
      sourceFileSha256: hash(text, 64)
    }));
    const computedQueryFields = { ...sourceBoundFields, ...(correlatedFields[entityName] ?? {}) };
    addEntityOperation(node, source, filePath, text, manifest, facts, binding.kind, sdkIdentity,
      entityName, operationName, selector, contexts, computedQueryFields);
  }
}

function sourceBoundComputedFields(
  _call: ts.CallExpression,
  context: SelectorEvaluationContext
): Record<string, string[]> {
  const byFile = sourceBoundComputedFieldsCache.get(context.contexts) ?? new Map<string, Record<string, string[]>>();
  sourceBoundComputedFieldsCache.set(context.contexts, byFile);
  const cached = byFile.get(context.filePath);
  if (cached) return cached;
  const result: Record<string, string[]> = {};
  const visit = (node: ts.Node): void => {
    if (ts.isComputedPropertyName(node)) {
      const values = evaluateSelectorValues(node.expression, context, new Set());
      const strings = values ? flattenStaticValues(values)
        .filter((value) => value.kind === "string" && typeof value.value === "string"
          && /^[A-Za-z_][A-Za-z0-9_.]*$/u.test(value.value))
        .map((value) => value.value as string) : [];
      if (values && strings.length === flattenStaticValues(values).length && strings.length > 0) {
        result[String(node.getStart(context.source))] = uniqueStrings(strings);
      }
      return;
    }
    ts.forEachChild(node, visit);
  };
  visit(context.source);
  byFile.set(context.filePath, result);
  return result;
}

const sourceBoundComputedFieldsCache = new WeakMap<Map<string, SourceContext>, Map<string, Record<string, string[]>>>();

function correlatedComputedQueryFields(
  call: ts.CallExpression,
  selectorExpression: ts.Expression,
  entityNames: string[],
  context: SelectorEvaluationContext
): Record<string, Record<string, string[]>> {
  const selectorMember = selectorObjectMember(selectorExpression, context.source);
  const filter = call.arguments[0] && unwrapAliasExpression(call.arguments[0]);
  if (!selectorMember || !filter || !ts.isObjectLiteralExpression(filter)) return {};
  const ownerValues = evaluateSelectorValues(selectorMember.owner, context, new Set());
  if (!ownerValues) return {};
  const result: Record<string, Record<string, string[]>> = {};
  for (const property of filter.properties) {
    if (!ts.isPropertyAssignment(property) || !ts.isComputedPropertyName(property.name)) continue;
    const fieldMember = selectorObjectMember(property.name.expression, context.source);
    if (!fieldMember || fieldMember.declaration !== selectorMember.declaration) continue;
    for (const entityName of entityNames) {
      const names: string[] = [];
      let complete = true;
      for (const value of ownerValues) {
        if (value.kind !== "object") { complete = false; break; }
        const entities = selectStaticProperty([value], selectorMember.property);
        const candidates = entities ? selectorStrings(entities) : [];
        if (!candidates.includes(entityName)) continue;
        const fields = selectStaticProperty([value], fieldMember.property);
        const strings = fields ? flattenStaticValues(fields).filter((item) => item.kind === "string"
          && typeof item.value === "string").map((item) => item.value as string) : [];
        if (strings.length === 0) { complete = false; break; }
        names.push(...strings);
      }
      if (complete && names.length > 0) {
        const byPosition = result[entityName] ?? {};
        byPosition[String(property.name.getStart(context.source))] = uniqueStrings(names);
        result[entityName] = byPosition;
      }
    }
  }
  return result;
}

function selectorObjectMember(
  input: ts.Expression,
  source: ts.SourceFile
): { owner: ts.Identifier; property: string; declaration: ts.Node } | null {
  const expression = unwrapAliasExpression(input);
  if (!ts.isPropertyAccessExpression(expression)) return null;
  const owner = unwrapAliasExpression(expression.expression);
  if (!ts.isIdentifier(owner)) return null;
  const declaration = resolveLexicalBinding(owner.text, owner, source)?.node;
  return declaration ? { owner, property: expression.name.text, declaration } : null;
}

interface DormantEntityReachability {
  callableName: string;
  authorityPath: string;
  authoritySha256: string;
  sourceSnapshotDigest: string;
  externalModuleReferences: 0;
  ambiguousDynamicModuleReferences: 0;
}

interface DormantEntityCallsiteDisposition extends DormantEntityReachability {
  schemaVersion: "88mph.base44-entity-callsite-disposition.v2";
  disposition: "dormant-unreachable";
  operationName: string;
  operationEvidenceIds: string[];
  primitiveCapabilities: string[];
  entitySelector: EntitySelectorContract;
  sdkIdentity: SdkIdentity | null;
  sdkIdentityGap: string;
  callsite: {
    filePath: string;
    sourceFileSha256: string;
    startLine: number;
    endLine: number;
    startOffset: number;
    endOffset: number;
    snippetSha256: string;
  };
}

function dormantEntityCallsiteDisposition(
  node: ts.CallExpression,
  source: ts.SourceFile,
  filePath: string,
  contexts: Map<string, SourceContext>
): DormantEntityReachability | null {
  const dormantMutation = dormantMutationHookCallable(node, source);
  if (dormantMutation) return {
    callableName: dormantMutation,
    authorityPath: filePath,
    authoritySha256: hash(source.getFullText(), 64),
    sourceSnapshotDigest: sourceGraphDigest(contexts),
    externalModuleReferences: 0,
    ambiguousDynamicModuleReferences: 0
  };
  const callable = findAncestor(node, (candidate): candidate is ts.FunctionDeclaration => ts.isFunctionDeclaration(candidate));
  if (!callable?.name || !hasModifier(callable, ts.SyntaxKind.ExportKeyword)) return null;
  if (!sourceModuleHasClosedExports(source) || sourceModuleMayBeReferenced(filePath, contexts)) return null;
  return {
    callableName: callable.name.text,
    authorityPath: filePath,
    authoritySha256: hash(source.getFullText(), 64),
    sourceSnapshotDigest: sourceGraphDigest(contexts),
    externalModuleReferences: 0,
    ambiguousDynamicModuleReferences: 0
  };
}

function buildEntityCallsiteDisposition(
  reachability: DormantEntityReachability,
  node: ts.CallExpression,
  source: ts.SourceFile,
  filePath: string,
  entityNames: string[],
  operationName: string,
  entitySelector: EntitySelectorContract,
  sdkIdentity: SdkIdentityResolution,
  primitiveCapabilities: string[]
): DormantEntityCallsiteDisposition {
  return {
    schemaVersion: "88mph.base44-entity-callsite-disposition.v2",
    disposition: "dormant-unreachable",
    ...reachability,
    operationName,
    operationEvidenceIds: entityNames.map((entityName) =>
      entityOperationEvidenceId(node, source, filePath, entityName, operationName)).sort(),
    primitiveCapabilities: [...primitiveCapabilities].sort(),
    entitySelector,
    sdkIdentity: sdkIdentity.identity ?? null,
    sdkIdentityGap: sdkIdentity.gap ?? "",
    callsite: {
      filePath,
      sourceFileSha256: hash(source.getFullText(), 64),
      startLine: source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1,
      endLine: source.getLineAndCharacterOfPosition(node.getEnd()).line + 1,
      startOffset: node.getStart(source),
      endOffset: node.getEnd(),
      snippetSha256: hash(node.getText(source), 64),
    },
  };
}

function dormantMutationHookCallable(node: ts.CallExpression, source: ts.SourceFile): string | null {
  const owner = findAncestor(node, (candidate): candidate is ts.ArrowFunction | ts.FunctionExpression =>
    ts.isArrowFunction(candidate) || ts.isFunctionExpression(candidate));
  if (!owner) return null;
  const property = owner.parent;
  if (!ts.isPropertyAssignment(property) || property.initializer !== owner
    || selectorPropertyName(property.name) !== "mutationFn" || !ts.isObjectLiteralExpression(property.parent)) return null;
  const hookCall = property.parent.parent;
  if (!ts.isCallExpression(hookCall) || !hookCall.arguments.includes(property.parent)
    || !isReactQueryUseMutationCall(hookCall, source)) return null;
  const declaration = hookCall.parent;
  if (!ts.isVariableDeclaration(declaration) || !ts.isIdentifier(declaration.name)) return null;
  const bindingName = declaration.name.text;
  if (mutationHandleIsExported(declaration, bindingName, source)) return null;
  let unsafe = false;
  const scope = executionRoot(declaration.name);
  const visit = (candidate: ts.Node): void => {
    if (unsafe) return;
    if (ts.isIdentifier(candidate) && candidate !== declaration.name && candidate.text === bindingName) {
      if (resolveLexicalBinding(bindingName, candidate, source)?.node !== declaration) {
        unsafe = true;
        return;
      }
      const member = candidate.parent;
      if (!ts.isPropertyAccessExpression(member) || member.expression !== candidate
        || member.name.text === "mutate" || member.name.text === "mutateAsync") {
        unsafe = true;
      }
      return;
    }
    ts.forEachChild(candidate, visit);
  };
  visit(scope);
  return unsafe ? null : `${bindingName}.mutationFn`;
}

function mutationHandleIsExported(
  declaration: ts.VariableDeclaration,
  bindingName: string,
  source: ts.SourceFile
): boolean {
  const statement = declaration.parent.parent;
  if (ts.isVariableStatement(statement)
    && statement.modifiers?.some((modifier) => modifier.kind === ts.SyntaxKind.ExportKeyword)) return true;
  for (const candidate of source.statements) {
    if (ts.isExportAssignment(candidate)) {
      const expression = unwrapAliasExpression(candidate.expression);
      if (ts.isIdentifier(expression) && expression.text === bindingName
        && resolveLexicalBinding(bindingName, expression, source)?.node === declaration) return true;
    }
    if (ts.isExportDeclaration(candidate) && !candidate.moduleSpecifier
      && candidate.exportClause && ts.isNamedExports(candidate.exportClause)
      && candidate.exportClause.elements.some((element) => (element.propertyName?.text ?? element.name.text) === bindingName)) return true;
    if (ts.isExpressionStatement(candidate) && ts.isBinaryExpression(candidate.expression)
      && candidate.expression.operatorToken.kind === ts.SyntaxKind.EqualsToken) {
      const right = unwrapAliasExpression(candidate.expression.right);
      if (!ts.isIdentifier(right) || right.text !== bindingName
        || resolveLexicalBinding(bindingName, right, source)?.node !== declaration) continue;
      const left = candidate.expression.left.getText(source);
      if (left === "module.exports" || left.startsWith("module.exports.") || left.startsWith("exports.")) return true;
    }
  }
  return false;
}

function isReactQueryUseMutationCall(call: ts.CallExpression, source: ts.SourceFile): boolean {
  const callee = unwrapAliasExpression(call.expression);
  if (ts.isIdentifier(callee)) {
    const binding = resolveLexicalBinding(callee.text, callee, source);
    if (binding?.kind !== "import" || binding.exportedName !== "useMutation") return false;
    const declaration = findAncestor(binding.node, ts.isImportDeclaration);
    return Boolean(declaration && ts.isStringLiteralLike(declaration.moduleSpecifier)
      && declaration.moduleSpecifier.text === "@tanstack/react-query");
  }
  if (!ts.isPropertyAccessExpression(callee) || callee.name.text !== "useMutation"
    || !ts.isIdentifier(callee.expression)) return false;
  const binding = resolveLexicalBinding(callee.expression.text, callee.expression, source);
  const declaration = binding?.kind === "import" ? findAncestor(binding.node, ts.isImportDeclaration) : null;
  return Boolean(binding?.kind === "import" && binding.exportedName === "*" && declaration
    && ts.isStringLiteralLike(declaration.moduleSpecifier)
    && declaration.moduleSpecifier.text === "@tanstack/react-query");
}

function sourceGraphDigest(contexts: Map<string, SourceContext>): string {
  const cached = sourceGraphDigestCache.get(contexts);
  if (cached) return cached;
  const digest = hash(JSON.stringify([...contexts.values()]
    .map((context) => [context.item.relativePath, hash(context.source.getFullText(), 64)])
    .sort(([left], [right]) => left.localeCompare(right))), 64);
  sourceGraphDigestCache.set(contexts, digest);
  return digest;
}

const sourceGraphDigestCache = new WeakMap<Map<string, SourceContext>, string>();

function sourceModuleHasClosedExports(source: ts.SourceFile): boolean {
  for (const statement of source.statements) {
    if (ts.isExportDeclaration(statement) || ts.isExportAssignment(statement)) return false;
    if (ts.isVariableStatement(statement) && hasModifier(statement, ts.SyntaxKind.ExportKeyword)) {
      if ((statement.declarationList.flags & ts.NodeFlags.Const) === 0) return false;
      if (statement.declarationList.declarations.some((declaration) => !ts.isIdentifier(declaration.name))) return false;
    }
  }
  return true;
}

function sourceModuleMayBeReferenced(filePath: string, contexts: Map<string, SourceContext>): boolean {
  const graph = sourceReachabilityGraph(contexts);
  const frontendRoots = [...graph.roots].filter((candidate) => /(^|\/)src\/(?:pages\.config|main|index|App)\.[jt]sx?$/u.test(candidate));
  if ((filePath.startsWith("src/") || filePath.includes("/src/")) && frontendRoots.length === 0) return true;
  return !graph.closed || graph.roots.size === 0 || graph.reachable.has(filePath);
}

const sourceReachabilityCache = new WeakMap<Map<string, SourceContext>, {
  closed: boolean;
  roots: Set<string>;
  reachable: Set<string>;
}>();

function sourceReachabilityGraph(contexts: Map<string, SourceContext>): {
  closed: boolean;
  roots: Set<string>;
  reachable: Set<string>;
} {
  const cached = sourceReachabilityCache.get(contexts);
  if (cached) return cached;
  let closed = localModuleAliasCache.get(contexts)?.closed ?? false;
  const edges = new Map<string, Set<string>>();
  const roots = new Set([...contexts.keys()].filter((filePath) =>
    /(^|\/)(?:pages\.config|main|index|App)\.[jt]sx?$/u.test(filePath)
    || /(^|\/)functions\/[^/]+\.[jt]sx?$/u.test(filePath)
    || /(^|\/)base44\/functions\/[^/]+\/(?:entry|index)\.[jt]sx?$/u.test(filePath)));
  for (const htmlRoot of htmlEntrypointRoots(contexts)) roots.add(htmlRoot);
  for (const context of contexts.values()) {
    const targets = edges.get(context.item.relativePath) ?? new Set<string>();
    const visit = (node: ts.Node): void => {
      if (ts.isImportDeclaration(node) || ts.isExportDeclaration(node)) {
        const specifier = node.moduleSpecifier;
        if (specifier && ts.isStringLiteralLike(specifier)) {
          const target = resolveLocalModule(context.item.relativePath, specifier.text, contexts);
          if (target) targets.add(target);
          else if (localSpecifierRequiresExecutableContext(specifier.text, contexts)) closed = false;
        }
      }
      if (ts.isCallExpression(node)) {
        const isImport = node.expression.kind === ts.SyntaxKind.ImportKeyword;
        const isRequire = ts.isIdentifier(node.expression) && node.expression.text === "require";
        const isImportGlob = ts.isPropertyAccessExpression(node.expression)
          && ts.isMetaProperty(node.expression.expression) && node.expression.name.text === "glob";
        if (isImport || isRequire || isImportGlob) {
          const argument = node.arguments[0];
          if (!argument || !ts.isStringLiteralLike(argument)) {
            closed = false;
          } else if (isImportGlob) {
            closed = false;
          } else {
            const target = resolveLocalModule(context.item.relativePath, argument.text, contexts);
            if (target) targets.add(target);
            else if (localSpecifierRequiresExecutableContext(argument.text, contexts)) closed = false;
          }
        }
      }
      ts.forEachChild(node, visit);
    };
    visit(context.source);
    edges.set(context.item.relativePath, targets);
  }
  const reachable = new Set<string>();
  const queue = [...roots];
  while (queue.length) {
    const current = queue.shift()!;
    if (reachable.has(current)) continue;
    reachable.add(current);
    queue.push(...(edges.get(current) ?? []));
  }
  const result = { closed, roots, reachable };
  sourceReachabilityCache.set(contexts, result);
  return result;
}

function htmlEntrypointRoots(contexts: Map<string, SourceContext>): string[] {
  const first = contexts.values().next().value as SourceContext | undefined;
  if (!first) return [];
  let repositoryRoot = path.dirname(first.item.absolutePath);
  for (let index = 1; index < first.item.relativePath.split("/").length; index++) repositoryRoot = path.dirname(repositoryRoot);
  try {
    const html = readFileSync(path.join(repositoryRoot, "index.html"), "utf8");
    return [...html.matchAll(/\bsrc\s*=\s*["']([^"']+\.[cm]?[jt]sx?)["']/giu)]
      .map((match) => match[1].replace(/^[./]+/u, ""))
      .filter((candidate) => contexts.has(candidate))
      .sort();
  } catch {
    return [];
  }
}

function localSpecifierRequiresExecutableContext(
  specifier: string,
  contexts: Map<string, SourceContext>
): boolean {
  const local = specifier.startsWith(".") || specifier.startsWith("@/")
    || localModuleAliases(contexts).some((alias) => moduleAliasMatch(alias.pattern, specifier) !== null);
  if (!local && isDeclaredExternalModule(specifier, contexts)) return false;
  // Static style imports cannot contain executable Base44 callsites and are
  // deliberately absent from the TypeScript/JavaScript SourceContext graph.
  // Missing/broken asset bytes remain a build/package concern. Every local
  // executable or extensionless module edge still has to resolve here.
  return !/\.(?:css|less|sass|scss)(?:[?#].*)?$/iu.test(specifier);
}

const builtinModuleNames = new Set(builtinModules.flatMap((name) => [name, `node:${name}`]));

function isDeclaredExternalModule(specifier: string, contexts: Map<string, SourceContext>): boolean {
  if (/^(?:https?:|npm:|jsr:|data:|virtual:)/u.test(specifier) || builtinModuleNames.has(specifier)) return true;
  const packageName = specifier.startsWith("@")
    ? specifier.split("/").slice(0, 2).join("/")
    : specifier.split("/", 1)[0];
  return Boolean(packageName && localModuleAliasCache.get(contexts)?.declaredPackages.has(packageName));
}

function returnedExpression(owner: ts.FunctionLikeDeclaration): ts.Expression | null {
  if (!owner.body) return null;
  if (!ts.isBlock(owner.body)) return owner.body;
  if (owner.body.statements.length !== 1 || !ts.isReturnStatement(owner.body.statements[0])) return null;
  return owner.body.statements[0].expression ?? null;
}

function unwrapSelectorExpression(input: ts.Expression): ts.Expression {
  let expression = unwrapAliasExpression(input);
  while (ts.isAwaitExpression(expression)) expression = unwrapAliasExpression(expression.expression);
  return expression;
}

function filePathForSource(source: ts.SourceFile, contexts: Map<string, SourceContext>): string {
  return [...contexts].find(([, context]) => context.source === source)?.[0] ?? "";
}

function evaluateSelectorValues(expression: ts.Expression, context: SelectorEvaluationContext, visited: Set<string>): StaticSelectorValue[] | null {
  const value = unwrapSelectorExpression(expression);
  if (ts.isStringLiteralLike(value)) {
    return [{
      kind: "string",
      value: value.text,
      evidence: [selectorEvidence(value, context.source, context.filePath, "literal")]
    }];
  }
  if (value.kind === ts.SyntaxKind.TrueKeyword || value.kind === ts.SyntaxKind.FalseKeyword) {
    return [{ kind: "boolean", value: value.kind === ts.SyntaxKind.TrueKeyword, evidence: [] }];
  }
  if (ts.isIdentifier(value) && value.text === "undefined" && !resolveLexicalBinding(value.text, value, context.source)) {
    return [{ kind: "undefined", evidence: [] }];
  }
  if (ts.isNumericLiteral(value) || value.kind === ts.SyntaxKind.NullKeyword) {
    return [{ kind: "opaque", evidence: [] }];
  }
  if (ts.isConditionalExpression(value)) {
    return mergeStaticValues(
      evaluateSelectorValues(value.whenTrue, context, new Set(visited)),
      evaluateSelectorValues(value.whenFalse, context, new Set(visited))
    );
  }
  if (ts.isBinaryExpression(value)
    && [ts.SyntaxKind.BarBarToken, ts.SyntaxKind.QuestionQuestionToken].includes(value.operatorToken.kind)) {
    return mergeStaticValues(
      evaluateSelectorValues(value.left, context, new Set(visited)),
      evaluateSelectorValues(value.right, context, new Set(visited))
    );
  }
  if (ts.isArrayLiteralExpression(value)) {
    const elements: StaticSelectorValue[] = [];
    for (const element of value.elements) {
      if (ts.isOmittedExpression(element)) continue;
      if (ts.isSpreadElement(element)) {
        const spread = evaluateSelectorValues(element.expression, context, new Set(visited));
        if (!spread || spread.some((item) => item.kind !== "array")) return null;
        elements.push(...spread.flatMap((item) => item.elements ?? []));
      } else {
        const resolved = evaluateSelectorValues(element, context, new Set(visited));
        if (!resolved) return null;
        elements.push(...resolved);
      }
    }
    return [{ kind: "array", elements, evidence: normalizeSelectorEvidence(elements.flatMap((item) => item.evidence)) }];
  }
  if (ts.isObjectLiteralExpression(value)) {
    const correlatedBinding = correlatedObjectForOfBinding(value, context.source);
    if (correlatedBinding && !context.bindingValues?.has(correlatedBinding)) {
      const statement = correlatedBinding.parent.parent;
      if (!ts.isForOfStatement(statement)) return null;
      const iterable = evaluateSelectorValues(statement.expression, context, new Set(visited));
      if (!iterable || iterable.some((item) => item.kind !== "array")) return null;
      const elements = iterable.flatMap((item) => item.elements ?? []);
      const objects: StaticSelectorValue[] = [];
      for (const element of elements) {
        const bindingValues = new Map(context.bindingValues ?? []);
        bindingValues.set(correlatedBinding, [element]);
        const resolved = evaluateSelectorValues(value, { ...context, bindingValues }, new Set(visited));
        if (!resolved) return null;
        objects.push(...resolved);
      }
      return normalizeStaticValues(objects);
    }
    const properties = new Map<string, StaticSelectorValue[]>();
    let openProperties = false;
    const evidence: EntitySelectorEvidence[] = [];
    for (const property of value.properties) {
      if (ts.isSpreadAssignment(property)) {
        const spread = evaluateSelectorValues(property.expression, context, new Set(visited));
        if (!spread || spread.some((item) => item.kind !== "object")) {
          // The object-literal construction itself still proves the outer
          // runtime kind even when a spread's fields are opaque. Keep the
          // property set open so callers cannot use this as static field or
          // selector completeness.
          openProperties = true;
          evidence.push(selectorEvidence(property, context.source, context.filePath, "object-property"));
          continue;
        }
        for (const item of spread) for (const [name, values] of item.properties ?? []) {
          properties.set(name, [...(properties.get(name) ?? []), ...values]);
        }
        if (spread.some((item) => item.openProperties)) openProperties = true;
        evidence.push(...spread.flatMap((item) => item.evidence));
        continue;
      }
      if (!ts.isPropertyAssignment(property) && !ts.isShorthandPropertyAssignment(property)) return null;
      const name = selectorPropertyName(property.name);
      if (!name) {
        openProperties = true;
        evidence.push(selectorEvidence(property.name, context.source, context.filePath, "object-property"));
        continue;
      }
      const initializer = ts.isPropertyAssignment(property) ? property.initializer : property.name;
      const resolved = evaluateSelectorValues(initializer, context, new Set(visited))
        ?? [{ kind: "opaque" as const, evidence: [] }];
      properties.set(name, [...(properties.get(name) ?? []), ...resolved]);
      evidence.push(selectorEvidence(property.name, context.source, context.filePath, "object-property"),
        ...resolved.flatMap((item) => item.evidence));
    }
    return [{ kind: "object", properties, ...(openProperties ? { openProperties: true } : {}), evidence: normalizeSelectorEvidence(evidence) }];
  }
  if (ts.isNewExpression(value) && ts.isIdentifier(value.expression) && value.expression.text === "Set"
    && value.arguments?.length === 1
    && !resolveLexicalBinding("Set", value.expression, context.source)
    && setIntrinsicsArePristine(context.contexts)) {
    const resolved = evaluateSelectorValues(value.arguments[0], context, new Set(visited));
    if (!resolved || resolved.length !== 1 || resolved[0].kind !== "array") return null;
    return [{ ...resolved[0], evidence: normalizeSelectorEvidence([
      selectorEvidence(value, context.source, context.filePath, "set-membership"), ...resolved[0].evidence
    ]) }];
  }
  if (ts.isIdentifier(value)) return evaluateSelectorIdentifier(value, context, visited);
  if (ts.isPropertyAccessExpression(value)) {
    const owner = evaluateSelectorValues(value.expression, context, new Set(visited));
    return owner ? selectStaticProperty(owner, value.name.text) : null;
  }
  if (ts.isElementAccessExpression(value) && value.argumentExpression) {
    const owner = evaluateSelectorValues(value.expression, context, new Set(visited));
    const keys = evaluateSelectorValues(value.argumentExpression, context, new Set(visited));
    if (!owner || !keys) return null;
    const names = selectorStrings(keys);
    if (names.length === 0) return null;
    const selected = names.flatMap((name) => selectStaticProperty(owner, name) ?? []);
    return selected.length > 0 ? selected : null;
  }
  if (ts.isCallExpression(value)) return evaluateSelectorCall(value, context, visited);
  return null;
}

function evaluateSelectorIdentifier(identifier: ts.Identifier, context: SelectorEvaluationContext, visited: Set<string>): StaticSelectorValue[] | null {
  const constrained = evaluateDominatingSetConstraint(identifier, context, visited);
  if (constrained) return constrained;
  const binding = resolveLexicalBinding(identifier.text, identifier, context.source);
  if (!binding) return null;
  const key = `${context.filePath}:${binding.node.getStart(context.source)}`;
  if (visited.has(key)) return null;
  const next = new Set(visited).add(key);
  if (binding.kind === "variable") {
    const supplied = context.bindingValues?.get(binding.node);
    if (supplied) return projectBindingValues(binding.node.name, identifier.text, supplied);
    if (ts.isVariableDeclaration(binding.node) && ts.isForOfStatement(binding.node.parent.parent)) {
      const iterable = evaluateSelectorValues(binding.node.parent.parent.expression, context, next);
      if (!iterable || iterable.some((item) => item.kind !== "array")) return null;
      const elements = narrowSelectorObjectsForUse(identifier, binding.node,
        iterable.flatMap((item) => item.elements ?? []), context);
      if (!elements) return null;
      return projectBindingValues(binding.node.name, identifier.text, elements);
    }
    const reactState = evaluateSelectorReactStateBinding(identifier.text, binding.node, identifier, context, next);
    if (reactState) return reactState;
    const accumulated = evaluateAppendOnlyArrayBinding(identifier.text, binding.node, identifier, context, next);
    if (accumulated) return accumulated;
    if (!isImmutableVariable(binding.node, identifier) || !binding.node.initializer
      || !selectorBindingIsUnmutated(binding.node, context.source, identifier)) return null;
    const resolved = evaluateSelectorValues(binding.node.initializer, context, next);
    return resolved ? projectBindingValues(binding.node.name, identifier.text, resolved) : null;
  }
  if (binding.kind === "parameter") {
    const supplied = context.parameterValues?.get(binding.node);
    if (supplied) return projectBindingValues(binding.node.name, identifier.text, supplied);
    return evaluateSelectorParameter(binding.node, identifier.text, context, next);
  }
  return null;
}

function correlatedObjectForOfBinding(
  object: ts.ObjectLiteralExpression,
  source: ts.SourceFile
): ts.VariableDeclaration | null {
  const candidates = new Set<ts.VariableDeclaration>();
  for (const property of object.properties) {
    if (!ts.isPropertyAssignment(property) && !ts.isShorthandPropertyAssignment(property)) continue;
    const initializer = ts.isPropertyAssignment(property) ? property.initializer : property.name;
    const visit = (node: ts.Node): void => {
      if (ts.isIdentifier(node)) {
        const binding = resolveLexicalBinding(node.text, node, source);
        if (binding?.kind === "variable" && ts.isForOfStatement(binding.node.parent.parent)) candidates.add(binding.node);
      }
      ts.forEachChild(node, visit);
    };
    visit(initializer);
  }
  return candidates.size === 1 ? [...candidates][0] : null;
}

interface SelectorObjectConstraint {
  property: string;
  value: string | boolean;
  equal: boolean;
  test: "truthy" | "equality";
}

function narrowSelectorObjectsForUse(
  use: ts.Identifier,
  declaration: ts.VariableDeclaration,
  values: StaticSelectorValue[],
  context: SelectorEvaluationContext
): StaticSelectorValue[] | null {
  let narrowed = values;
  for (let current: ts.Node | undefined = use; current?.parent; current = current.parent) {
    const parent = current.parent;
    if (!ts.isIfStatement(parent)) continue;
    const branch = isAncestorNode(parent.thenStatement, use) ? "then"
      : parent.elseStatement && isAncestorNode(parent.elseStatement, use) ? "else" : null;
    if (!branch) continue;
    const constraint = selectorObjectConstraint(parent.expression, declaration, context.source);
    if (!constraint) continue;
    const expected = branch === "then" ? constraint.equal : !constraint.equal;
    const matches = narrowed.map((value) => selectorObjectMatches(value, constraint));
    if (matches.some((match) => match === null)) return null;
    narrowed = narrowed.filter((_value, index) => matches[index] === expected);
  }
  return narrowed;
}

function selectorObjectConstraint(
  input: ts.Expression,
  declaration: ts.VariableDeclaration,
  source: ts.SourceFile
): SelectorObjectConstraint | null {
  const expression = unwrapAliasExpression(input);
  if (ts.isPrefixUnaryExpression(expression) && expression.operator === ts.SyntaxKind.ExclamationToken) {
    const member = selectorBindingProperty(expression.operand, declaration, source);
    return member ? { property: member, value: true, equal: false, test: "truthy" } : null;
  }
  const truthy = selectorBindingProperty(expression, declaration, source);
  if (truthy) return { property: truthy, value: true, equal: true, test: "truthy" };
  if (!ts.isBinaryExpression(expression)
    || ![ts.SyntaxKind.EqualsEqualsEqualsToken, ts.SyntaxKind.EqualsEqualsToken,
      ts.SyntaxKind.ExclamationEqualsEqualsToken, ts.SyntaxKind.ExclamationEqualsToken].includes(expression.operatorToken.kind)) return null;
  const leftProperty = selectorBindingProperty(expression.left, declaration, source);
  const rightProperty = selectorBindingProperty(expression.right, declaration, source);
  const literal = leftProperty ? selectorConstraintLiteral(expression.right)
    : rightProperty ? selectorConstraintLiteral(expression.left) : null;
  const property = leftProperty ?? rightProperty;
  if (!property || literal === null) return null;
  return { property, value: literal,
    equal: [ts.SyntaxKind.EqualsEqualsEqualsToken, ts.SyntaxKind.EqualsEqualsToken].includes(expression.operatorToken.kind),
    test: "equality" };
}

function selectorBindingProperty(input: ts.Expression, declaration: ts.VariableDeclaration, source: ts.SourceFile): string | null {
  const expression = unwrapAliasExpression(input);
  if (!ts.isPropertyAccessExpression(expression)) return null;
  const owner = unwrapAliasExpression(expression.expression);
  return ts.isIdentifier(owner) && resolveLexicalBinding(owner.text, owner, source)?.node === declaration
    ? expression.name.text : null;
}

function selectorConstraintLiteral(input: ts.Expression): string | boolean | null {
  const expression = unwrapAliasExpression(input);
  if (ts.isStringLiteralLike(expression)) return expression.text;
  if (expression.kind === ts.SyntaxKind.TrueKeyword) return true;
  if (expression.kind === ts.SyntaxKind.FalseKeyword) return false;
  return null;
}

function selectorObjectMatches(value: StaticSelectorValue, constraint: SelectorObjectConstraint): boolean | null {
  if (value.kind !== "object") return false;
  const selected = value.properties?.get(constraint.property);
  if (!selected) return value.openProperties ? null : false;
  const matches: boolean[] = [];
  for (const item of selected) {
  if (constraint.test === "truthy") {
      if (item.kind === "opaque") return null;
      if (item.kind === "boolean") matches.push(item.value === true);
      else if (item.kind === "string") matches.push(item.value !== "");
      else if (item.kind === "undefined") matches.push(false);
      else matches.push(true);
    } else if (typeof constraint.value === "boolean" && item.kind === "boolean") {
      matches.push(item.value === constraint.value);
    } else if (typeof constraint.value === "string" && item.kind === "string") {
      matches.push(item.value === constraint.value);
    } else if (item.kind === "opaque") {
      return null;
    } else {
      matches.push(false);
    }
  }
  const result = matches.length > 0 && matches.every((match) => match === matches[0]) ? matches[0] : null;
  return value.openProperties && result === false ? null : result;
}

function evaluateDominatingSetConstraint(identifier: ts.Identifier, context: SelectorEvaluationContext, visited: Set<string>): StaticSelectorValue[] | null {
  const binding = resolveLexicalBinding(identifier.text, identifier, context.source)?.node;
  if (!binding) return null;
  for (let current: ts.Node | undefined = identifier; current; current = current.parent) {
    if (!current.parent) continue;
    if (ts.isIfStatement(current.parent)) {
      const positive = setMembershipCondition(current.parent.expression, identifier.text, binding, context.source);
      if (positive && isAncestorNode(current.parent.thenStatement, identifier)) {
        return evaluateSetMembershipOwner(positive, context, visited);
      }
    }
    if (!ts.isBlock(current.parent)) continue;
    const statement = current.parent.statements.find((candidate) => isAncestorNode(candidate, identifier));
    if (!statement) continue;
    const index = current.parent.statements.indexOf(statement);
    for (const prior of current.parent.statements.slice(0, index)) {
      if (!ts.isIfStatement(prior) || !statementTerminates(prior.thenStatement)) continue;
      const condition = unwrapAliasExpression(prior.expression);
      if (!ts.isPrefixUnaryExpression(condition) || condition.operator !== ts.SyntaxKind.ExclamationToken) continue;
      const owner = setMembershipCondition(condition.operand, identifier.text, binding, context.source);
      if (owner) return evaluateSetMembershipOwner(owner, context, visited);
    }
  }
  return null;
}

function setMembershipCondition(expression: ts.Expression, name: string, binding: ts.Node, source: ts.SourceFile): ts.Expression | null {
  const call = unwrapAliasExpression(expression);
  if (!ts.isCallExpression(call) || call.arguments.length !== 1) return null;
  const callee = unwrapAliasExpression(call.expression);
  if (!ts.isPropertyAccessExpression(callee) || callee.name.text !== "has") return null;
  const argument = unwrapAliasExpression(call.arguments[0]);
  return ts.isIdentifier(argument) && argument.text === name
    && resolveLexicalBinding(name, argument, source)?.node === binding ? callee.expression : null;
}

function evaluateSetMembershipOwner(owner: ts.Expression, context: SelectorEvaluationContext, visited: Set<string>): StaticSelectorValue[] | null {
  const resolved = evaluateSelectorValues(owner, context, new Set(visited));
  if (!resolved || resolved.some((item) => item.kind !== "array")) return null;
  return normalizeStaticValues(resolved.flatMap((item) => item.elements ?? []).map((item) => ({
    ...item,
    evidence: normalizeSelectorEvidence(item.evidence.map((evidence) => ({ ...evidence, derivation: "set-membership" as const })))
  })));
}

function statementTerminates(statement: ts.Statement): boolean {
  if (ts.isReturnStatement(statement) || ts.isThrowStatement(statement)) return true;
  if (ts.isBlock(statement)) {
    const last = statement.statements.at(-1);
    return Boolean(last && statementTerminates(last));
  }
  if (ts.isIfStatement(statement)) return Boolean(statement.elseStatement
    && statementTerminates(statement.thenStatement) && statementTerminates(statement.elseStatement));
  if (ts.isTryStatement(statement)) {
    if (statement.finallyBlock && statementTerminates(statement.finallyBlock)) return true;
    return statementTerminates(statement.tryBlock)
      && (!statement.catchClause || statementTerminates(statement.catchClause.block));
  }
  return false;
}

function isStaticallyUnreachable(node: ts.Node, boundary: ts.Node): boolean {
  let child: ts.Node = node;
  for (let current = node.parent; current; child = current, current = current.parent) {
    if (ts.isBlock(current) || ts.isSourceFile(current)) {
      const statement = current.statements.find((candidate) => candidate === child || isAncestorNode(candidate, child));
      if (statement) {
        const index = current.statements.indexOf(statement);
        if (current.statements.slice(0, index).some(statementTerminates)) return true;
      }
    }
    if (current === boundary) break;
  }
  return false;
}

const selectorParameterCache = new WeakMap<Map<string, SourceContext>, Map<string, StaticSelectorValue[] | null>>();

function evaluateSelectorParameter(parameter: ts.ParameterDeclaration, bindingName: string, context: SelectorEvaluationContext, visited: Set<string>): StaticSelectorValue[] | null {
  const cache = selectorParameterCache.get(context.contexts) ?? new Map<string, StaticSelectorValue[] | null>();
  selectorParameterCache.set(context.contexts, cache);
  const cacheKey = `${parameter.getSourceFile().fileName}:${parameter.getStart(parameter.getSourceFile())}:${bindingName}`;
  if (!context.parameterValues && cache.has(cacheKey)) return cache.get(cacheKey) ?? null;
  if (parameter.dotDotDotToken || parameter.initializer) return null;
  const owner = parameter.parent;
  if (!ts.isFunctionLike(owner) || !("body" in owner) || !owner.body) return null;
  const executableOwner = owner as ts.FunctionLikeDeclaration;
  const directReferencesClosed = selectorCallableReferencesAreClosed(executableOwner, context.contexts);
  const index = executableOwner.parameters.indexOf(parameter);
  const values: StaticSelectorValue[] = [];
  let calls = 0;
  let resolvedCalls = 0;
  for (const caller of context.contexts.values()) {
    const visitCalls = (node: ts.Node): void => {
      if (ts.isCallExpression(node) && resolveCallableTarget(node.expression, caller, context.contexts) === executableOwner) {
        if (isTransparentForwarderInvocation(node, executableOwner, caller, context.contexts)) {
          ts.forEachChild(node, visitCalls);
          return;
        }
        calls += 1;
        const argument = node.arguments[index];
        if (!argument || ts.isSpreadElement(argument)) return;
        const argumentValues = evaluateSelectorValues(argument, {
          filePath: caller.item.relativePath,
          source: caller.source,
          contexts: context.contexts
        }, new Set(visited));
        const resolved = argumentValues ? projectBindingValues(parameter.name, bindingName, argumentValues) : null;
        if (resolved) {
          resolvedCalls += 1;
          values.push(...resolved.map((item) => ({
            ...item,
            evidence: normalizeSelectorEvidence([
              selectorEvidence(argument, caller.source, caller.item.relativePath, "caller-argument"),
              ...item.evidence
            ])
          })));
        }
      }
      ts.forEachChild(node, visitCalls);
    };
    visitCalls(caller.source);
  }
  if (calls === 0) {
    const hookArguments = selectorMutationHookArguments(executableOwner, context);
    for (const { argument, caller } of hookArguments ?? []) {
      calls += 1;
      const argumentValues = evaluateSelectorValues(argument, caller, new Set(visited));
      const resolved = argumentValues ? projectBindingValues(parameter.name, bindingName, argumentValues) : null;
      if (!resolved) continue;
      resolvedCalls += 1;
      values.push(...resolved.map((item) => ({
        ...item,
        evidence: normalizeSelectorEvidence([
          selectorEvidence(argument, caller.source, caller.filePath, "caller-argument"),
          ...item.evidence
        ])
      })));
    }
  }
  if (calls === 0) {
    const inlineArguments = selectorInlineComponentCallbackArguments(executableOwner, index, context);
    for (const { argument, caller } of inlineArguments ?? []) {
      calls += 1;
      const argumentValues = evaluateSelectorValues(argument, caller, new Set(visited));
      const resolved = argumentValues ? projectBindingValues(parameter.name, bindingName, argumentValues) : null;
      if (!resolved) continue;
      resolvedCalls += 1;
      values.push(...resolved.map((item) => ({
        ...item,
        evidence: normalizeSelectorEvidence([
          selectorEvidence(argument, caller.source, caller.filePath, "caller-argument"),
          ...item.evidence
        ])
      })));
    }
  }
  if (!directReferencesClosed) {
    const componentArguments = selectorComponentCallbackArguments(executableOwner, index, context.contexts);
    if (!componentArguments) return null;
    for (const { argument, caller } of componentArguments) {
      calls += 1;
      const argumentValues = evaluateSelectorValues(argument, caller, new Set(visited));
      const resolved = argumentValues ? projectBindingValues(parameter.name, bindingName, argumentValues) : null;
      if (!resolved) continue;
      resolvedCalls += 1;
      values.push(...resolved.map((item) => ({
        ...item,
        evidence: normalizeSelectorEvidence([
          selectorEvidence(argument, caller.source, caller.filePath, "caller-argument"),
          ...item.evidence
        ])
      })));
    }
  }
  const result = calls > 0 && resolvedCalls === calls ? normalizeStaticValues(values) : null;
  if (!context.parameterValues) cache.set(cacheKey, result);
  return result;
}

function selectorInlineComponentCallbackArguments(
  owner: ts.FunctionLikeDeclaration,
  argumentIndex: number,
  context: SelectorEvaluationContext
): Array<{ argument: ts.Expression; caller: SelectorEvaluationContext }> | null {
  if (!ts.isArrowFunction(owner) && !ts.isFunctionExpression(owner)) return null;
  let expression: ts.Node = owner;
  while (expression.parent && (ts.isParenthesizedExpression(expression.parent)
    || ts.isAsExpression(expression.parent) || ts.isTypeAssertionExpression(expression.parent)
    || ts.isNonNullExpression(expression.parent) || ts.isSatisfiesExpression(expression.parent))) {
    expression = expression.parent;
  }
  const jsxExpression = expression.parent;
  if (!ts.isJsxExpression(jsxExpression) || jsxExpression.expression !== expression
    || !ts.isJsxAttribute(jsxExpression.parent)) return null;
  const attribute = jsxExpression.parent;
  if (!ts.isIdentifier(attribute.name)) return null;
  const opening = attribute.parent.parent;
  if ((!ts.isJsxOpeningElement(opening) && !ts.isJsxSelfClosingElement(opening))
    || !ts.isIdentifier(opening.tagName) || opening.attributes.properties.some(ts.isJsxSpreadAttribute)) return null;
  const caller = contextForSource(owner.getSourceFile(), context.contexts);
  const component = resolveJsxCallable(opening.tagName, caller, context.contexts);
  if (!component) return null;
  const propName = attribute.name.text;
  const parameter = component.parameters.find((candidate) => ts.isObjectBindingPattern(candidate.name)
    && candidate.name.elements.some((element) => !element.dotDotDotToken
      && (element.propertyName ? selectorPropertyName(element.propertyName)
        : ts.isIdentifier(element.name) ? element.name.text : null) === propName));
  if (!parameter || !ts.isObjectBindingPattern(parameter.name)) return null;
  const element = parameter.name.elements.find((candidate) => !candidate.dotDotDotToken
    && (candidate.propertyName ? selectorPropertyName(candidate.propertyName)
      : ts.isIdentifier(candidate.name) ? candidate.name.text : null) === propName);
  if (!element || !ts.isIdentifier(element.name)) return null;
  const propBinding = element.name;
  const componentContext = contextForSource(component.getSourceFile(), context.contexts);
  const result: Array<{ argument: ts.Expression; caller: SelectorEvaluationContext }> = [];
  let unsafe = false;
  const visit = (candidate: ts.Node): void => {
    if (unsafe) return;
    if (ts.isIdentifier(candidate) && ts.isJsxAttribute(candidate.parent)
      && candidate.parent.name === candidate) return;
    if (ts.isIdentifier(candidate) && candidate !== propBinding && candidate.text === propBinding.text
      && resolveLexicalBinding(candidate.text, candidate, componentContext.source)?.node === parameter) {
      const invocation = candidate.parent;
      if (!ts.isCallExpression(invocation) || unwrapAliasExpression(invocation.expression) !== candidate) {
        unsafe = true;
        return;
      }
      const argument = invocation.arguments[argumentIndex];
      if (!argument || ts.isSpreadElement(argument)) {
        unsafe = true;
        return;
      }
      result.push({ argument, caller: {
        filePath: componentContext.item.relativePath,
        source: componentContext.source,
        contexts: context.contexts
      } });
    }
    ts.forEachChild(candidate, visit);
  };
  visit(component);
  return !unsafe && result.length > 0 ? result : null;
}

function selectorComponentCallbackArguments(
  owner: ts.FunctionLikeDeclaration,
  argumentIndex: number,
  contexts: Map<string, SourceContext>
): Array<{ argument: ts.Expression; caller: SelectorEvaluationContext }> | null {
  const result: Array<{ argument: ts.Expression; caller: SelectorEvaluationContext }> = [];
  let foundOwnerReference = false;
  let unsafe = false;
  for (const caller of contexts.values()) {
    const visitOwner = (node: ts.Node): void => {
      if (unsafe || !ts.isIdentifier(node) || resolveCallableTarget(node, caller, contexts) !== owner) {
        if (!unsafe) ts.forEachChild(node, visitOwner);
        return;
      }
      const declarationName = (ts.isFunctionDeclaration(owner) || ts.isFunctionExpression(owner)) ? owner.name : undefined;
      const variable = (ts.isArrowFunction(owner) || ts.isFunctionExpression(owner)) && ts.isVariableDeclaration(owner.parent)
        ? owner.parent : undefined;
      if (node === declarationName || node === variable?.name || (ts.isImportSpecifier(node.parent) && node.parent.name === node)) return;
      if (ts.isCallExpression(node.parent) && unwrapAliasExpression(node.parent.expression) === node) return;
      const expression = node.parent;
      const attribute = ts.isJsxExpression(expression) && expression.expression === node && ts.isJsxAttribute(expression.parent)
        ? expression.parent : null;
      const opening = attribute?.parent.parent;
      if (!attribute || !opening || (!ts.isJsxOpeningElement(opening) && !ts.isJsxSelfClosingElement(opening))
        || !ts.isIdentifier(attribute.name) || !ts.isIdentifier(opening.tagName)
        || opening.attributes.properties.some(ts.isJsxSpreadAttribute)) {
        unsafe = true;
        return;
      }
      const component = resolveJsxCallable(opening.tagName, caller, contexts);
      if (!component) {
        unsafe = true;
        return;
      }
      const propName = attribute.name.text;
      const parameter = component.parameters.find((candidate) => ts.isObjectBindingPattern(candidate.name)
        && candidate.name.elements.some((element) => !element.dotDotDotToken
          && (element.propertyName ? selectorPropertyName(element.propertyName)
            : ts.isIdentifier(element.name) ? element.name.text : null) === propName));
      if (!parameter || !ts.isObjectBindingPattern(parameter.name)) {
        unsafe = true;
        return;
      }
      const element = parameter.name.elements.find((candidate) => !candidate.dotDotDotToken
        && (candidate.propertyName ? selectorPropertyName(candidate.propertyName)
          : ts.isIdentifier(candidate.name) ? candidate.name.text : null) === propName);
      if (!element || !ts.isIdentifier(element.name)) {
        unsafe = true;
        return;
      }
      foundOwnerReference = true;
      const propBinding = element.name;
      const componentContext = contextForSource(component.getSourceFile(), contexts);
      const visitProp = (candidate: ts.Node): void => {
        if (unsafe) return;
        if (ts.isIdentifier(candidate) && ts.isJsxAttribute(candidate.parent)
          && candidate.parent.name === candidate) return;
        if (ts.isIdentifier(candidate) && candidate !== propBinding && candidate.text === propBinding.text
          && resolveLexicalBinding(candidate.text, candidate, componentContext.source)?.node === parameter) {
          const call = candidate.parent;
          if (!ts.isCallExpression(call) || unwrapAliasExpression(call.expression) !== candidate) {
            unsafe = true;
            return;
          }
          const argument = call.arguments[argumentIndex];
          if (!argument || ts.isSpreadElement(argument)) {
            unsafe = true;
            return;
          }
          result.push({ argument, caller: {
            filePath: componentContext.item.relativePath,
            source: componentContext.source,
            contexts
          } });
        }
        ts.forEachChild(candidate, visitProp);
      };
      visitProp(component);
    };
    visitOwner(caller.source);
    if (unsafe) return null;
  }
  return foundOwnerReference && result.length > 0 ? result : null;
}

function selectorMutationHookArguments(
  owner: ts.FunctionLikeDeclaration,
  context: SelectorEvaluationContext
): Array<{ argument: ts.Expression; caller: SelectorEvaluationContext }> | null {
  const property = owner.parent;
  if (!ts.isPropertyAssignment(property) || property.initializer !== owner
    || selectorPropertyName(property.name) !== "mutationFn" || !ts.isObjectLiteralExpression(property.parent)) return null;
  const hookCall = property.parent.parent;
  if (!ts.isCallExpression(hookCall) || !hookCall.arguments.includes(property.parent)
    || !isReactQueryUseMutationCall(hookCall, context.source)) return null;
  const declaration = hookCall.parent;
  if (!ts.isVariableDeclaration(declaration) || !ts.isIdentifier(declaration.name)) return null;
  const bindingName = declaration.name.text;
  const caller: SelectorEvaluationContext = {
    filePath: context.filePath,
    source: context.source,
    contexts: context.contexts
  };
  const result: Array<{ argument: ts.Expression; caller: SelectorEvaluationContext }> = [];
  let unsafe = false;
  const scope = executionRoot(declaration.name);
  const visit = (node: ts.Node): void => {
    if (unsafe) return;
    if (ts.isIdentifier(node) && node !== declaration.name && node.text === bindingName
      && resolveLexicalBinding(bindingName, node, context.source)?.node === declaration) {
      const member = node.parent;
      if ((ts.isPropertyAccessExpression(member) || ts.isElementAccessExpression(member)) && member.expression === node) {
        const name = ts.isPropertyAccessExpression(member) ? member.name.text
          : member.argumentExpression && ts.isStringLiteralLike(member.argumentExpression) ? member.argumentExpression.text : null;
        if ((name === "mutate" || name === "mutateAsync") && ts.isCallExpression(member.parent)
          && member.parent.expression === member) {
          if (sourceProvenDormantInlineJsxCallback(member.parent, caller)) return;
          const argument = member.parent.arguments[0];
          if (!argument || ts.isSpreadElement(argument)) { unsafe = true; return; }
          result.push({ argument, caller });
        } else if (!name) {
          unsafe = true;
        }
        return;
      }
      if (ts.isArrayLiteralExpression(node.parent) && node.parent.elements.includes(node)) {
        const dependencyArray = node.parent;
        const hookCall = dependencyArray.parent;
        if (ts.isCallExpression(hookCall) && hookCall.arguments[1] === dependencyArray
          && isExactImportedCall(hookCall, context.source, "react",
            new Set(["useCallback", "useEffect", "useLayoutEffect", "useMemo"]))) return;
      }
      unsafe = true;
      return;
    }
    ts.forEachChild(node, visit);
  };
  visit(scope);
  return !unsafe && result.length > 0 ? result : null;
}

function sourceProvenDormantInlineJsxCallback(
  node: ts.Node,
  context: SelectorEvaluationContext
): boolean {
  const callback = findAncestor(node, (candidate): candidate is ts.ArrowFunction | ts.FunctionExpression =>
    ts.isArrowFunction(candidate) || ts.isFunctionExpression(candidate));
  if (!callback) return false;
  let expression: ts.Node = callback;
  while (expression.parent && (ts.isParenthesizedExpression(expression.parent)
    || ts.isAsExpression(expression.parent) || ts.isTypeAssertionExpression(expression.parent)
    || ts.isNonNullExpression(expression.parent) || ts.isSatisfiesExpression(expression.parent))) {
    expression = expression.parent;
  }
  const jsxExpression = expression.parent;
  if (!ts.isJsxExpression(jsxExpression) || jsxExpression.expression !== expression
    || !ts.isJsxAttribute(jsxExpression.parent)) return false;
  const attribute = jsxExpression.parent;
  if (!ts.isIdentifier(attribute.name)) return false;
  const opening = attribute.parent.parent;
  if ((!ts.isJsxOpeningElement(opening) && !ts.isJsxSelfClosingElement(opening))
    || !ts.isIdentifier(opening.tagName) || opening.attributes.properties.some(ts.isJsxSpreadAttribute)) return false;
  const caller = contextForSource(callback.getSourceFile(), context.contexts);
  const component = resolveJsxCallable(opening.tagName, caller, context.contexts);
  if (!component) return false;
  const propName = attribute.name.text;
  const parameter = component.parameters.find((candidate) => ts.isObjectBindingPattern(candidate.name)
    && candidate.name.elements.some((element) => !element.dotDotDotToken
      && (element.propertyName ? selectorPropertyName(element.propertyName)
        : ts.isIdentifier(element.name) ? element.name.text : null) === propName));
  if (!parameter || !ts.isObjectBindingPattern(parameter.name)) return false;
  const element = parameter.name.elements.find((candidate) => !candidate.dotDotDotToken
    && (candidate.propertyName ? selectorPropertyName(candidate.propertyName)
      : ts.isIdentifier(candidate.name) ? candidate.name.text : null) === propName);
  if (!element || !ts.isIdentifier(element.name)) return false;
  const propBinding = element.name;
  let referenced = false;
  const visit = (candidate: ts.Node): void => {
    if (referenced) return;
    if (ts.isIdentifier(candidate) && candidate !== propBinding && candidate.text === propBinding.text
      && !(ts.isJsxAttribute(candidate.parent) && candidate.parent.name === candidate)
      && resolveLexicalBinding(candidate.text, candidate, component.getSourceFile())?.node === parameter) {
      referenced = true;
      return;
    }
    ts.forEachChild(candidate, visit);
  };
  visit(component);
  return !referenced;
}

function selectorCallableReferencesAreClosed(owner: ts.FunctionLikeDeclaration, contexts: Map<string, SourceContext>): boolean {
  let closed = true;
  for (const context of contexts.values()) {
    const visitNode = (node: ts.Node): void => {
      if (!closed) return;
      if (ts.isIdentifier(node)) {
        const declarationName = (ts.isFunctionDeclaration(owner) || ts.isFunctionExpression(owner)) ? owner.name : undefined;
        const variable = (ts.isArrowFunction(owner) || ts.isFunctionExpression(owner)) && ts.isVariableDeclaration(owner.parent)
          ? owner.parent : undefined;
        if (node === declarationName || node === variable?.name
          || (ts.isImportSpecifier(node.parent) && node.parent.name === node)) {
          ts.forEachChild(node, visitNode);
          return;
        }
        if (resolveCallableTarget(node, context, contexts) === owner
          && (!ts.isCallExpression(node.parent) || unwrapAliasExpression(node.parent.expression) !== node)
          && !isTransparentCallableAliasReference(node, context.source)
          && !isTransparentClosedReactRefUse(node, owner, context, contexts)) {
          closed = false;
          return;
        }
      }
      ts.forEachChild(node, visitNode);
    };
    visitNode(context.source);
    if (!closed) break;
  }
  return closed;
}

function isTransparentClosedReactRefUse(
  reference: ts.Identifier,
  owner: ts.FunctionLikeDeclaration,
  context: SourceContext,
  contexts: Map<string, SourceContext>
): boolean {
  const member = reference.parent;
  if (!ts.isPropertyAccessExpression(member) || member.expression !== reference || member.name.text !== "current"
    || resolveCallableExpression(member, context, contexts, new Set(), true) !== owner) return false;
  const use = member.parent;
  if (ts.isCallExpression(use) && unwrapAliasExpression(use.expression) === member) return true;
  if (ts.isPropertyAccessExpression(use) && use.expression === member
    && (use.name.text === "flush" || use.name.text === "cancel")
    && ts.isCallExpression(use.parent) && use.parent.expression === use && use.parent.arguments.length === 0
    && isExactLodashDebounceRef(reference, context.source)) return true;
  if (ts.isBinaryExpression(use) && use.left === member
    && use.operatorToken.kind === ts.SyntaxKind.EqualsToken
    && resolveCallableTarget(use.right, context, contexts) === owner) return true;
  if (ts.isVariableDeclaration(use) && use.initializer === member && ts.isIdentifier(use.name)) {
    const list = use.parent;
    return ts.isVariableDeclarationList(list) && (list.flags & ts.NodeFlags.Const) !== 0;
  }
  return false;
}

function isExactLodashDebounceRef(reference: ts.Identifier, source: ts.SourceFile): boolean {
  const binding = resolveLexicalBinding(reference.text, reference, source);
  if (binding?.kind !== "variable" || !binding.node.initializer) return false;
  const initializer = unwrapAliasExpression(binding.node.initializer);
  if (!ts.isCallExpression(initializer) || !isExactImportedCall(initializer, source, "react", new Set(["useRef"]))) return false;
  const argument = initializer.arguments[0] && unwrapAliasExpression(initializer.arguments[0]);
  return Boolean(argument && ts.isCallExpression(argument)
    && isExactImportedCall(argument, source, "lodash", new Set(["debounce"])));
}

function isTransparentCallableAliasReference(reference: ts.Identifier, source: ts.SourceFile): boolean {
  const parent = reference.parent;
  if (ts.isCallExpression(parent) && parent.arguments.includes(reference)
    && (isExactImportedCall(parent, source, "react", new Set(["useRef", "useCallback"]))
      || isExactImportedCall(parent, source, "lodash", new Set(["debounce"])))) return true;
  if (ts.isBinaryExpression(parent) && parent.right === reference
    && parent.operatorToken.kind === ts.SyntaxKind.EqualsToken) {
    return isClosedReactRefCallableAssignment(reference, parent.left, source);
  }
  if (ts.isArrayLiteralExpression(parent) && parent.elements.includes(reference)) {
    const call = parent.parent;
    return ts.isCallExpression(call) && call.arguments[1] === parent
      && isExactImportedCall(call, source, "react", new Set(["useCallback", "useEffect", "useLayoutEffect", "useMemo"]));
  }
  return false;
}

function isClosedReactRefCallableAssignment(
  callableReference: ts.Identifier,
  assignmentTarget: ts.Expression,
  source: ts.SourceFile
): boolean {
  const left = unwrapAliasExpression(assignmentTarget);
  if (!ts.isPropertyAccessExpression(left) || left.name.text !== "current") return false;
  const refIdentifier = unwrapAliasExpression(left.expression);
  if (!ts.isIdentifier(refIdentifier)) return false;
  const refBinding = resolveLexicalBinding(refIdentifier.text, refIdentifier, source);
  if (refBinding?.kind !== "variable" || !ts.isVariableDeclaration(refBinding.node)
    || !ts.isIdentifier(refBinding.node.name) || !refBinding.node.initializer) return false;
  const declarationList = refBinding.node.parent;
  const statement = declarationList.parent;
  if (!ts.isVariableDeclarationList(declarationList)
    || (declarationList.flags & ts.NodeFlags.Const) === 0
    || (ts.isVariableStatement(statement)
      && statement.modifiers?.some((modifier) => modifier.kind === ts.SyntaxKind.ExportKeyword))) return false;
  const initializer = unwrapSelectorExpression(refBinding.node.initializer);
  if (!ts.isCallExpression(initializer) || !isExactImportedCall(initializer, source, "react", new Set(["useRef"]))) return false;
  const initialCallable = initializer.arguments[0] && unwrapAliasExpression(initializer.arguments[0]);
  const callableBinding = resolveLexicalBinding(callableReference.text, callableReference, source)?.node;
  if (!initialCallable || !ts.isIdentifier(initialCallable)
    || resolveLexicalBinding(initialCallable.text, initialCallable, source)?.node !== callableBinding) return false;
  const refName = refBinding.node.name.text;

  let closed = true;
  const visit = (node: ts.Node): void => {
    if (!closed) return;
    if (ts.isIdentifier(node) && node !== refBinding.node.name && node.text === refName
      && resolveLexicalBinding(node.text, node, source)?.node === refBinding.node) {
      const member = node.parent;
      if (!ts.isPropertyAccessExpression(member) || member.expression !== node || member.name.text !== "current") {
        closed = false;
        return;
      }
      const use = member.parent;
      if (ts.isCallExpression(use) && unwrapAliasExpression(use.expression) === member) return;
      if (ts.isBinaryExpression(use) && use.left === member && use.operatorToken.kind === ts.SyntaxKind.EqualsToken) {
        const assigned = unwrapAliasExpression(use.right);
        if (ts.isIdentifier(assigned)
          && resolveLexicalBinding(assigned.text, assigned, source)?.node === callableBinding) return;
      }
      closed = false;
      return;
    }
    ts.forEachChild(node, visit);
  };
  visit(source);
  return closed;
}

function isTransparentForwarderInvocation(
  call: ts.CallExpression,
  owner: ts.FunctionLikeDeclaration,
  caller: SourceContext,
  contexts: Map<string, SourceContext>
): boolean {
  const callback = findAncestor(call, (node): node is ts.ArrowFunction | ts.FunctionExpression =>
    ts.isArrowFunction(node) || ts.isFunctionExpression(node));
  if (!callback || callback.parameters.length !== 1 || !callback.parameters[0].dotDotDotToken) return false;
  const wrapper = callback.parent;
  return ts.isCallExpression(wrapper) && wrapper.arguments[0] === callback
    && isExactImportedCall(wrapper, caller.source, "lodash", new Set(["debounce"]))
    && resolveForwardedCallable(callback, caller, contexts, new Set()) === owner;
}

function evaluateAppendOnlyArrayBinding(
  bindingName: string,
  declaration: ts.VariableDeclaration,
  use: ts.Identifier,
  context: SelectorEvaluationContext,
  visited: Set<string>
): StaticSelectorValue[] | null {
  if (!arrayIntrinsicsArePristine(context.contexts)) return null;
  if (!ts.isIdentifier(declaration.name) || !declaration.initializer) return null;
  const initial = evaluateSelectorValues(declaration.initializer, context, new Set(visited));
  if (!initial || initial.length !== 1 || initial[0].kind !== "array") return null;
  const elements = [...(initial[0].elements ?? [])];
  let unsafe = false;
  const scope = executionRoot(declaration.name);
  // Append order cannot be inferred from a deferred closure's source position.
  // The immutable-binding fallback still handles unchanged captured arrays.
  if (executionRoot(use) !== scope) return null;
  if (isStaticallyUnreachable(use, scope)) return null;
  const usePosition = use.getStart(context.source);
  const visit = (node: ts.Node): void => {
    if (unsafe) return;
    if (node !== scope && isStaticallyUnreachable(node, scope)) return;
    if (node.getStart(context.source) > usePosition) return;
    if (node !== scope && ts.isFunctionLike(node) && !isAncestorNode(node, use)) {
      if (functionBodyMutatesBinding(node, bindingName, declaration, context.source)) unsafe = true;
      return;
    }
    if (ts.isIdentifier(node) && node !== declaration.name && node !== use && node.text === bindingName
      && resolveLexicalBinding(bindingName, node, context.source)?.node === declaration) {
      const member = node.parent;
      const call = member.parent;
      if (ts.isPropertyAccessExpression(member) && member.expression === node && member.name.text === "push"
        && ts.isCallExpression(call) && call.expression === member) {
        for (const argument of call.arguments) {
          if (ts.isSpreadElement(argument)) { unsafe = true; return; }
          const resolved = evaluateSelectorValues(argument, context, new Set(visited));
          if (!resolved) { unsafe = true; return; }
          elements.push(...resolved);
        }
        return;
      }
      if (!selectorBindingReferenceMutatesValue(node) && selectorAccumulatedArrayReferenceIsSafe(node, use)) return;
      unsafe = true;
      return;
    }
    ts.forEachChild(node, visit);
  };
  visit(scope);
  return unsafe ? null : [{
    kind: "array",
    elements: normalizeStaticValues(elements),
    evidence: normalizeSelectorEvidence([...initial[0].evidence, ...elements.flatMap((item) => item.evidence)])
  }];
}

function selectorAccumulatedArrayReferenceIsSafe(node: ts.Identifier, evaluatedUse: ts.Identifier): boolean {
  if (node === evaluatedUse) return true;
  const parent = node.parent;
  if (ts.isPropertyAssignment(parent) && parent.name === node) return true;
  if (ts.isPropertyAccessExpression(parent) && parent.expression === node && parent.name.text === "length") return true;
  if (ts.isForOfStatement(parent) && parent.expression === node) return true;
  return false;
}

function evaluateSelectorReactStateBinding(
  bindingName: string,
  declaration: ts.VariableDeclaration,
  use: ts.Identifier,
  context: SelectorEvaluationContext,
  visited: Set<string>
): StaticSelectorValue[] | null {
  if (!ts.isArrayBindingPattern(declaration.name) || !declaration.initializer) return null;
  const initializer = unwrapSelectorExpression(declaration.initializer);
  if (!ts.isCallExpression(initializer) || !isReactUseStateCall(initializer, context.source)) return null;
  const valueElement = declaration.name.elements[0];
  const setterElement = declaration.name.elements[1];
  if (!valueElement || !ts.isBindingElement(valueElement) || !ts.isIdentifier(valueElement.name)
    || valueElement.name.text !== bindingName || valueElement.dotDotDotToken || valueElement.initializer
    || !setterElement || !ts.isBindingElement(setterElement) || !ts.isIdentifier(setterElement.name)
    || setterElement.dotDotDotToken || setterElement.initializer || declaration.name.elements.length !== 2) return null;
  const setterName = setterElement.name.text;
  const values: StaticSelectorValue[] = [];
  const initialExpression = initializer.arguments[0];
  const initial = initialExpression && !ts.isSpreadElement(initialExpression)
    ? evaluateSelectorValues(initialExpression, context, new Set(visited))
      ?? evaluateComponentParameterValues(initialExpression, context, new Set(visited))
    : null;
  if (!initial) return null;
  values.push(...initial);
  let setterCalls = 0;
  let unsafe = false;
  const scope = executionRoot(declaration.name);
  const usePosition = use.getStart(context.source);
  const visit = (node: ts.Node): void => {
    if (unsafe) return;
    if (node === setterElement.name) return;
    if (ts.isIdentifier(node) && node !== valueElement.name && node.text === bindingName
      && resolveLexicalBinding(bindingName, node, context.source)?.node === declaration) {
      if (node.getStart(context.source) > usePosition) return;
      if (node === use) return;
      if (selectorBindingReferenceMutatesValue(node) || !selectorBindingReferenceIsSafe(node)) {
        unsafe = true;
      }
      return;
    }
    if (ts.isIdentifier(node) && node.text === setterName
      && resolveLexicalBinding(setterName, node, context.source)?.node === declaration) {
      const call = node.parent;
      if (!ts.isCallExpression(call) || unwrapAliasExpression(call.expression) !== node
        || call.arguments.length !== 1 || ts.isSpreadElement(call.arguments[0])) {
        unsafe = true;
        return;
      }
      setterCalls += 1;
      const resolved = evaluateSelectorValues(call.arguments[0], context, new Set(visited))
        ?? evaluateReactStateOuterObjectUpdate(call.arguments[0], context)
        ?? selectorControlledOptionValues(call, bindingName, declaration, context);
      if (!resolved) { unsafe = true; return; }
      values.push(...resolved);
      return;
    }
    ts.forEachChild(node, visit);
  };
  visit(scope);
  if (unsafe) return null;
  const usable = values.filter((value) => value.kind !== "opaque");
  if (usable.length === 0 || (values.some((value) => value.kind === "opaque")
    && !selectorStateUseExcludesOpaque(bindingName, declaration, use, context))) return null;
  return normalizeStaticValues(usable);
}

function evaluateReactStateOuterObjectUpdate(
  input: ts.Expression,
  context: SelectorEvaluationContext
): StaticSelectorValue[] | null {
  const callback = unwrapAliasExpression(input);
  if ((!ts.isArrowFunction(callback) && !ts.isFunctionExpression(callback)) || callback.modifiers?.some((item) => item.kind === ts.SyntaxKind.AsyncKeyword)
    || callback.parameters.length !== 1 || callback.parameters[0].dotDotDotToken || callback.parameters[0].initializer) return null;
  const returned = ts.isBlock(callback.body)
    ? callback.body.statements.length === 1 && ts.isReturnStatement(callback.body.statements[0])
      ? callback.body.statements[0].expression : undefined
    : callback.body;
  const object = returned && unwrapAliasExpression(returned);
  if (!object || !ts.isObjectLiteralExpression(object)) return null;
  return [{
    kind: "object",
    properties: new Map(),
    evidence: [selectorEvidence(object, context.source, context.filePath, "caller-argument")]
  }];
}

function isReactUseStateCall(call: ts.CallExpression, source: ts.SourceFile): boolean {
  const callee = unwrapAliasExpression(call.expression);
  if (!ts.isIdentifier(callee) || callee.text !== "useState") return false;
  const binding = resolveLexicalBinding(callee.text, callee, source);
  if (binding?.kind !== "import") return false;
  const declaration = findAncestor(binding.node, ts.isImportDeclaration);
  return Boolean(declaration && ts.isStringLiteralLike(declaration.moduleSpecifier)
    && declaration.moduleSpecifier.text === "react" && binding.exportedName === "useState");
}

function evaluateComponentParameterValues(
  input: ts.Expression,
  context: SelectorEvaluationContext,
  visited: Set<string>
): StaticSelectorValue[] | null {
  const expression = unwrapAliasExpression(input);
  if (!ts.isIdentifier(expression)) return null;
  const binding = resolveLexicalBinding(expression.text, expression, context.source);
  if (binding?.kind !== "parameter" || !ts.isObjectBindingPattern(binding.node.name)) return null;
  const element = binding.node.name.elements.find((candidate) => bindingNames(candidate.name).includes(expression.text));
  if (!element || element.dotDotDotToken || !element.initializer) return null;
  const propertyNode = element.propertyName ?? (ts.isIdentifier(element.name) ? element.name : null);
  const propertyName = propertyNode ? selectorPropertyName(propertyNode) : null;
  if (!propertyName) return null;
  const owner = binding.node.parent;
  if (!ts.isFunctionLike(owner) || !("body" in owner) || !owner.body) return null;
  const executableOwner = owner as ts.FunctionLikeDeclaration;
  const values: StaticSelectorValue[] = [];
  let callsites = 0;
  let resolvedCallsites = 0;
  for (const caller of context.contexts.values()) {
    const visit = (node: ts.Node): void => {
      if (!ts.isJsxOpeningElement(node) && !ts.isJsxSelfClosingElement(node)) {
        ts.forEachChild(node, visit);
        return;
      }
      if (!ts.isIdentifier(node.tagName) || resolveJsxCallable(node.tagName, caller, context.contexts) !== executableOwner) return;
      callsites += 1;
      if (node.attributes.properties.some(ts.isJsxSpreadAttribute)) return;
      const attribute = node.attributes.properties.find((item): item is ts.JsxAttribute =>
        ts.isJsxAttribute(item) && ts.isIdentifier(item.name) && item.name.text === propertyName);
      const resolved = !attribute
        ? evaluateSelectorValues(element.initializer!, context, new Set(visited))
        : attribute.initializer && ts.isStringLiteral(attribute.initializer)
          ? [{ kind: "string" as const, value: attribute.initializer.text,
            evidence: [selectorEvidence(attribute.initializer, caller.source, caller.item.relativePath, "caller-argument")] }]
          : attribute.initializer && ts.isJsxExpression(attribute.initializer) && attribute.initializer.expression
            ? evaluateSelectorValues(attribute.initializer.expression, {
              filePath: caller.item.relativePath, source: caller.source, contexts: context.contexts
            }, new Set(visited)) : null;
      if (resolved) {
        resolvedCallsites += 1;
        values.push(...resolved);
      }
    };
    visit(caller.source);
  }
  return callsites > 0 && resolvedCallsites === callsites ? normalizeStaticValues(values) : null;
}

function resolveJsxCallable(
  tag: ts.Identifier,
  caller: SourceContext,
  contexts: Map<string, SourceContext>
): ts.FunctionLikeDeclaration | null {
  const binding = resolveLexicalBinding(tag.text, tag, caller.source);
  if (binding?.kind === "function") return binding.node.body ? binding.node : null;
  if (binding?.kind !== "import") return null;
  const declaration = findAncestor(binding.node, ts.isImportDeclaration);
  if (!declaration || !ts.isStringLiteralLike(declaration.moduleSpecifier)) return null;
  const targetPath = resolveLocalModule(caller.item.relativePath, declaration.moduleSpecifier.text, contexts);
  const target = targetPath ? contexts.get(targetPath) : undefined;
  return target ? findExportedCallable(target.source, binding.exportedName) : null;
}

function selectorControlledOptionValues(
  setterCall: ts.CallExpression,
  stateName: string,
  declaration: ts.VariableDeclaration,
  context: SelectorEvaluationContext
): StaticSelectorValue[] | null {
  const arrow = findAncestor(setterCall, (node): node is ts.ArrowFunction | ts.FunctionExpression =>
    ts.isArrowFunction(node) || ts.isFunctionExpression(node));
  const attribute = arrow && findAncestor(arrow, ts.isJsxAttribute);
  const opening = attribute && findAncestor(attribute, (node): node is ts.JsxOpeningElement => ts.isJsxOpeningElement(node));
  if (!arrow || !attribute || !ts.isIdentifier(attribute.name) || attribute.name.text !== "onChange" || !opening
    || !ts.isIdentifier(opening.tagName) || opening.tagName.text !== "select") return null;
  const valueAttribute = opening.attributes.properties.find((item): item is ts.JsxAttribute =>
    ts.isJsxAttribute(item) && ts.isIdentifier(item.name) && item.name.text === "value");
  if (!valueAttribute?.initializer || !ts.isJsxExpression(valueAttribute.initializer)
    || !valueAttribute.initializer.expression || !ts.isIdentifier(valueAttribute.initializer.expression)
    || valueAttribute.initializer.expression.text !== stateName
    || resolveLexicalBinding(stateName, valueAttribute.initializer.expression, context.source)?.node !== declaration) return null;
  const expression = setterCall.arguments[0] && unwrapAliasExpression(setterCall.arguments[0]);
  if (!expression || !ts.isPropertyAccessExpression(expression) || expression.name.text !== "value") return null;
  const target = unwrapAliasExpression(expression.expression);
  if (!ts.isPropertyAccessExpression(target) || target.name.text !== "target") return null;
  const event = unwrapAliasExpression(target.expression);
  if (!ts.isIdentifier(event) || !arrow.parameters.some((parameter) => ts.isIdentifier(parameter.name)
    && parameter.name.text === event.text)) return null;
  const element = opening.parent;
  if (!ts.isJsxElement(element)) return null;
  const values: StaticSelectorValue[] = [];
  for (const child of element.children) {
    if (ts.isJsxText(child) && child.text.trim() === "") continue;
    if (!ts.isJsxElement(child)) return null;
    const childOpening = child.openingElement;
    if (!ts.isIdentifier(childOpening.tagName) || childOpening.tagName.text !== "option") return null;
    const optionValue = childOpening.attributes.properties.find((item): item is ts.JsxAttribute =>
      ts.isJsxAttribute(item) && ts.isIdentifier(item.name) && item.name.text === "value");
    if (!optionValue?.initializer || !ts.isStringLiteral(optionValue.initializer)) return null;
    values.push({ kind: "string", value: optionValue.initializer.text,
      evidence: [selectorEvidence(optionValue.initializer, context.source, context.filePath, "literal")] });
  }
  return values.length ? normalizeStaticValues(values) : null;
}

function selectorStateUseExcludesOpaque(
  bindingName: string,
  declaration: ts.VariableDeclaration,
  use: ts.Identifier,
  context: SelectorEvaluationContext
): boolean {
  for (let current: ts.Node | undefined = use; current?.parent; current = current.parent) {
    const parent = current.parent;
    if (ts.isBinaryExpression(parent) && isAncestorNode(parent.left, use)
      && [ts.SyntaxKind.BarBarToken, ts.SyntaxKind.QuestionQuestionToken].includes(parent.operatorToken.kind)) {
      const fallback = evaluateSelectorValues(parent.right, context, new Set());
      if (fallback) return true;
    }
    if (ts.isIfStatement(parent) && isAncestorNode(parent.thenStatement, use)
      && ts.isIdentifier(unwrapAliasExpression(parent.expression))
      && (unwrapAliasExpression(parent.expression) as ts.Identifier).text === bindingName) return true;
    if (!ts.isBlock(parent)) continue;
    const statement = parent.statements.find((candidate) => isAncestorNode(candidate, use));
    if (!statement) continue;
    for (const prior of parent.statements.slice(0, parent.statements.indexOf(statement))) {
      if (!ts.isIfStatement(prior) || !statementTerminates(prior.thenStatement)) continue;
      if (selectorConditionRejectsFalsyBinding(prior.expression, bindingName, declaration, context.source)) return true;
    }
  }
  return false;
}

function selectorConditionRejectsFalsyBinding(
  input: ts.Expression,
  bindingName: string,
  declaration: ts.VariableDeclaration,
  source: ts.SourceFile
): boolean {
  const condition = unwrapAliasExpression(input);
  if (ts.isPrefixUnaryExpression(condition) && condition.operator === ts.SyntaxKind.ExclamationToken) {
    const checked = unwrapAliasExpression(condition.operand);
    return ts.isIdentifier(checked) && checked.text === bindingName
      && resolveLexicalBinding(bindingName, checked, source)?.node === declaration;
  }
  return ts.isBinaryExpression(condition) && condition.operatorToken.kind === ts.SyntaxKind.BarBarToken
    && (selectorConditionRejectsFalsyBinding(condition.left, bindingName, declaration, source)
      || selectorConditionRejectsFalsyBinding(condition.right, bindingName, declaration, source));
}

function evaluateSelectorCall(call: ts.CallExpression, context: SelectorEvaluationContext, visited: Set<string>): StaticSelectorValue[] | null {
  const callee = unwrapAliasExpression(call.expression);
  if (ts.isPropertyAccessExpression(callee) && ts.isIdentifier(callee.expression) && callee.expression.text === "Object"
    && ["values", "entries"].includes(callee.name.text) && call.arguments.length === 1
    && !resolveLexicalBinding("Object", callee.expression, context.source)
    && objectValueEnumerationIntrinsicsArePristine(context.contexts)) {
    const resolved = evaluateSelectorValues(call.arguments[0], context, new Set(visited));
    if (!resolved || resolved.some((item) => item.kind !== "object")) return null;
    if (resolved.some((item) => item.openProperties)) return null;
    const elements: StaticSelectorValue[] = [];
    for (const item of resolved) for (const [name, values] of item.properties ?? []) {
      if (callee.name.text === "values") elements.push(...values);
      else elements.push({
        kind: "array",
        elements: [{ kind: "string", value: name, evidence: item.evidence }, ...values],
        evidence: normalizeSelectorEvidence(item.evidence)
      });
    }
    return [{ kind: "array", elements, evidence: normalizeSelectorEvidence(elements.flatMap((item) => item.evidence)) }];
  }
  if (ts.isPropertyAccessExpression(callee) && ["filter", "map"].includes(callee.name.text)) {
    if (!arrayIntrinsicsArePristine(context.contexts)) return null;
    if (selectorReceiverMethodCanBeOverridden(callee.expression, callee.name.text, context)) return null;
    const owner = evaluateSelectorValues(callee.expression, context, new Set(visited));
    if (!owner || owner.some((item) => item.kind !== "array")) return null;
    if (callee.name.text === "filter") return owner;
  }
  const target = resolveCallableTarget(call.expression, contextForSource(context.source, context.contexts), context.contexts);
  if (target) return evaluateCallableReturns(call, target, context, visited);
  return null;
}

const arrayIntrinsicRealmCache = new WeakMap<Map<string, SourceContext>, boolean>();
const setIntrinsicRealmCache = new WeakMap<Map<string, SourceContext>, boolean>();
const objectValueEnumerationIntrinsicRealmCache = new WeakMap<Map<string, SourceContext>, boolean>();

function arrayIntrinsicsArePristine(contexts: Map<string, SourceContext>): boolean {
  const cached = arrayIntrinsicRealmCache.get(contexts);
  if (cached !== undefined) return cached;
  let pristine = true;
  const mutationMembers = new Set(["map", "filter", "push"]);
  for (const context of contexts.values()) {
    const visit = (node: ts.Node): void => {
      if (!pristine) return;
      if (isArrayPrototypeExpression(node, context.source)) {
        pristine = false;
        return;
      }
      if (dangerousPrototypeCapabilityReference(node, context.source)) {
        pristine = false;
        return;
      }
      if (unboundedDynamicEvaluationCapabilityReference(node, context.source)) {
        pristine = false;
        return;
      }
      const mutatedMember = (expression: ts.Expression): string | null => {
        const target = unwrapAliasExpression(expression);
        if (ts.isPropertyAccessExpression(target)) return target.name.text;
        if (ts.isElementAccessExpression(target) && target.argumentExpression
          && ts.isStringLiteralLike(target.argumentExpression)) return target.argumentExpression.text;
        return null;
      };
      if (ts.isBinaryExpression(node) && isAssignmentOperator(node.operatorToken.kind)
        && mutationMembers.has(mutatedMember(node.left) ?? "")
        && ((ts.isPropertyAccessExpression(unwrapAliasExpression(node.left))
          && isArrayPrototypeExpression((unwrapAliasExpression(node.left) as ts.PropertyAccessExpression).expression, context.source))
          || (ts.isElementAccessExpression(unwrapAliasExpression(node.left))
            && isArrayPrototypeExpression((unwrapAliasExpression(node.left) as ts.ElementAccessExpression).expression, context.source)))) {
        pristine = false;
        return;
      }
      if (ts.isDeleteExpression(node) && mutationMembers.has(mutatedMember(node.expression) ?? "")
        && ((ts.isPropertyAccessExpression(unwrapAliasExpression(node.expression))
          && isArrayPrototypeExpression((unwrapAliasExpression(node.expression) as ts.PropertyAccessExpression).expression, context.source))
          || (ts.isElementAccessExpression(unwrapAliasExpression(node.expression))
            && isArrayPrototypeExpression((unwrapAliasExpression(node.expression) as ts.ElementAccessExpression).expression, context.source)))) {
        pristine = false;
        return;
      }
      if (ts.isCallExpression(node) && ts.isPropertyAccessExpression(node.expression)
        && ts.isIdentifier(node.expression.expression)
        && ["Object", "Reflect"].includes(node.expression.expression.text)
        && ["defineProperty", "set", "setPrototypeOf"].includes(node.expression.name.text)
        && !resolveLexicalBinding(node.expression.expression.text, node.expression.expression, context.source)) {
        const target = node.arguments[0] && unwrapAliasExpression(node.arguments[0]);
        const member = node.arguments[1];
        if (target && isArrayPrototypeExpression(target, context.source)
          && (node.expression.name.text === "setPrototypeOf"
            || (member && ts.isStringLiteralLike(member) && mutationMembers.has(member.text)))) {
          pristine = false;
          return;
        }
      }
      ts.forEachChild(node, visit);
    };
    visit(context.source);
    if (!pristine) break;
  }
  arrayIntrinsicRealmCache.set(contexts, pristine);
  return pristine;
}

function setIntrinsicsArePristine(contexts: Map<string, SourceContext>): boolean {
  const cached = setIntrinsicRealmCache.get(contexts);
  if (cached !== undefined) return cached;
  let pristine = true;
  for (const context of contexts.values()) {
    const visit = (node: ts.Node): void => {
      if (!pristine) return;
      if (ts.isBinaryExpression(node) && isAssignmentOperator(node.operatorToken.kind)
        && setPrototypeMemberIs(node.left, context.source, new Set(["has", "add"]))) {
        pristine = false;
        return;
      }
      if (ts.isDeleteExpression(node) && setPrototypeMemberIs(node.expression, context.source, new Set(["has", "add"]))) {
        pristine = false;
        return;
      }
      if (ts.isCallExpression(node) && intrinsicMutationCallTargetsMembers(node, context.source, isSetPrototypeExpression, new Set(["has", "add"]))) {
        pristine = false;
        return;
      }
      ts.forEachChild(node, visit);
    };
    visit(context.source);
    if (!pristine) break;
  }
  setIntrinsicRealmCache.set(contexts, pristine);
  return pristine;
}

function objectValueEnumerationIntrinsicsArePristine(contexts: Map<string, SourceContext>): boolean {
  const cached = objectValueEnumerationIntrinsicRealmCache.get(contexts);
  if (cached !== undefined) return cached;
  let pristine = true;
  for (const context of contexts.values()) {
    const visit = (node: ts.Node): void => {
      if (!pristine) return;
      if (ts.isBinaryExpression(node) && isAssignmentOperator(node.operatorToken.kind)
        && objectIntrinsicMemberIs(node.left, context.source, new Set(["values", "entries"]))) {
        pristine = false;
        return;
      }
      if (ts.isDeleteExpression(node) && objectIntrinsicMemberIs(node.expression, context.source, new Set(["values", "entries"]))) {
        pristine = false;
        return;
      }
      if (ts.isCallExpression(node) && intrinsicMutationCallTargetsMembers(node, context.source, isObjectIntrinsicExpression, new Set(["values", "entries"]))) {
        pristine = false;
        return;
      }
      ts.forEachChild(node, visit);
    };
    visit(context.source);
    if (!pristine) break;
  }
  objectValueEnumerationIntrinsicRealmCache.set(contexts, pristine);
  return pristine;
}

function intrinsicMutationCallTargetsMembers(
  node: ts.CallExpression,
  source: ts.SourceFile,
  targetPredicate: (node: ts.Node, source: ts.SourceFile) => boolean,
  members: Set<string>,
): boolean {
  if (!ts.isPropertyAccessExpression(node.expression)
    || !ts.isIdentifier(node.expression.expression)
    || !["Object", "Reflect"].includes(node.expression.expression.text)
    || !["defineProperty", "set"].includes(node.expression.name.text)
    || resolveLexicalBinding(node.expression.expression.text, node.expression.expression, source)) return false;
  const target = node.arguments[0] && unwrapAliasExpression(node.arguments[0]);
  const member = node.arguments[1];
  return !!target
    && targetPredicate(target, source)
    && !!member
    && ts.isStringLiteralLike(member)
    && members.has(member.text);
}

function setPrototypeMemberIs(node: ts.Node, source: ts.SourceFile, members: Set<string>): boolean {
  const expression = ts.isExpression(node) ? unwrapAliasExpression(node) : node;
  if (ts.isPropertyAccessExpression(expression)) {
    return members.has(expression.name.text) && isSetPrototypeExpression(expression.expression, source);
  }
  return ts.isElementAccessExpression(expression)
    && !!expression.argumentExpression
    && ts.isStringLiteralLike(expression.argumentExpression)
    && members.has(expression.argumentExpression.text)
    && isSetPrototypeExpression(expression.expression, source);
}

function objectIntrinsicMemberIs(node: ts.Node, source: ts.SourceFile, members: Set<string>): boolean {
  const expression = ts.isExpression(node) ? unwrapAliasExpression(node) : node;
  if (ts.isPropertyAccessExpression(expression)) {
    return members.has(expression.name.text) && isObjectIntrinsicExpression(expression.expression, source);
  }
  return ts.isElementAccessExpression(expression)
    && !!expression.argumentExpression
    && ts.isStringLiteralLike(expression.argumentExpression)
    && members.has(expression.argumentExpression.text)
    && isObjectIntrinsicExpression(expression.expression, source);
}

const dangerousPrototypeCapabilities = new Set([
  "defineProperties", "defineProperty", "getPrototypeOf", "set", "setPrototypeOf"
]);

function isBoundedArithmeticDynamicEvaluation(
  node: ts.CallExpression | ts.NewExpression,
  source: ts.SourceFile
): boolean {
  if (!node.arguments || node.arguments.length !== 1) return false;
  const argument = unwrapAliasExpression(node.arguments[0]);
  let value: ts.Identifier | null = null;
  const callee = unwrapAliasExpression(node.expression);
  if (ts.isIdentifier(callee) && callee.text === "Function") {
    if (!ts.isTemplateExpression(argument) || argument.head.text !== "return "
      || argument.templateSpans.length !== 1 || argument.templateSpans[0].literal.text !== ""
      || !ts.isIdentifier(argument.templateSpans[0].expression)) return false;
    value = argument.templateSpans[0].expression;
  } else if (ts.isIdentifier(callee) && callee.text === "eval" && ts.isIdentifier(argument)) {
    value = argument;
  }
  if (!value) return false;
  const binding = resolveLexicalBinding(value.text, value, source);
  if (binding?.kind !== "variable" || !isImmutableVariable(binding.node, value)
    || !isProvenArithmeticStringBinding(binding.node, source)) return false;
  const statement = findAncestor(node, ts.isStatement);
  const block = statement?.parent;
  if (!statement || !block || !ts.isBlock(block)) return false;
  const statementIndex = block.statements.indexOf(statement);
  return block.statements.slice(0, statementIndex).some((candidate) => {
    if (!ts.isIfStatement(candidate) || candidate.elseStatement
      || !statementTerminates(candidate.thenStatement)) return false;
    const condition = unwrapAliasExpression(candidate.expression);
    if (!ts.isPrefixUnaryExpression(condition) || condition.operator !== ts.SyntaxKind.ExclamationToken) return false;
    const test = unwrapAliasExpression(condition.operand);
    if (!ts.isCallExpression(test) || test.arguments.length !== 1
      || !ts.isIdentifier(unwrapAliasExpression(test.arguments[0]))) return false;
    const tested = unwrapAliasExpression(test.arguments[0]) as ts.Identifier;
    if (resolveLexicalBinding(tested.text, tested, source)?.node !== binding.node) return false;
    const testCallee = unwrapAliasExpression(test.expression);
    if (!ts.isPropertyAccessExpression(testCallee) || testCallee.name.text !== "test") return false;
    const regex = unwrapAliasExpression(testCallee.expression);
    return ts.isRegularExpressionLiteral(regex)
      && regex.getText(source) === String.raw`/^[\d+\-*/(). ]+$/`;
  });
}

function unboundedDynamicEvaluationCapabilityReference(node: ts.Node, source: ts.SourceFile): boolean {
  if (ts.isIdentifier(node) && ["global", "globalThis", "self", "window"].includes(node.text)
    && isRuntimeIdentifierReference(node) && !resolveLexicalBinding(node.text, node, source)) {
    const parent = node.parent;
    if ((ts.isPropertyAccessExpression(parent) || ts.isElementAccessExpression(parent))
      && parent.expression === node) return false;
    if (ts.isTypeOfExpression(parent)) return false;
    if (isImmutableGlobalAliasInitializer(node, source)) return false;
    return true;
  }
  if (ts.isIdentifier(node) && isRuntimeIdentifierReference(node)
    && isGlobalObjectExpression(node, source, new Set())) {
    const parent = node.parent;
    if ((ts.isPropertyAccessExpression(parent) || ts.isElementAccessExpression(parent))
      && parent.expression === node) return false;
    if (ts.isTypeOfExpression(parent) || isImmutableGlobalAliasInitializer(node, source)) return false;
    return true;
  }
  if (ts.isIdentifier(node) && (node.text === "eval" || node.text === "Function")
    && isRuntimeIdentifierReference(node)
    && !resolveLexicalBinding(node.text, node, source)) {
    const invocation = node.parent;
    return !((ts.isCallExpression(invocation) || ts.isNewExpression(invocation))
      && unwrapAliasExpression(invocation.expression) === node
      && isBoundedArithmeticDynamicEvaluation(invocation, source));
  }
  if ((ts.isCallExpression(node) || ts.isNewExpression(node))) {
    const callee = unwrapAliasExpression(node.expression);
    if (ts.isPropertyAccessExpression(callee) && callee.name.text === "constructor"
      && (ts.isArrowFunction(unwrapAliasExpression(callee.expression))
        || ts.isFunctionExpression(unwrapAliasExpression(callee.expression)))) return true;
  }
  if (!ts.isPropertyAccessExpression(node) && !ts.isElementAccessExpression(node)) return false;
  const owner = unwrapAliasExpression(node.expression);
  if (!isGlobalObjectExpression(owner, source, new Set())) return false;
  const member = ts.isPropertyAccessExpression(node) ? node.name.text
    : node.argumentExpression && ts.isStringLiteralLike(node.argumentExpression) ? node.argumentExpression.text : "";
  return !member || member === "eval" || member === "Function";
}

function isGlobalObjectExpression(node: ts.Expression, source: ts.SourceFile, visited: Set<string>): boolean {
  const expression = unwrapAliasExpression(node);
  if (ts.isConditionalExpression(expression)) {
    return isGlobalObjectExpression(expression.whenTrue, source, new Set(visited))
      || isGlobalObjectExpression(expression.whenFalse, source, new Set(visited));
  }
  if (!ts.isIdentifier(expression)) return false;
  if (["global", "globalThis", "self", "window"].includes(expression.text)
    && !resolveLexicalBinding(expression.text, expression, source)) return true;
  const binding = resolveLexicalBinding(expression.text, expression, source);
  if (binding?.kind !== "variable" || !binding.node.initializer || !isImmutableVariable(binding.node, expression)) return false;
  const key = `${binding.node.getStart(source)}:${expression.text}`;
  if (visited.has(key)) return false;
  return isGlobalObjectExpression(binding.node.initializer, source, new Set(visited).add(key));
}

function isImmutableGlobalAliasInitializer(node: ts.Identifier, source: ts.SourceFile): boolean {
  let current: ts.Node = node;
  while (current.parent && (ts.isParenthesizedExpression(current.parent)
    || ts.isAsExpression(current.parent) || ts.isTypeAssertionExpression(current.parent)
    || ts.isNonNullExpression(current.parent) || ts.isConditionalExpression(current.parent))) {
    current = current.parent;
  }
  if (!ts.isVariableDeclaration(current.parent) || current.parent.initializer !== current
    || !ts.isVariableDeclarationList(current.parent.parent)
    || !(current.parent.parent.flags & ts.NodeFlags.Const)
    || !ts.isIdentifier(current.parent.name)) return false;
  return !mutationHandleIsExported(current.parent, current.parent.name.text, source);
}

function isRuntimeIdentifierReference(node: ts.Identifier): boolean {
  const parent = node.parent;
  // Declaration names are not runtime reads. Keep shorthand properties out of
  // this exemption because `{ eval }` reads the ambient binding.
  if ((ts.isVariableDeclaration(parent) || ts.isParameter(parent)
    || ts.isFunctionDeclaration(parent) || ts.isFunctionExpression(parent)
    || ts.isClassDeclaration(parent) || ts.isClassExpression(parent)
    || ts.isInterfaceDeclaration(parent) || ts.isTypeAliasDeclaration(parent)
    || ts.isTypeParameterDeclaration(parent) || ts.isEnumDeclaration(parent)
    || ts.isEnumMember(parent) || ts.isPropertyDeclaration(parent)
    || ts.isPropertySignature(parent) || ts.isMethodSignature(parent)
    || ts.isMethodDeclaration(parent) || ts.isGetAccessorDeclaration(parent)
    || ts.isSetAccessorDeclaration(parent) || ts.isModuleDeclaration(parent))
    && parent.name === node) return false;
  if (ts.isPropertyAccessExpression(parent) && parent.name === node) return false;
  if ((ts.isPropertyAssignment(parent) || ts.isMethodDeclaration(parent)
    || ts.isGetAccessorDeclaration(parent) || ts.isSetAccessorDeclaration(parent))
    && parent.name === node) return false;
  if (ts.isBindingElement(parent) && (parent.name === node || parent.propertyName === node)) return false;
  if ((ts.isImportSpecifier(parent) || ts.isExportSpecifier(parent))
    && (parent.name === node || parent.propertyName === node)) return false;
  return true;
}

function isProvenArithmeticStringBinding(declaration: ts.VariableDeclaration, source: ts.SourceFile): boolean {
  if (!declaration.initializer) return false;
  const initializer = unwrapAliasExpression(declaration.initializer);
  if (ts.isStringLiteralLike(initializer)) return true;
  if (!ts.isCallExpression(initializer) || initializer.arguments.length !== 0) return false;
  const trim = unwrapAliasExpression(initializer.expression);
  if (!ts.isPropertyAccessExpression(trim) || trim.name.text !== "trim") return false;
  const stringSource = unwrapAliasExpression(trim.expression);
  if (ts.isCallExpression(stringSource) && stringSource.arguments.length === 1) {
    const callee = unwrapAliasExpression(stringSource.expression);
    return ts.isIdentifier(callee) && callee.text === "String"
      && !resolveLexicalBinding("String", callee, source);
  }
  if (!ts.isCallExpression(stringSource) || stringSource.arguments.length !== 0) return false;
  const toString = unwrapAliasExpression(stringSource.expression);
  return ts.isPropertyAccessExpression(toString) && toString.name.text === "toString";
}

function dangerousPrototypeCapabilityReference(node: ts.Node, source: ts.SourceFile): boolean {
  const intrinsic = ts.isIdentifier(node) && isRuntimeIdentifierReference(node)
    ? globalIntrinsicConstructorName(node, source, new Set()) : null;
  if (ts.isIdentifier(node) && intrinsic) {
    const parent = node.parent;
    if ((ts.isCallExpression(parent) || ts.isNewExpression(parent))
      && unwrapAliasExpression(parent.expression) === node) {
      return intrinsic === "Function"
        && !(node.text === "Function" && !resolveLexicalBinding("Function", node, source));
    }
    if ((ts.isPropertyAccessExpression(parent) || ts.isElementAccessExpression(parent))
      && parent.expression === node) return false;
    if (isImmutableGlobalAliasInitializer(node, source)) return false;
    return true;
  }
  if (ts.isPropertyAccessExpression(node)) {
    if (["__proto__", "constructor"].includes(node.name.text)) return true;
    if (node.name.text === "prototype"
      && globalIntrinsicConstructorName(node.expression, source, new Set())) return true;
    if (node.name.text === "prototype" && ts.isPropertyAccessExpression(unwrapAliasExpression(node.expression))
      && (unwrapAliasExpression(node.expression) as ts.PropertyAccessExpression).name.text === "constructor") return true;
    return ts.isIdentifier(node.expression) && ["Object", "Reflect"].includes(node.expression.text)
      && dangerousPrototypeCapabilities.has(node.name.text) && !resolveLexicalBinding(node.expression.text, node.expression, source);
  }
  if (!ts.isElementAccessExpression(node) || !node.argumentExpression
    || !ts.isStringLiteralLike(node.argumentExpression)) return false;
  const owner = unwrapAliasExpression(node.expression);
  if (["__proto__", "constructor"].includes(node.argumentExpression.text)) return true;
  if (node.argumentExpression.text === "prototype"
    && globalIntrinsicConstructorName(owner, source, new Set())) return true;
  return ts.isIdentifier(owner) && ["Object", "Reflect"].includes(owner.text)
    && dangerousPrototypeCapabilities.has(node.argumentExpression.text) && !resolveLexicalBinding(owner.text, owner, source);
}

function globalIntrinsicConstructorName(
  node: ts.Expression,
  source: ts.SourceFile,
  visited: Set<string>
): "Function" | "RegExp" | "String" | "Set" | null {
  const expression = unwrapAliasExpression(node);
  if (ts.isIdentifier(expression)) {
    if (["Function", "RegExp", "String", "Set"].includes(expression.text)
      && !resolveLexicalBinding(expression.text, expression, source)) {
      return expression.text as "Function" | "RegExp" | "String" | "Set";
    }
    const binding = resolveLexicalBinding(expression.text, expression, source);
    if (binding?.kind !== "variable" || !binding.node.initializer
      || !isImmutableVariable(binding.node, expression)) return null;
    const key = `${binding.node.getStart(source)}:${expression.text}`;
    if (visited.has(key)) return null;
    return globalIntrinsicConstructorName(binding.node.initializer, source, new Set(visited).add(key));
  }
  if (!ts.isPropertyAccessExpression(expression) && !ts.isElementAccessExpression(expression)) return null;
  const owner = unwrapAliasExpression(expression.expression);
  if (!isGlobalObjectExpression(owner, source, new Set())) return null;
  const member = ts.isPropertyAccessExpression(expression) ? expression.name.text
    : expression.argumentExpression && ts.isStringLiteralLike(expression.argumentExpression)
      ? expression.argumentExpression.text : "";
  return ["Function", "RegExp", "String", "Set"].includes(member)
    ? member as "Function" | "RegExp" | "String" | "Set" : null;
}

function isSetPrototypeExpression(node: ts.Node, source: ts.SourceFile): boolean {
  const expression = ts.isExpression(node) ? unwrapAliasExpression(node) : node;
  return ts.isPropertyAccessExpression(expression)
    && ts.isIdentifier(expression.expression)
    && expression.expression.text === "Set"
    && expression.name.text === "prototype"
    && !resolveLexicalBinding("Set", expression.expression, source);
}

function isObjectIntrinsicExpression(node: ts.Node, source: ts.SourceFile): boolean {
  const expression = ts.isExpression(node) ? unwrapAliasExpression(node) : node;
  return ts.isIdentifier(expression)
    && expression.text === "Object"
    && !resolveLexicalBinding("Object", expression, source);
}

function isArrayPrototypeExpression(node: ts.Node, source: ts.SourceFile): boolean {
  const expression = ts.isExpression(node) ? unwrapAliasExpression(node) : node;
  if (ts.isPropertyAccessExpression(expression) && ts.isIdentifier(expression.expression)
    && expression.expression.text === "Array" && expression.name.text === "prototype"
    && !resolveLexicalBinding("Array", expression.expression, source)) return true;
  if (ts.isPropertyAccessExpression(expression) && expression.name.text === "prototype") {
    const constructor = unwrapAliasExpression(expression.expression);
    if (ts.isPropertyAccessExpression(constructor) && constructor.name.text === "constructor"
      && isArrayConstruction(constructor.expression, source)) return true;
  }
  if (ts.isPropertyAccessExpression(expression) && expression.name.text === "__proto__"
    && ts.isArrayLiteralExpression(unwrapAliasExpression(expression.expression))) return true;
  if (!ts.isCallExpression(expression) || expression.arguments.length !== 1
    || !isArrayConstruction(expression.arguments[0], source)) return false;
  return isGlobalGetPrototypeOf(expression.expression, source, new Set());
}

function isArrayConstruction(node: ts.Expression, source: ts.SourceFile): boolean {
  const expression = unwrapAliasExpression(node);
  if (ts.isArrayLiteralExpression(expression)) return true;
  if (!ts.isNewExpression(expression) && !ts.isCallExpression(expression)) return false;
  const callee = unwrapAliasExpression(expression.expression);
  return ts.isIdentifier(callee) && callee.text === "Array"
    && !resolveLexicalBinding("Array", callee, source);
}

function isGlobalGetPrototypeOf(
  node: ts.Expression,
  source: ts.SourceFile,
  visited: Set<ts.Node>
): boolean {
  const expression = unwrapAliasExpression(node);
  if (visited.has(expression)) return false;
  const next = new Set(visited).add(expression);
  if (ts.isPropertyAccessExpression(expression) && expression.name.text === "getPrototypeOf"
    && ts.isIdentifier(expression.expression) && ["Object", "Reflect"].includes(expression.expression.text)
    && !resolveLexicalBinding(expression.expression.text, expression.expression, source)) return true;
  if (!ts.isIdentifier(expression)) return false;
  const binding = resolveLexicalBinding(expression.text, expression, source);
  return Boolean(binding?.kind === "variable" && binding.node.initializer
    && isImmutableVariable(binding.node, expression)
    && isGlobalGetPrototypeOf(binding.node.initializer, source, next));
}

function evaluateCallableReturns(
  call: ts.CallExpression,
  target: ts.FunctionLikeDeclaration,
  caller: SelectorEvaluationContext,
  visited: Set<string>
): StaticSelectorValue[] | null {
  const targetContext = contextForSource(target.getSourceFile(), caller.contexts);
  const targetKey = `callable:${targetContext.item.relativePath}:${target.getStart(targetContext.source)}`;
  if (visited.has(targetKey) || !target.body) return null;
  const parameterValues = new Map<ts.ParameterDeclaration, StaticSelectorValue[]>();
  for (const [index, parameter] of target.parameters.entries()) {
    if (parameter.dotDotDotToken) return null;
    const argument = call.arguments[index];
    const resolved = argument && !ts.isSpreadElement(argument)
      ? evaluateSelectorValues(argument, caller, new Set(visited))
      : parameter.initializer
        ? evaluateSelectorValues(parameter.initializer, {
          filePath: targetContext.item.relativePath,
          source: targetContext.source,
          contexts: caller.contexts,
          parameterValues
        }, new Set(visited))
        : null;
    parameterValues.set(parameter, resolved ?? [{ kind: "opaque", evidence: [] }]);
  }
  const returns = callableReturnExpressions(target);
  if (returns.length === 0) return null;
  const values: StaticSelectorValue[] = [];
  const nextContext: SelectorEvaluationContext = {
    filePath: targetContext.item.relativePath,
    source: targetContext.source,
    contexts: caller.contexts,
    parameterValues
  };
  for (const expression of returns) {
    const resolved = evaluateSelectorValues(expression, nextContext, new Set(visited).add(targetKey));
    if (!resolved) return null;
    values.push(...resolved.map((value) => ({
      ...value,
      evidence: normalizeSelectorEvidence([
        selectorEvidence(call, caller.source, caller.filePath, "caller-argument"),
        ...value.evidence
      ])
    })));
  }
  return normalizeStaticValues(values);
}

function callableReturnExpressions(owner: ts.FunctionLikeDeclaration): ts.Expression[] {
  if (!owner.body) return [];
  if (!ts.isBlock(owner.body)) return [owner.body];
  const returns: ts.Expression[] = [];
  const visit = (node: ts.Node): void => {
    if (node !== owner.body && ts.isFunctionLike(node)) return;
    if (ts.isReturnStatement(node) && node.expression) {
      returns.push(node.expression);
      return;
    }
    ts.forEachChild(node, visit);
  };
  visit(owner.body);
  return returns;
}

function selectStaticProperty(values: StaticSelectorValue[], name: string): StaticSelectorValue[] | null {
  const selected: StaticSelectorValue[] = [];
  for (const value of values) {
    if (value.kind === "object") {
      if (value.openProperties) return null;
      const property = value.properties?.get(name);
      selected.push(...(property ?? [{ kind: "undefined" as const, evidence: value.evidence }]));
    } else if (value.kind === "array" && /^\d+$/u.test(name)) {
      const item = value.elements?.[Number(name)];
      if (!item) return null;
      selected.push(item);
    } else return null;
  }
  return normalizeStaticValues(selected);
}

function projectBindingValues(name: ts.BindingName, bindingName: string, values: StaticSelectorValue[]): StaticSelectorValue[] | null {
  if (ts.isIdentifier(name)) return name.text === bindingName ? normalizeStaticValues(values) : null;
  for (const [index, element] of name.elements.entries()) {
    if (ts.isOmittedExpression(element)) continue;
    if (element.dotDotDotToken || element.initializer) return null;
    if (ts.isIdentifier(element.name) && element.name.text === bindingName) {
      return ts.isArrayBindingPattern(name) ? selectStaticProperty(values, String(index))
        : selectStaticProperty(values, selectorPropertyName(element.propertyName ?? element.name) ?? "");
    }
  }
  return null;
}

function selectorBindingIsUnmutated(
  declaration: ts.VariableDeclaration,
  source: ts.SourceFile,
  evaluatedUse?: ts.Identifier
): boolean {
  if (!ts.isIdentifier(declaration.name)) return false;
  const bindingName = declaration.name.text;
  let unsafe = false;
  const root = executionRoot(declaration.name);
  // Source order only bounds execution within the same frame. A closure can
  // run after statements following its declaration have mutated captured state.
  const usePosition = evaluatedUse && executionRoot(evaluatedUse) === root
    ? evaluatedUse.getStart(source) : Number.POSITIVE_INFINITY;
  const visitNode = (node: ts.Node): void => {
    if (unsafe) return;
    if (node.getStart(source) > usePosition) return;
    if (node !== root && ts.isFunctionLike(node)) {
      if (functionBodyMutatesBinding(node, bindingName, declaration, source)) unsafe = true;
      return;
    }
    if (ts.isIdentifier(node) && node !== declaration.name && node.text === bindingName
      && resolveLexicalBinding(bindingName, node, source)?.node === declaration) {
      if (node === evaluatedUse) return;
      const parent = node.parent;
      if (selectorBindingReferenceMutatesValue(node)
        || (ts.isBinaryExpression(parent) && isAssignmentOperator(parent.operatorToken.kind) && isAncestorNode(parent.left, node))
        || ((ts.isPrefixUnaryExpression(parent) && [ts.SyntaxKind.PlusPlusToken, ts.SyntaxKind.MinusMinusToken].includes(parent.operator)
          || ts.isPostfixUnaryExpression(parent)) && parent.operand === node)) {
        unsafe = true;
        return;
      }
      if (!selectorBindingReferenceIsSafe(node)) {
        unsafe = true;
        return;
      }
    }
    ts.forEachChild(node, visitNode);
  };
  visitNode(root);
  return !unsafe;
}

function functionBodyMutatesBinding(
  owner: ts.SignatureDeclaration,
  bindingName: string,
  declaration: ts.VariableDeclaration,
  source: ts.SourceFile
): boolean {
  let mutated = false;
  const visit = (node: ts.Node): void => {
    if (mutated) return;
    if (node !== owner && ts.isFunctionLike(node)) return;
    if (ts.isIdentifier(node) && node.text === bindingName
      && resolveLexicalBinding(bindingName, node, source)?.node === declaration
      && selectorBindingReferenceMutatesValue(node)) {
      mutated = true;
      return;
    }
    ts.forEachChild(node, visit);
  };
  visit(owner);
  return mutated;
}

function selectorBindingReferenceMutatesValue(node: ts.Identifier): boolean {
  const parent = node.parent;
  if ((ts.isPropertyAccessExpression(parent) || ts.isElementAccessExpression(parent)) && parent.expression === node) {
    const use = parent.parent;
    if (ts.isBinaryExpression(use) && isAssignmentOperator(use.operatorToken.kind) && isAncestorNode(use.left, parent)) return true;
    if (ts.isDeleteExpression(use) && use.expression === parent) return true;
    if (((ts.isPrefixUnaryExpression(use) && [ts.SyntaxKind.PlusPlusToken, ts.SyntaxKind.MinusMinusToken].includes(use.operator))
      || ts.isPostfixUnaryExpression(use)) && use.operand === parent) return true;
    if (ts.isCallExpression(use) && unwrapAliasExpression(use.expression) === parent) {
      const member = ts.isPropertyAccessExpression(parent) ? parent.name.text
        : parent.argumentExpression && ts.isStringLiteralLike(parent.argumentExpression) ? parent.argumentExpression.text : "";
      return new Set(["push", "pop", "shift", "unshift", "splice", "sort", "reverse", "copyWithin", "fill", "add", "set", "delete", "clear"]).has(member);
    }
  }
  if (ts.isCallExpression(parent)) {
    const callee = unwrapAliasExpression(parent.expression);
    return ts.isPropertyAccessExpression(callee)
      && ts.isIdentifier(callee.expression)
      && callee.expression.text === "Object"
      && ["assign", "defineProperty", "defineProperties", "setPrototypeOf"].includes(callee.name.text)
      && !resolveLexicalBinding("Object", callee.expression, node.getSourceFile())
      && parent.arguments[0] === node;
  }
  return false;
}

function selectorReceiverMethodCanBeOverridden(
  receiver: ts.Expression,
  methodName: string,
  context: SelectorEvaluationContext
): boolean {
  const owner = unwrapAliasExpression(receiver);
  if (!ts.isIdentifier(owner)) return false;
  const binding = resolveLexicalBinding(owner.text, owner, context.source);
  return binding?.kind === "variable" && ts.isVariableDeclaration(binding.node)
    && !selectorBindingMemberIsUnmutated(binding.node, methodName, context.source);
}

function selectorBindingMemberIsUnmutated(
  declaration: ts.VariableDeclaration,
  memberName: string,
  source: ts.SourceFile
): boolean {
  if (!ts.isIdentifier(declaration.name)) return false;
  const bindingName = declaration.name.text;
  let mutated = false;
  const root = executionRoot(declaration.name);
  const visitNode = (node: ts.Node): void => {
    if (mutated || (node !== root && ts.isFunctionLike(node))) return;
    if (ts.isIdentifier(node) && node !== declaration.name && node.text === bindingName
      && resolveLexicalBinding(bindingName, node, source)?.node === declaration) {
      const parent = node.parent;
      if ((ts.isPropertyAccessExpression(parent) || ts.isElementAccessExpression(parent)) && parent.expression === node) {
        const member = ts.isPropertyAccessExpression(parent) ? parent.name.text
          : parent.argumentExpression && ts.isStringLiteralLike(parent.argumentExpression) ? parent.argumentExpression.text : "";
        const use = parent.parent;
        if (member === memberName
          && ((ts.isBinaryExpression(use) && isAssignmentOperator(use.operatorToken.kind) && isAncestorNode(use.left, parent))
            || (ts.isDeleteExpression(use) && use.expression === parent))) {
          mutated = true;
          return;
        }
      }
    }
    ts.forEachChild(node, visitNode);
  };
  visitNode(root);
  return !mutated;
}

function selectorBindingReferenceIsSafe(node: ts.Identifier): boolean {
  const parent = node.parent;
  if ((ts.isPropertyAccessExpression(parent) || ts.isElementAccessExpression(parent)) && parent.expression === node) return true;
  if (ts.isForOfStatement(parent) && parent.expression === node) return true;
  if (ts.isSpreadElement(parent) || ts.isSpreadAssignment(parent)) return true;
  if (ts.isElementAccessExpression(parent) && parent.argumentExpression === node) return true;
  if (ts.isPropertyAssignment(parent) && parent.initializer === node) return false;
  if (ts.isShorthandPropertyAssignment(parent) && parent.name === node) return false;
  if (ts.isReturnStatement(parent) && parent.expression === node) return false;
  if (ts.isCallExpression(parent)) return false;
  if (ts.isCallExpression(parent.parent) && parent.parent.arguments.includes(parent as ts.Expression)) {
    const callee = unwrapAliasExpression(parent.parent.expression);
    return ts.isPropertyAccessExpression(callee) && ts.isIdentifier(callee.expression)
      && callee.expression.text === "Object" && ["entries", "keys", "values"].includes(callee.name.text);
  }
  if (ts.isVariableDeclaration(parent) && parent.initializer === node) return false;
  return true;
}

function selectorStrings(values: StaticSelectorValue[]): string[] {
  const flattened = flattenStaticValues(values);
  if (flattened.length === 0 || flattened.some((value) => value.kind !== "string"
    || typeof value.value !== "string" || !value.value || !isEntityCandidate(value.value))) return [];
  return uniqueStrings(flattened.map((value) => value.value as string));
}

function flattenStaticValues(values: StaticSelectorValue[]): StaticSelectorValue[] {
  return values.flatMap((value) => value.kind === "array" ? flattenStaticValues(value.elements ?? []) : [value]);
}

function mergeStaticValues(left: StaticSelectorValue[] | null, right: StaticSelectorValue[] | null): StaticSelectorValue[] | null {
  return left && right ? normalizeStaticValues([...left, ...right]) : null;
}

function normalizeStaticValues(values: StaticSelectorValue[]): StaticSelectorValue[] {
  const byKey = new Map<string, StaticSelectorValue>();
  for (const value of values) {
    const key = staticValueKey(value);
    const existing = byKey.get(key);
    byKey.set(key, existing ? {
      ...existing,
      evidence: normalizeSelectorEvidence([...existing.evidence, ...value.evidence])
    } : value);
  }
  return [...byKey.values()].sort((left, right) => staticValueKey(left).localeCompare(staticValueKey(right)));
}

function staticValueKey(value: StaticSelectorValue): string {
  if (value.kind === "string") return `string:${value.value}`;
  if (value.kind === "boolean") return `boolean:${value.value}`;
  if (value.kind === "undefined") return "undefined";
  if (value.kind === "opaque") return "opaque";
  if (value.kind === "array") return `array:${JSON.stringify((value.elements ?? []).map(staticValueKey))}`;
  return `object:${value.openProperties ? "open:" : "closed:"}${JSON.stringify([...(value.properties ?? new Map())].map(([name, values]) => [name, values.map(staticValueKey)]).sort())}`;
}

function selectorEvidence(node: ts.Node, source: ts.SourceFile, filePath: string, derivation: EntitySelectorEvidence["derivation"]): EntitySelectorEvidence {
  const startLine = source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1;
  const endLine = source.getLineAndCharacterOfPosition(node.getEnd()).line + 1;
  return { filePath, sourceFileSha256: hash(source.getFullText(), 64), startLine, endLine,
    snippetSha256: hash(node.getText(source), 64), derivation };
}

function normalizeSelectorEvidence(evidence: EntitySelectorEvidence[]): EntitySelectorEvidence[] {
  const byKey = new Map(evidence.map((item) => [JSON.stringify(item), item]));
  return [...byKey.values()].sort((left, right) => left.filePath.localeCompare(right.filePath)
    || left.sourceFileSha256.localeCompare(right.sourceFileSha256)
    || left.startLine - right.startLine || left.endLine - right.endLine
    || left.derivation.localeCompare(right.derivation) || left.snippetSha256.localeCompare(right.snippetSha256));
}

function selectorPropertyName(name: ts.PropertyName): string | null {
  return ts.isIdentifier(name) || ts.isStringLiteralLike(name) || ts.isNumericLiteral(name) ? name.text : null;
}

function isEntityCandidate(value: string): boolean {
  return /^[A-Z][A-Za-z0-9_]*$/u.test(value);
}

function isAncestorNode(ancestor: ts.Node, node: ts.Node): boolean {
  for (let current: ts.Node | undefined = node; current; current = current.parent) if (current === ancestor) return true;
  return false;
}

function ensureSelectorEvidenceAuthorityFacts(
  manifest: ScanManifest,
  facts: CodeFact[],
  contexts: Map<string, SourceContext>
): void {
  const authorized = new Set(facts
    .filter((candidate) => typeof candidate.properties.sourceFileSha256 === "string")
    .map((candidate) => `${candidate.evidence.filePath}\0${candidate.properties.sourceFileSha256}`));
  const required = new Map<string, { filePath: string; sourceFileSha256: string }>();
  for (const candidate of facts) {
    const selectorJson = candidate.properties.entitySelectorJson;
    if (!selectorJson) continue;
    try {
      const selector = JSON.parse(selectorJson) as { evidence?: Array<{ filePath?: unknown; sourceFileSha256?: unknown }> };
      for (const item of selector.evidence ?? []) {
        if (typeof item.filePath !== "string" || typeof item.sourceFileSha256 !== "string") continue;
        const key = `${item.filePath}\0${item.sourceFileSha256}`;
        if (!authorized.has(key)) required.set(key, { filePath: item.filePath, sourceFileSha256: item.sourceFileSha256 });
      }
    } catch {
      continue;
    }
  }
  for (const authority of [...required.values()].sort((left, right) =>
    left.filePath.localeCompare(right.filePath) || left.sourceFileSha256.localeCompare(right.sourceFileSha256))) {
    const context = contexts.get(authority.filePath);
    if (!context) continue;
    const text = context.source.getFullText();
    if (hash(text, 64) !== authority.sourceFileSha256) continue;
    facts.push(fact(manifest, FactTypes.Base44SourceAuthority, RuleIds.Base44SourceAuthority,
      context.source, context.source, authority.filePath, authority.filePath, {
        authorityKind: "selector-evidence-source",
        sourceFileSha256: authority.sourceFileSha256
      }, EvidenceTiers.Tier2Structural));
    authorized.add(`${authority.filePath}\0${authority.sourceFileSha256}`);
  }
}

function sdkRootIndex(chain: string[], root: string): number {
  if (chain[0] === root) return 0;
  return chain[0] === "asServiceRole" && chain[1] === root ? 1 : -1;
}

async function sqlFact(manifest: ScanManifest, item: FileInventoryItem): Promise<CodeFact> {
  const text = await fs.readFile(item.absolutePath, "utf8");
  const uncommented = text.replace(/--.*$/gm, "").replace(/\/\*[\s\S]*?\*\//g, "");
  const kinds = [...new Set([...uncommented.matchAll(/\b(create|alter|drop)\s+(table|index|policy|function|trigger)\b/gi)].map((match) => `${match[1].toLowerCase()}-${match[2].toLowerCase()}`))].sort();
  return createFact(manifest, FactTypes.Base44MigrationSurface, RuleIds.Base44MigrationSurface, EvidenceTiers.Tier3SyntaxOrTextual,
    createEvidence(item.relativePath, 1, Math.max(1, text.split(/\r?\n/).length), "base44-evidence", ScannerVersions.Base44EvidenceExtractor, hash(text, 64)), {
      targetSymbol: item.relativePath,
      properties: { sourceFileSha256: hash(text, 64), statementKinds: kinds.join(";"), statementKindCount: String(kinds.length) }
    });
}

function fact(manifest: ScanManifest, factType: string, ruleId: string, node: ts.Node, source: ts.SourceFile, filePath: string, target: string, properties: Record<string, string>, tier: string = EvidenceTiers.Tier3SyntaxOrTextual): CodeFact {
  const start = source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1;
  const end = source.getLineAndCharacterOfPosition(node.getEnd()).line + 1;
  return createFact(manifest, factType, ruleId, tier, createEvidence(filePath, start, end, "base44-evidence", ScannerVersions.Base44EvidenceExtractor, hash(node.getText(source), 64)), {
    targetSymbol: target,
    contractElement: target,
    properties
  });
}

function expressionChain(expression: ts.Expression): string[] | null {
  if (ts.isIdentifier(expression)) return [expression.text];
  if (ts.isPropertyAccessExpression(expression)) {
    const parent = expressionChain(expression.expression);
    return parent ? [...parent, expression.name.text] : null;
  }
  if (ts.isElementAccessExpression(expression) && ts.isStringLiteral(expression.argumentExpression)) {
    const parent = expressionChain(expression.expression);
    return parent ? [...parent, expression.argumentExpression.text] : null;
  }
  return null;
}

function stringArgument(node: ts.Expression | undefined): string | null {
  return node && (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) ? node.text : null;
}

function isBase44SdkPackageImport(value: string): boolean {
  const normalized = value.startsWith("npm:") ? value.slice(4) : value;
  return normalized === "@base44/sdk"
    || normalized.startsWith("@base44/sdk@")
    || normalized.startsWith("@base44/sdk/");
}

function isBase44SdkClientImport(value: string): boolean {
  const normalized = value.startsWith("npm:") ? value.slice(4) : value;
  return normalized === "@base44/sdk" || normalized.startsWith("@base44/sdk@");
}

function packageVersion(value: string): string {
  const normalized = value.startsWith("npm:") ? value.slice(4) : value;
  return normalized.startsWith("@base44/sdk@") ? normalized.slice("@base44/sdk@".length) : "package-manifest-resolved";
}

function isFunctionEntry(filePath: string): boolean {
  return /(^|\/)base44\/functions\/[^/]+\/(entry|index)\.[jt]sx?$/.test(filePath);
}

function isMigrationPath(filePath: string): boolean {
  return /(^|\/)(migrations?|db\/migrations?|database\/migrations?|supabase\/migrations?)(\/|$)/i.test(filePath);
}

function functionName(filePath: string): string {
  const match = filePath.match(/base44\/functions\/([^/]+)\//);
  return match?.[1] ?? path.basename(path.dirname(filePath));
}

function safeOrigin(value: string): string | null {
  try {
    const url = new URL(value);
    return `${url.protocol}//${url.host}`;
  } catch {
    return null;
  }
}

function scriptKind(filePath: string): ts.ScriptKind {
  if (filePath.endsWith(".tsx")) return ts.ScriptKind.TSX;
  if (filePath.endsWith(".jsx")) return ts.ScriptKind.JSX;
  if (filePath.endsWith(".js")) return ts.ScriptKind.JS;
  return ts.ScriptKind.TS;
}

interface SourceContext {
  item: FileInventoryItem;
  source: ts.SourceFile;
  aliases: Map<string, string[]>;
  factoryAliases: Set<string>;
}

interface LocalModuleAlias {
  pattern: string;
  targets: string[];
}

interface LocalModuleResolutionAuthority {
  aliases: LocalModuleAlias[];
  baseUrls: string[];
  closed: boolean;
  declaredPackages: Set<string>;
}

const localModuleAliasCache = new WeakMap<Map<string, SourceContext>, LocalModuleResolutionAuthority>();

function localModuleAliases(contexts: Map<string, SourceContext>): LocalModuleAlias[] {
  return localModuleAliasCache.get(contexts)?.aliases ?? [];
}

async function loadLocalModuleAliases(items: readonly FileInventoryItem[]): Promise<LocalModuleResolutionAuthority> {
  const first = items[0];
  if (!first) return { aliases: [], baseUrls: [], closed: true, declaredPackages: new Set() };
  let repositoryRoot = path.dirname(first.absolutePath);
  for (let index = 1; index < first.relativePath.split("/").length; index++) {
    repositoryRoot = path.dirname(repositoryRoot);
  }
  let declaredPackages = new Set<string>();
  let packageAliases: LocalModuleAlias[] = [];
  let packageAuthorityClosed = true;
  try {
    const packageJson = JSON.parse(await fs.readFile(path.join(repositoryRoot, "package.json"), "utf8"));
    for (const key of ["dependencies", "devDependencies", "peerDependencies", "optionalDependencies"]) {
      const dependencies = packageJson?.[key];
      if (dependencies === undefined) continue;
      if (!dependencies || typeof dependencies !== "object" || Array.isArray(dependencies)) {
        packageAuthorityClosed = false;
        continue;
      }
      for (const name of Object.keys(dependencies)) declaredPackages.add(name);
    }
    if (packageJson.imports !== undefined) {
      if (!packageJson.imports || typeof packageJson.imports !== "object" || Array.isArray(packageJson.imports)) {
        packageAuthorityClosed = false;
      } else {
        for (const [pattern, target] of Object.entries(packageJson.imports)) {
          if (!pattern.startsWith("#") || (pattern.match(/\*/gu)?.length ?? 0) > 1
            || typeof target !== "string" || !target.startsWith("./")
            || (target.match(/\*/gu)?.length ?? 0) > 1) {
            packageAuthorityClosed = false;
            continue;
          }
          packageAliases.push({ pattern, targets: [path.posix.normalize(target).replace(/^\.\//u, "")] });
        }
      }
    }
  } catch (error: any) {
    if (error?.code !== "ENOENT") packageAuthorityClosed = false;
  }
  for (const configName of ["tsconfig.json", "jsconfig.json"]) {
    const configPath = path.join(repositoryRoot, configName);
    try {
      await fs.access(configPath);
    } catch (error: any) {
      if (error?.code === "ENOENT") continue;
      return { aliases: packageAliases, baseUrls: [], closed: false, declaredPackages };
    }
    const loaded = await loadLocalModuleConfig(configPath, repositoryRoot, new Set());
    return {
      aliases: [...packageAliases, ...loaded.aliases].sort((left, right) => left.pattern.localeCompare(right.pattern)),
      baseUrls: loaded.baseUrls,
      closed: loaded.closed && packageAuthorityClosed,
      declaredPackages
    };
  }
  return { aliases: packageAliases, baseUrls: [], closed: packageAuthorityClosed, declaredPackages };
}

async function loadLocalModuleConfig(
  configPath: string,
  repositoryRoot: string,
  visited: Set<string>
): Promise<{aliases: LocalModuleAlias[]; baseUrls: string[]; closed: boolean}> {
  const canonicalPath = path.resolve(configPath);
  if (visited.has(canonicalPath) || !canonicalPath.startsWith(`${path.resolve(repositoryRoot)}${path.sep}`)) {
    return { aliases: [], baseUrls: [], closed: false };
  }
  const nextVisited = new Set(visited).add(canonicalPath);
  let text: string;
  try {
    text = await fs.readFile(canonicalPath, "utf8");
  } catch {
    return { aliases: [], baseUrls: [], closed: false };
  }
  const parsed = ts.parseConfigFileTextToJson(canonicalPath, text);
  if (parsed.error || !parsed.config || typeof parsed.config !== "object" || Array.isArray(parsed.config)) {
    return { aliases: [], baseUrls: [], closed: false };
  }
  let inherited: {aliases: LocalModuleAlias[]; baseUrls: string[]; closed: boolean} = {
    aliases: [], baseUrls: [], closed: true
  };
  if (parsed.config.extends !== undefined) {
    if (typeof parsed.config.extends !== "string" || !parsed.config.extends.startsWith(".")) {
      inherited = { aliases: [], baseUrls: [], closed: false };
    } else {
      let extendedPath = path.resolve(path.dirname(canonicalPath), parsed.config.extends);
      if (!path.extname(extendedPath)) extendedPath += ".json";
      inherited = await loadLocalModuleConfig(extendedPath, repositoryRoot, nextVisited);
    }
  }
  const options = parsed.config.compilerOptions;
  if (options !== undefined && (!options || typeof options !== "object" || Array.isArray(options))) {
    return { aliases: inherited.aliases, baseUrls: inherited.baseUrls, closed: false };
  }
  let baseUrls = inherited.baseUrls;
  let baseDirectory = path.dirname(canonicalPath);
  if (options && typeof options.baseUrl === "string") {
    baseDirectory = path.resolve(path.dirname(canonicalPath), options.baseUrl);
    const relativeBase = path.relative(repositoryRoot, baseDirectory).split(path.sep).join("/") || ".";
    if (relativeBase.startsWith("../") || path.posix.isAbsolute(relativeBase)) {
      return { aliases: inherited.aliases, baseUrls: inherited.baseUrls, closed: false };
    }
    baseUrls = [relativeBase];
  }
  if (!options || options.paths === undefined) return { ...inherited, baseUrls };
  if (!options.paths || typeof options.paths !== "object" || Array.isArray(options.paths)
    || (options.baseUrl !== undefined && typeof options.baseUrl !== "string")) {
    return { aliases: inherited.aliases, baseUrls, closed: false };
  }
  const relativeBaseDirectory = path.relative(repositoryRoot, baseDirectory)
    .split(path.sep).join("/") || ".";
  const aliases: LocalModuleAlias[] = [];
  let closed = inherited.closed;
  for (const [pattern, rawTargets] of Object.entries(options.paths)) {
    if (!pattern || (pattern.match(/\*/gu)?.length ?? 0) > 1
      || !Array.isArray(rawTargets) || rawTargets.length !== 1
      || typeof rawTargets[0] !== "string" || !rawTargets[0]
      || (rawTargets[0].match(/\*/gu)?.length ?? 0) > 1) {
      closed = false;
      continue;
    }
    aliases.push({
      pattern,
      targets: [path.posix.normalize(path.posix.join(relativeBaseDirectory, rawTargets[0])).replace(/^\.\//u, "")]
    });
  }
  return { aliases: aliases.sort((left, right) => left.pattern.localeCompare(right.pattern)), baseUrls, closed };
}

interface AliasDiscovery {
  contexts: Map<string, SourceContext>;
  aliasesByFile: Map<string, Map<string, string[]>>;
  factoryAliasesByFile: Map<string, Set<string>>;
  injectedParametersByFile: Map<string, Map<number, string[]>>;
  sdkAuthorityRootsByFile: Map<string, SdkAuthorityRoot[]>;
}

interface SdkAuthorityRoot {
  authorityPath: string;
  authoritySha256: string;
  rawSpecifier: string;
}

interface SdkIdentityEvidence {
  authorityPath: string;
  authoritySha256: string;
  kind: "source-import" | "package-manifest" | "package-lock-resolution";
}

interface SdkIdentity {
  schemaVersion: "88mph.base44-sdk-callsite-identity.v1";
  packageName: "@base44/sdk";
  version: "0.8.4" | "0.8.5";
  scope: "function-runtime" | "frontend-package";
  rawSpecifier: string;
  evidence: SdkIdentityEvidence[];
}

interface SdkIdentityResolution {
  identity?: SdkIdentity;
  gap?: string;
}

interface FrontendPackageAuthority {
  identityEvidence?: SdkIdentityEvidence[];
  version?: string;
  gap?: string;
}

interface ParameterSource {
  kind: "known" | "parameter" | "unknown";
  prefix?: string[];
  parameterKey?: string;
  suffix?: string[];
}

interface ParameterTarget {
  context: SourceContext;
  parameter: ts.ParameterDeclaration;
}

async function buildAliasMaps(items: readonly FileInventoryItem[]): Promise<AliasDiscovery> {
  const contexts = new Map<string, SourceContext>();
  for (const item of items) {
    const text = await fs.readFile(item.absolutePath, "utf8");
    const source = ts.createSourceFile(item.absolutePath, text, ts.ScriptTarget.Latest, true, scriptKind(item.relativePath));
    const context: SourceContext = { item, source, aliases: new Map(), factoryAliases: new Set() };
    seedDirectSdkImports(context);
    contexts.set(item.relativePath, context);
  }
  localModuleAliasCache.set(contexts, await loadLocalModuleAliases(items));

  const exportsByFile = new Map<string, Map<string, string[]>>();
  for (let pass = 0; pass < contexts.size + 2; pass++) {
    let changed = false;
    for (const context of contexts.values()) {
      changed = propagateLocalImports(context, contexts, exportsByFile) || changed;
      changed = updateExportedAliases(context, exportsByFile) || changed;
    }
    if (!changed) break;
  }
  return {
    contexts,
    aliasesByFile: new Map([...contexts].map(([filePath, context]) => [filePath, context.aliases])),
    factoryAliasesByFile: new Map([...contexts].map(([filePath, context]) => [filePath, context.factoryAliases])),
    injectedParametersByFile: discoverInjectedParameterAliases(contexts),
    sdkAuthorityRootsByFile: discoverSdkAuthorityRoots(contexts)
  };
}

function discoverSdkAuthorityRoots(contexts: Map<string, SourceContext>): Map<string, SdkAuthorityRoot[]> {
  const direct = new Map<string, SdkAuthorityRoot[]>();
  const imports = new Map<string, string[]>();
  const importers = new Map<string, string[]>();
  for (const [filePath, context] of contexts) {
    const roots: SdkAuthorityRoot[] = [];
    const targets: string[] = [];
    for (const statement of context.source.statements) {
      if (!ts.isImportDeclaration(statement) || !ts.isStringLiteral(statement.moduleSpecifier)
        || statement.importClause?.isTypeOnly) continue;
      const rawSpecifier = statement.moduleSpecifier.text;
      if (isBase44SdkClientImport(rawSpecifier)) {
        roots.push({
          authorityPath: filePath,
          authoritySha256: hash(context.source.getFullText(), 64),
          rawSpecifier
        });
      }
      const target = resolveLocalModule(filePath, rawSpecifier, contexts);
      if (target) {
        targets.push(target);
        importers.set(target, [...(importers.get(target) ?? []), filePath]);
      }
    }
    for (const target of dynamicLocalImportTargets(context, contexts)) {
      targets.push(target);
      importers.set(target, [...(importers.get(target) ?? []), filePath]);
    }
    direct.set(filePath, normalizeSdkRoots(roots));
    imports.set(filePath, uniqueStrings(targets));
  }

  const forward = new Map([...direct].map(([filePath, roots]) => [filePath, [...roots]]));
  for (let pass = 0; pass < contexts.size + 1; pass++) {
    let changed = false;
    for (const filePath of contexts.keys()) {
      const roots = normalizeSdkRoots([
        ...(forward.get(filePath) ?? []),
        ...(imports.get(filePath) ?? []).flatMap((target) => forward.get(target) ?? [])
      ]);
      if (JSON.stringify(roots) !== JSON.stringify(forward.get(filePath) ?? [])) {
        forward.set(filePath, roots);
        changed = true;
      }
    }
    if (!changed) break;
  }

  const result = new Map<string, SdkAuthorityRoot[]>();
  for (const filePath of contexts.keys()) {
    const rooted = forward.get(filePath) ?? [];
    if (rooted.length > 0) {
      result.set(filePath, rooted);
      continue;
    }
    // A callsite-proven injected client may flow into an otherwise SDK-free
    // helper. Conservatively collect every importing source root; ambiguity is
    // retained for the per-operation resolver rather than selecting by path.
    const visited = new Set<string>([filePath]);
    const queue = [...(importers.get(filePath) ?? [])];
    const roots: SdkAuthorityRoot[] = [];
    while (queue.length) {
      const importer = queue.shift()!;
      if (visited.has(importer)) continue;
      visited.add(importer);
      roots.push(...(forward.get(importer) ?? []));
      queue.push(...(importers.get(importer) ?? []));
    }
    result.set(filePath, normalizeSdkRoots(roots));
  }
  return result;
}

function normalizeSdkRoots(roots: SdkAuthorityRoot[]): SdkAuthorityRoot[] {
  const byKey = new Map(roots.map((root) => [JSON.stringify(root), root]));
  return [...byKey.values()].sort((left, right) => left.authorityPath.localeCompare(right.authorityPath)
    || left.rawSpecifier.localeCompare(right.rawSpecifier)
    || left.authoritySha256.localeCompare(right.authoritySha256));
}

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values)].sort((left, right) => left.localeCompare(right));
}

async function loadFrontendPackageAuthority(inventory: readonly FileInventoryItem[]): Promise<FrontendPackageAuthority> {
  const manifest = inventory.find((item) => !item.skipped && item.relativePath === "package.json");
  const lock = inventory.find((item) => !item.skipped && item.relativePath === "package-lock.json");
  if (!manifest || !lock) return { gap: "sdk-identity-package-authority-missing" };
  try {
    const [manifestText, lockText] = await Promise.all([
      fs.readFile(manifest.absolutePath, "utf8"),
      fs.readFile(lock.absolutePath, "utf8")
    ]);
    const manifestJson = JSON.parse(manifestText) as Record<string, any>;
    const lockJson = JSON.parse(lockText) as Record<string, any>;
    const requested = manifestJson.dependencies?.["@base44/sdk"] ?? manifestJson.devDependencies?.["@base44/sdk"];
    const lockedRequested = lockJson.packages?.[""]?.dependencies?.["@base44/sdk"]
      ?? lockJson.packages?.[""]?.devDependencies?.["@base44/sdk"];
    const version = lockJson.packages?.["node_modules/@base44/sdk"]?.version;
    if (typeof requested !== "string" || requested !== lockedRequested || typeof version !== "string") {
      return { gap: "sdk-identity-package-authority-invalid" };
    }
    return {
      version,
      identityEvidence: [
        { authorityPath: manifest.relativePath, authoritySha256: hash(manifestText, 64), kind: "package-manifest" },
        { authorityPath: lock.relativePath, authoritySha256: hash(lockText, 64), kind: "package-lock-resolution" }
      ]
    };
  } catch {
    return { gap: "sdk-identity-package-authority-invalid" };
  }
}

function resolveSdkIdentity(roots: SdkAuthorityRoot[], frontend: FrontendPackageAuthority): SdkIdentityResolution {
  if (roots.length === 0) return { gap: "sdk-identity-source-root-missing" };
  const identities = roots.map((root): SdkIdentityResolution => {
    const sourceEvidence: SdkIdentityEvidence = {
      authorityPath: root.authorityPath,
      authoritySha256: root.authoritySha256,
      kind: "source-import"
    };
    if (root.rawSpecifier === "npm:@base44/sdk@0.8.4") {
      return { identity: sdkIdentity("0.8.4", "function-runtime", root.rawSpecifier, [sourceEvidence]) };
    }
    if (root.rawSpecifier === "@base44/sdk") {
      if (frontend.gap || !frontend.identityEvidence || !frontend.version) return { gap: frontend.gap ?? "sdk-identity-package-authority-invalid" };
      if (frontend.version !== "0.8.5") return { gap: "sdk-identity-version-unsupported" };
      return { identity: sdkIdentity("0.8.5", "frontend-package", root.rawSpecifier, [sourceEvidence, ...frontend.identityEvidence]) };
    }
    return { gap: "sdk-identity-specifier-unsupported" };
  });
  const gaps = uniqueStrings(identities.flatMap((candidate) => candidate.gap ? [candidate.gap] : []));
  const values = new Map(identities.flatMap((candidate) => candidate.identity
    ? [[JSON.stringify(candidate.identity), candidate.identity] as const] : []));
  if (gaps.length > 0) return { gap: gaps.length === 1 ? gaps[0] : "sdk-identity-source-root-ambiguous" };
  if (values.size !== 1) return { gap: "sdk-identity-source-root-ambiguous" };
  return { identity: [...values.values()][0] };
}

function sdkIdentity(
  version: "0.8.4" | "0.8.5",
  scope: "function-runtime" | "frontend-package",
  rawSpecifier: string,
  evidence: SdkIdentityEvidence[]
): SdkIdentity {
  return {
    schemaVersion: "88mph.base44-sdk-callsite-identity.v1",
    packageName: "@base44/sdk",
    version,
    scope,
    rawSpecifier,
    evidence: [...evidence].sort((left, right) => left.kind.localeCompare(right.kind)
      || left.authorityPath.localeCompare(right.authorityPath)
      || left.authoritySha256.localeCompare(right.authoritySha256))
  };
}

/**
 * Propagate an SDK alias into a local helper parameter only when every statically
 * discovered direct callsite supplies the same proven SDK-derived expression.
 * Names, JSDoc, and parameter annotations never create authority.
 */
function discoverInjectedParameterAliases(contexts: Map<string, SourceContext>): Map<string, Map<number, string[]>> {
  const inputs = new Map<string, { target: ParameterTarget; sources: ParameterSource[] }>();
  for (const caller of contexts.values()) {
    const visitCall = (node: ts.Node): void => {
      if (ts.isCallExpression(node)) {
        const target = resolveCallableTarget(node.expression, caller, contexts);
        if (target) {
          for (let index = 0; index < target.parameters.length; index++) {
            const parameter = target.parameters[index];
            if (!ts.isIdentifier(parameter.name)) continue;
            const key = parameterKey(target.getSourceFile(), parameter);
            const record = inputs.get(key) ?? { target: { context: contextForSource(target.getSourceFile(), contexts), parameter }, sources: [] };
            record.sources.push(parameter.dotDotDotToken || node.arguments.slice(0, index + 1).some(ts.isSpreadElement)
              ? { kind: "unknown" }
              : argumentAliasSource(node.arguments[index], node, caller));
            inputs.set(key, record);
          }
        }
      }
      ts.forEachChild(node, visitCall);
    };
    visitCall(caller.source);
  }

  const candidates = new Map<string, Map<string, string[]>>();
  for (const [key, record] of inputs) {
    const values = new Map<string, string[]>();
    for (const source of record.sources) {
      if (source.kind === "known" && source.prefix) values.set(JSON.stringify(source.prefix), source.prefix);
    }
    candidates.set(key, values);
  }
  for (let pass = 0; pass < inputs.size + 1; pass++) {
    let changed = false;
    for (const [key, record] of inputs) {
      const values = candidates.get(key)!;
      for (const source of record.sources) {
        if (source.kind !== "parameter" || !source.parameterKey) continue;
        for (const value of [...(candidates.get(source.parameterKey)?.values() ?? [])]) {
          const candidate = [...value, ...(source.suffix ?? [])];
          const identity = JSON.stringify(candidate);
          if (!values.has(identity) && values.size < 2) { values.set(identity, candidate); changed = true; }
        }
      }
    }
    if (!changed) break;
  }

  const resolved = new Map<string, string[]>();
  for (const [key, record] of inputs) {
    const values = candidates.get(key)!;
    const dependenciesResolved = record.sources.every((source) => source.kind === "known"
      || source.kind === "parameter" && Boolean(source.parameterKey && candidates.get(source.parameterKey)?.size));
    if (record.sources.length && dependenciesResolved && values.size === 1) resolved.set(key, [...values.values()][0]);
  }

  // A candidate path is not proof: unknown or ambiguous inputs must invalidate
  // every downstream dependent, including otherwise consistently seeded cycles.
  for (let pass = 0; pass < inputs.size; pass++) {
    let changed = false;
    for (const key of resolved.keys()) {
      if (inputs.get(key)!.sources.some((source) => source.kind === "parameter"
        && (!source.parameterKey || !resolved.has(source.parameterKey)))) {
        resolved.delete(key);
        changed = true;
      }
    }
    if (!changed) break;
  }

  const byFile = new Map<string, Map<number, string[]>>();
  for (const [key, prefix] of resolved) {
    const target = inputs.get(key)!.target;
    const file = target.context.item.relativePath;
    const parameters = byFile.get(file) ?? new Map<number, string[]>();
    parameters.set(target.parameter.getStart(target.context.source), prefix);
    byFile.set(file, parameters);
  }
  return byFile;
}

function contextForSource(source: ts.SourceFile, contexts: Map<string, SourceContext>): SourceContext {
  const context = [...contexts.values()].find((candidate) => candidate.source === source);
  if (!context) throw new Error(`Base44 alias analysis lost source context for ${source.fileName}`);
  return context;
}

function parameterKey(source: ts.SourceFile, parameter: ts.ParameterDeclaration): string {
  return `${source.fileName}:${parameter.getStart(source)}`;
}

function resolveParameterSource(source: ParameterSource, resolved: Map<string, string[]>): string[] | null {
  if (source.kind === "unknown") return null;
  if (source.kind === "known") return source.prefix ?? [];
  const prefix = source.parameterKey ? resolved.get(source.parameterKey) : undefined;
  return prefix ? [...prefix, ...(source.suffix ?? [])] : null;
}

function argumentAliasSource(argument: ts.Expression | undefined, call: ts.CallExpression, caller: SourceContext): ParameterSource {
  if (!argument) return { kind: "unknown" };
  return parameterSourceFromExpression(argument, call, caller, new Set());
}

function unwrapAliasExpression(input: ts.Expression): ts.Expression {
  let node = input;
  while (ts.isParenthesizedExpression(node) || ts.isAsExpression(node) || ts.isTypeAssertionExpression(node)
    || ts.isNonNullExpression(node) || ts.isSatisfiesExpression(node)) node = node.expression;
  return node;
}

function resolveCallableTarget(expression: ts.Expression, caller: SourceContext, contexts: Map<string, SourceContext>): ts.FunctionLikeDeclaration | null {
  const cache = callableTargetCache.get(contexts) ?? new WeakMap<ts.Expression, ts.FunctionLikeDeclaration | null>();
  callableTargetCache.set(contexts, cache);
  const cached = cache.get(expression);
  if (cached !== undefined) return cached;
  const resolved = resolveCallableExpression(expression, caller, contexts, new Set());
  cache.set(expression, resolved);
  return resolved;
}

const callableTargetCache = new WeakMap<
  Map<string, SourceContext>,
  WeakMap<ts.Expression, ts.FunctionLikeDeclaration | null>
>();

function resolveCallableExpression(
  expression: ts.Expression,
  caller: SourceContext,
  contexts: Map<string, SourceContext>,
  visited: Set<ts.Node>,
  allowDeferredCapture = false
): ts.FunctionLikeDeclaration | null {
  expression = unwrapAliasExpression(expression);
  if (visited.has(expression)) return null;
  const next = new Set(visited).add(expression);
  if (ts.isCallExpression(expression)) {
    if (isExactImportedCall(expression, caller.source, "react", new Set(["useRef", "useCallback"]))) {
      const argument = expression.arguments[0];
      if (!argument || ts.isSpreadElement(argument)) return null;
      return ts.isArrowFunction(argument) || ts.isFunctionExpression(argument)
        ? argument : resolveCallableExpression(argument, caller, contexts, next, allowDeferredCapture);
    }
    if (isExactImportedCall(expression, caller.source, "lodash", new Set(["debounce"]))) {
      const argument = expression.arguments[0];
      if (!argument || ts.isSpreadElement(argument)) return null;
      const direct = resolveCallableExpression(argument, caller, contexts, next, allowDeferredCapture);
      if (direct && !ts.isArrowFunction(argument) && !ts.isFunctionExpression(argument)) return direct;
      return ts.isArrowFunction(argument) || ts.isFunctionExpression(argument)
        ? resolveForwardedCallable(argument, caller, contexts, next) : null;
    }
  }
  if (ts.isPropertyAccessExpression(expression) && expression.name.text === "current") {
    const owner = unwrapAliasExpression(expression.expression);
    if (!ts.isIdentifier(owner)) return null;
    const lexical = resolveLexicalBinding(owner.text, owner, caller.source);
    const binding = lexical?.kind === "variable" ? lexical
      : allowDeferredCapture ? resolveDeferredCapturedVariable(owner.text, owner, caller.source) : null;
    if (binding?.kind !== "variable" || !binding.node.initializer) {
      return null;
    }
    const initializer = unwrapAliasExpression(binding.node.initializer);
    if (!ts.isCallExpression(initializer)
      || !isExactImportedCall(initializer, caller.source, "react", new Set(["useRef"]))) {
      return null;
    }
    const initialArgument = initializer.arguments[0];
    if (!initialArgument || ts.isSpreadElement(initialArgument)) return null;
    const targets: ts.FunctionLikeDeclaration[] = [];
    const initial = resolveCallableExpression(initialArgument, caller, contexts, next, allowDeferredCapture);
    if (!initial) return null;
    targets.push(initial);
    let unsafeWrite = false;
    const inspectWrites = (node: ts.Node): void => {
      if (unsafeWrite || !ts.isBinaryExpression(node)) return ts.forEachChild(node, inspectWrites);
      const left = unwrapAliasExpression(node.left);
      if (!ts.isPropertyAccessExpression(left) || left.name.text !== "current") return ts.forEachChild(node, inspectWrites);
      const root = unwrapAliasExpression(left.expression);
      if (!ts.isIdentifier(root) || root.text !== owner.text
        || resolveLexicalBinding(root.text, root, caller.source)?.node !== binding.node) return ts.forEachChild(node, inspectWrites);
      if (node.operatorToken.kind !== ts.SyntaxKind.EqualsToken) {
        unsafeWrite = true;
        return;
      }
      const target = resolveCallableExpression(node.right, caller, contexts, next, allowDeferredCapture);
      if (!target) {
        unsafeWrite = true;
        return;
      }
      targets.push(target);
    };
    inspectWrites(caller.source);
    return !unsafeWrite && targets.every((target) => target === initial) ? initial : null;
  }
  if (!ts.isIdentifier(expression)) return null;
  const binding = resolveLexicalBinding(expression.text, expression, caller.source);
  if (!binding) return null;
  if (binding.kind === "function") {
    return binding.node.body && !bindingWrittenBefore(binding.node.name!, expression) ? binding.node : null;
  }
  if (binding.kind === "variable") {
    if (!isImmutableVariable(binding.node, expression) || !binding.node.initializer) {
      return null;
    }
    const initializer = unwrapAliasExpression(binding.node.initializer);
    if (ts.isArrowFunction(initializer) || ts.isFunctionExpression(initializer)) return initializer;
    return resolveCallableExpression(initializer, caller, contexts, next, allowDeferredCapture);
  }
  if (binding.kind !== "import") return null;
  const statement = binding.node.parent;
  const declaration = ts.isImportDeclaration(statement) ? statement
    : findAncestor(binding.node, ts.isImportDeclaration);
  if (!declaration || !ts.isStringLiteral(declaration.moduleSpecifier)) return null;
  const targetPath = resolveLocalModule(caller.item.relativePath, declaration.moduleSpecifier.text, contexts);
  const target = targetPath ? contexts.get(targetPath) : undefined;
  if (!target) return null;
  return findExportedCallable(target.source, binding.exportedName);
}

function resolveForwardedCallable(
  callback: ts.ArrowFunction | ts.FunctionExpression,
  caller: SourceContext,
  contexts: Map<string, SourceContext>,
  visited: Set<ts.Node>
): ts.FunctionLikeDeclaration | null {
  if (callback.parameters.length !== 1 || !callback.parameters[0].dotDotDotToken
    || !ts.isIdentifier(callback.parameters[0].name)) return null;
  const body = callback.body;
  const expression = ts.isBlock(body)
    ? body.statements.length === 1 && ts.isReturnStatement(body.statements[0]) ? body.statements[0].expression : undefined
    : body;
  const call = expression && unwrapAliasExpression(expression);
  if (!call || !ts.isCallExpression(call) || call.arguments.length !== 1 || !ts.isSpreadElement(call.arguments[0])
    || !ts.isIdentifier(call.arguments[0].expression)
    || call.arguments[0].expression.text !== callback.parameters[0].name.text) return null;
  return resolveCallableExpression(call.expression, caller, contexts, visited, true);
}

function resolveDeferredCapturedVariable(
  name: string,
  use: ts.Identifier,
  source: ts.SourceFile
): Extract<LexicalBinding, { kind: "variable" }> | null {
  const capture = findAncestor(use, (node): node is ts.ArrowFunction | ts.FunctionExpression =>
    ts.isArrowFunction(node) || ts.isFunctionExpression(node));
  if (!capture) return null;
  const boundary = capture.parent ? executionRoot(capture.parent) : source;
  if (boundary === capture) return null;
  const declarations: ts.VariableDeclaration[] = [];
  const visit = (node: ts.Node): void => {
    if (node !== boundary && ts.isFunctionLike(node) && node !== capture) return;
    if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name) && node.name.text === name) declarations.push(node);
    ts.forEachChild(node, visit);
  };
  visit(boundary);
  if (declarations.length !== 1 || !declarations[0].initializer) return null;
  const declaration = declarations[0];
  const list = declaration.parent;
  if (!ts.isVariableDeclarationList(list) || (list.flags & ts.NodeFlags.Const) === 0) return null;
  return { kind: "variable", node: declaration };
}

function isExactImportedCall(
  call: ts.CallExpression,
  source: ts.SourceFile,
  moduleName: string,
  exportedNames: Set<string>
): boolean {
  const callee = unwrapAliasExpression(call.expression);
  if (!ts.isIdentifier(callee)) return false;
  const binding = resolveLexicalBinding(callee.text, callee, source);
  if (binding?.kind !== "import" || !exportedNames.has(binding.exportedName)) return false;
  const declaration = findAncestor(binding.node, ts.isImportDeclaration);
  return Boolean(declaration && ts.isStringLiteralLike(declaration.moduleSpecifier)
    && declaration.moduleSpecifier.text === moduleName);
}

function findExportedCallable(source: ts.SourceFile, exportedName: string): ts.FunctionLikeDeclaration | null {
  for (const statement of source.statements) {
    const exported = hasModifier(statement, ts.SyntaxKind.ExportKeyword);
    const isDefault = hasModifier(statement, ts.SyntaxKind.DefaultKeyword);
    if (ts.isFunctionDeclaration(statement) && statement.body && exported
      && (exportedName === "default" ? isDefault : !isDefault && statement.name?.text === exportedName)
      && (!statement.name || !bindingWrittenBefore(statement.name, source))) return statement;
    if (exported && ts.isVariableStatement(statement) && exportedName !== "default") {
      for (const declaration of statement.declarationList.declarations) {
        if (ts.isIdentifier(declaration.name) && declaration.name.text === exportedName && isImmutableVariable(declaration, source) && declaration.initializer
          && (ts.isArrowFunction(declaration.initializer) || ts.isFunctionExpression(declaration.initializer))) return declaration.initializer;
      }
    }
    if (ts.isExportAssignment(statement) && exportedName === "default" && ts.isIdentifier(statement.expression)) {
      return topLevelCallable(source, statement.expression.text);
    }
    if (ts.isExportDeclaration(statement) && !statement.moduleSpecifier && statement.exportClause && ts.isNamedExports(statement.exportClause)) {
      const binding = statement.exportClause.elements.find((item) => item.name.text === exportedName);
      if (binding) return topLevelCallable(source, binding.propertyName?.text ?? binding.name.text);
    }
  }
  return null;
}

function topLevelCallable(source: ts.SourceFile, name: string): ts.FunctionLikeDeclaration | null {
  const binding = bindingFromStatements(source.statements, name);
  if (binding?.kind === "function") return binding.node.body && !bindingWrittenBefore(binding.node.name!, source) ? binding.node : null;
  if (binding?.kind === "variable" && isImmutableVariable(binding.node, source) && binding.node.initializer
    && (ts.isArrowFunction(binding.node.initializer) || ts.isFunctionExpression(binding.node.initializer))) return binding.node.initializer;
  return null;
}

interface RuntimeAlias {
  prefix: string[];
  kind: "callsite-proven-parameter" | "source-import-or-derived";
}

type LexicalBinding =
  | { kind: "parameter"; node: ts.ParameterDeclaration }
  | { kind: "variable"; node: ts.VariableDeclaration }
  | { kind: "function"; node: ts.FunctionDeclaration }
  | { kind: "import"; node: ts.ImportClause | ts.ImportSpecifier | ts.NamespaceImport; exportedName: string }
  | { kind: "other"; node: ts.Node };

function resolveRuntimeAlias(name: string, use: ts.Node, source: ts.SourceFile, aliases: Map<string, string[]>, factoryAliases: Set<string>, injected: Map<number, string[]>, visited: Set<ts.Node>): RuntimeAlias | null {
  const binding = resolveLexicalBinding(name, use, source);
  if (!binding || visited.has(binding.node)) return null;
  const next = new Set(visited).add(binding.node);
  if (binding.kind === "parameter") {
    const prefix = injected.get(binding.node.getStart(source));
    return prefix && !bindingWrittenBefore(binding.node.name, use)
      ? { prefix, kind: "callsite-proven-parameter" } : null;
  }
  if (binding.kind === "variable") {
    if (!isImmutableVariable(binding.node, use) || !binding.node.initializer) return null;
    if (!ts.isIdentifier(binding.node.name)) {
      const prefix = aliases.get(name);
      return prefix && !namedBindingWrittenBefore(name, binding.node, use)
        ? { prefix, kind: "source-import-or-derived" } : null;
    }
    const factory = factoryAlias(binding.node.initializer, source, aliases, factoryAliases);
    if (factory) return { prefix: factory, kind: "source-import-or-derived" };
    const chain = expressionChain(unwrapAliasExpression(binding.node.initializer));
    if (!chain) return null;
    const parent = resolveRuntimeAlias(chain[0], binding.node, source, aliases, factoryAliases, injected, next);
    return parent ? { prefix: [...parent.prefix, ...chain.slice(1)], kind: parent.kind } : null;
  }
  if (binding.kind === "import") {
    const prefix = aliases.get(name);
    return prefix ? { prefix, kind: "source-import-or-derived" } : null;
  }
  return null;
}

function parameterSourceFromExpression(input: ts.Expression, use: ts.Node, context: SourceContext, visited: Set<ts.Node>): ParameterSource {
  const expression = unwrapAliasExpression(input);
  const factory = factoryAlias(expression, context.source, context.aliases, context.factoryAliases);
  if (factory) return { kind: "known", prefix: factory };
  const chain = expressionChain(expression);
  if (!chain) return { kind: "unknown" };
  const binding = resolveLexicalBinding(chain[0], use, context.source);
  if (!binding || visited.has(binding.node)) return { kind: "unknown" };
  const next = new Set(visited).add(binding.node);
  if (binding.kind === "parameter") {
    return bindingWrittenBefore(binding.node.name, use) ? { kind: "unknown" } : {
      kind: "parameter",
      parameterKey: parameterKey(context.source, binding.node),
      suffix: chain.slice(1)
    };
  }
  if (binding.kind === "variable") {
    if (!ts.isIdentifier(binding.node.name) || !isImmutableVariable(binding.node, use) || !binding.node.initializer) return { kind: "unknown" };
    return appendParameterSource(parameterSourceFromExpression(binding.node.initializer, binding.node, context, next), chain.slice(1));
  }
  if (binding.kind === "import") {
    const prefix = context.aliases.get(chain[0]);
    return prefix ? { kind: "known", prefix: [...prefix, ...chain.slice(1)] } : { kind: "unknown" };
  }
  return { kind: "unknown" };
}

function appendParameterSource(source: ParameterSource, suffix: string[]): ParameterSource {
  if (source.kind === "known") return { kind: "known", prefix: [...(source.prefix ?? []), ...suffix] };
  if (source.kind === "parameter") return { ...source, suffix: [...(source.suffix ?? []), ...suffix] };
  return source;
}

function factoryAlias(input: ts.Expression, source: ts.SourceFile, aliases: Map<string, string[]>, factoryAliases: Set<string>, visited = new Set<ts.Node>()): string[] | null {
  const expression = unwrapAliasExpression(input);
  if (!ts.isCallExpression(expression)) return null;
  const callee = expressionChain(unwrapAliasExpression(expression.expression));
  if (!callee) return null;
  const resolveRoot = (name: string, use: ts.Node): { factory: boolean; namespace: boolean } | null => {
    const binding = resolveLexicalBinding(name, use, source);
    if (!binding || visited.has(binding.node)) return null;
    visited.add(binding.node);
    if (binding.kind === "import") return { factory: factoryAliases.has(name), namespace: aliases.has(name) };
    if (binding.kind !== "variable" || !ts.isIdentifier(binding.node.name)
      || !isImmutableVariable(binding.node, use) || !binding.node.initializer) return null;
    const alias = unwrapAliasExpression(binding.node.initializer);
    return ts.isIdentifier(alias) ? resolveRoot(alias.text, alias) : null;
  };
  const root = resolveRoot(callee[0], expression);
  return root && ((callee.length === 1 && root.factory)
    || (callee.length === 2 && root.namespace && base44FactoryNames.has(callee[1]))) ? [] : null;
}

function resolveLexicalBinding(name: string, use: ts.Node, source: ts.SourceFile): LexicalBinding | null {
  for (let current: ts.Node | undefined = use.parent; current; current = current.parent) {
    if (ts.isBlock(current)) {
      const binding = bindingFromStatements(current.statements, name);
      if (binding) return binding;
    }
    if (ts.isCaseBlock(current)) {
      const binding = bindingFromStatements(current.clauses.flatMap((clause) => [...clause.statements]), name);
      if (binding) return binding;
    }
    if (ts.isCatchClause(current) && current.variableDeclaration
      && bindingNames(current.variableDeclaration.name).includes(name)) return { kind: "other", node: current.variableDeclaration };
    if ((ts.isForStatement(current) || ts.isForInStatement(current) || ts.isForOfStatement(current))
      && current.initializer && ts.isVariableDeclarationList(current.initializer)) {
      const declaration = current.initializer.declarations.find((item) => bindingNames(item.name).includes(name));
      if (declaration) return { kind: "variable", node: declaration };
    }
    if ((ts.isFunctionExpression(current) || ts.isClassExpression(current)) && current.name?.text === name) {
      return { kind: "other", node: current };
    }
    if (ts.isFunctionLike(current)) {
      const parameter = current.parameters.find((candidate) => bindingNames(candidate.name).includes(name));
      if (parameter) return { kind: "parameter", node: parameter };
      if ("body" in current && current.body) {
        const variable = functionScopedVar(current.body, name);
        if (variable) return { kind: "variable", node: variable };
      }
    }
    if (ts.isSourceFile(current)) return bindingFromStatements(current.statements, name);
  }
  return bindingFromStatements(source.statements, name);
}

function bindingFromStatements(statements: readonly ts.Statement[], name: string): LexicalBinding | null {
  const functions: ts.FunctionDeclaration[] = [];
  for (const statement of statements) {
    if (ts.isVariableStatement(statement)) {
      const declaration = statement.declarationList.declarations.find((item) => bindingNames(item.name).includes(name));
      if (declaration) return { kind: "variable", node: declaration };
    }
    if (ts.isFunctionDeclaration(statement) && statement.name?.text === name) functions.push(statement);
    if (ts.isClassDeclaration(statement) && statement.name?.text === name) return { kind: "other", node: statement };
    if (ts.isImportDeclaration(statement) && !statement.importClause?.isTypeOnly) {
      const clause = statement.importClause;
      if (clause?.name?.text === name) return { kind: "import", node: clause, exportedName: "default" };
      if (clause?.namedBindings && ts.isNamespaceImport(clause.namedBindings) && clause.namedBindings.name.text === name) {
        return { kind: "import", node: clause.namedBindings, exportedName: "*" };
      }
      if (clause?.namedBindings && ts.isNamedImports(clause.namedBindings)) {
        const binding = clause.namedBindings.elements.find((item) => !item.isTypeOnly && item.name.text === name);
        if (binding) return { kind: "import", node: binding, exportedName: binding.propertyName?.text ?? binding.name.text };
      }
    }
  }
  const implementation = functions.find((candidate) => candidate.body);
  return implementation ? { kind: "function", node: implementation }
    : functions[0] ? { kind: "function", node: functions[0] } : null;
}

function functionScopedVar(root: ts.Node, name: string): ts.VariableDeclaration | null {
  let found: ts.VariableDeclaration | null = null;
  const visitNode = (node: ts.Node): void => {
    if (found || (node !== root && ts.isFunctionLike(node))) return;
    if (ts.isVariableDeclarationList(node) && (node.flags & ts.NodeFlags.BlockScoped) === 0) {
      found = node.declarations.find((declaration) => bindingNames(declaration.name).includes(name)) ?? null;
      if (found) return;
    }
    ts.forEachChild(node, visitNode);
  };
  visitNode(root);
  return found;
}

function isImmutableVariable(declaration: ts.VariableDeclaration, use: ts.Node): boolean {
  return ts.isVariableDeclarationList(declaration.parent)
    && Boolean(declaration.parent.flags & ts.NodeFlags.Const)
    && declaration.getEnd() <= (ts.isSourceFile(use) ? use.getEnd() : use.getStart(use.getSourceFile()));
}

function bindingWrittenBefore(name: ts.BindingName, use: ts.Node): boolean {
  if (!ts.isIdentifier(name)) return true;
  const limit = ts.isSourceFile(use) ? use.getEnd() : use.getStart(use.getSourceFile());
  const root = executionRoot(name);
  const declaration = name.parent;
  let written = false;
  const visitNode = (node: ts.Node): void => {
    if (written || node.getStart(node.getSourceFile()) >= limit || (node !== root && ts.isFunctionLike(node))) return;
    if (ts.isBinaryExpression(node) && isAssignmentOperator(node.operatorToken.kind)) {
      written = assignmentTargetIdentifiers(node.left).some((identifier) => identifier.text === name.text
        && resolveLexicalBinding(identifier.text, identifier, identifier.getSourceFile())?.node === declaration);
    }
    if ((ts.isForInStatement(node) || ts.isForOfStatement(node)) && !ts.isVariableDeclarationList(node.initializer)) {
      written = assignmentTargetIdentifiers(node.initializer).some((identifier) => identifier.text === name.text
        && resolveLexicalBinding(identifier.text, identifier, identifier.getSourceFile())?.node === declaration);
    }
    if (((ts.isPrefixUnaryExpression(node) && [ts.SyntaxKind.PlusPlusToken, ts.SyntaxKind.MinusMinusToken].includes(node.operator))
      || ts.isPostfixUnaryExpression(node)) && ts.isIdentifier(node.operand) && node.operand.text === name.text
      && resolveLexicalBinding(node.operand.text, node.operand, node.operand.getSourceFile())?.node === declaration) written = true;
    if (!written) ts.forEachChild(node, visitNode);
  };
  visitNode(root);
  return written;
}

function namedBindingWrittenBefore(name: string, declaration: ts.VariableDeclaration, use: ts.Node): boolean {
  const limit = use.getStart(use.getSourceFile());
  const root = executionRoot(declaration.name);
  let written = false;
  const visitNode = (node: ts.Node): void => {
    if (written || node.getStart(node.getSourceFile()) >= limit || (node !== root && ts.isFunctionLike(node))) return;
    if (ts.isBinaryExpression(node) && isAssignmentOperator(node.operatorToken.kind)) {
      written = assignmentTargetIdentifiers(node.left).some((identifier) => identifier.text === name
        && resolveLexicalBinding(name, identifier, identifier.getSourceFile())?.node === declaration);
    }
    if (!written) ts.forEachChild(node, visitNode);
  };
  visitNode(root);
  return written;
}

function assignmentTargetIdentifiers(input: ts.Expression): ts.Identifier[] {
  const expression = unwrapAliasExpression(input);
  if (ts.isIdentifier(expression)) return [expression];
  if (ts.isArrayLiteralExpression(expression)) {
    return expression.elements.flatMap((element) => ts.isExpression(element) ? assignmentTargetIdentifiers(element) : []);
  }
  if (ts.isObjectLiteralExpression(expression)) {
    return expression.properties.flatMap((property) => {
      if (ts.isShorthandPropertyAssignment(property)) return [property.name];
      if (ts.isPropertyAssignment(property)) return assignmentTargetIdentifiers(property.initializer);
      if (ts.isSpreadAssignment(property)) return assignmentTargetIdentifiers(property.expression);
      return [];
    });
  }
  if (ts.isSpreadElement(expression)) return assignmentTargetIdentifiers(expression.expression);
  return [];
}

function isAssignmentOperator(kind: ts.SyntaxKind): boolean {
  return kind >= ts.SyntaxKind.FirstAssignment && kind <= ts.SyntaxKind.LastAssignment;
}

function executionRoot(node: ts.Node): ts.Node {
  for (let current: ts.Node | undefined = node.parent; current; current = current.parent) {
    if (ts.isFunctionDeclaration(current) && current.name === node) continue;
    if (ts.isFunctionLike(current) && "body" in current && current.body) return current.body;
    if (ts.isSourceFile(current)) return current;
  }
  return node.getSourceFile();
}

function findAncestor<T extends ts.Node>(node: ts.Node, predicate: (candidate: ts.Node) => candidate is T): T | null {
  for (let current: ts.Node | undefined = node.parent; current; current = current.parent) if (predicate(current)) return current;
  return null;
}

function hasModifier(node: ts.Node, kind: ts.SyntaxKind): boolean {
  return ts.canHaveModifiers(node) && Boolean(ts.getModifiers(node)?.some((modifier) => modifier.kind === kind));
}

function bindingNames(name: ts.BindingName): string[] {
  if (ts.isIdentifier(name)) return [name.text];
  return name.elements.flatMap((element) => ts.isOmittedExpression(element) ? [] : bindingNames(element.name));
}

function seedDirectSdkImports(context: SourceContext): void {
  for (const statement of context.source.statements) {
    if (!ts.isImportDeclaration(statement) || !ts.isStringLiteral(statement.moduleSpecifier) || !isBase44SdkClientImport(statement.moduleSpecifier.text)) continue;
    const clause = statement.importClause;
    if (!clause || clause.isTypeOnly) continue;
    if (clause.name) {
      context.aliases.set(clause.name.text, []);
      if (base44FactoryNames.has(clause.name.text)) context.factoryAliases.add(clause.name.text);
    }
    if (clause.namedBindings && ts.isNamespaceImport(clause.namedBindings)) context.aliases.set(clause.namedBindings.name.text, []);
    if (clause.namedBindings && ts.isNamedImports(clause.namedBindings)) {
      for (const element of clause.namedBindings.elements.filter((binding) => !binding.isTypeOnly)) {
        const importedName = element.propertyName?.text ?? element.name.text;
        if (base44FactoryNames.has(importedName)) context.factoryAliases.add(element.name.text);
        else context.aliases.set(element.name.text, primitiveRoots.has(importedName) ? [importedName] : []);
      }
    }
  }
}

function propagateLocalImports(context: SourceContext, contexts: Map<string, SourceContext>, exportsByFile: Map<string, Map<string, string[]>>): boolean {
  let changed = false;
  for (const statement of context.source.statements) {
    if (!ts.isImportDeclaration(statement) || !ts.isStringLiteral(statement.moduleSpecifier) || statement.importClause?.isTypeOnly) continue;
    const target = resolveLocalModule(context.item.relativePath, statement.moduleSpecifier.text, contexts);
    const exported = target ? exportsByFile.get(target) : undefined;
    if (!exported) continue;
    const clause = statement.importClause;
    if (clause?.name) changed = setAlias(context.aliases, clause.name.text, exported.get("default")) || changed;
    if (clause?.namedBindings && ts.isNamedImports(clause.namedBindings)) {
      for (const element of clause.namedBindings.elements.filter((binding) => !binding.isTypeOnly)) {
        const importedName = element.propertyName?.text ?? element.name.text;
        changed = setAlias(context.aliases, element.name.text, exported.get(importedName)) || changed;
      }
    }
  }
  const visitDynamicImports = (node: ts.Node): void => {
    if (ts.isVariableDeclaration(node) && ts.isObjectBindingPattern(node.name) && node.initializer) {
      const requested = dynamicImportSpecifier(node.initializer);
      const target = requested ? resolveLocalModule(context.item.relativePath, requested, contexts) : null;
      const exported = target ? exportsByFile.get(target) : undefined;
      if (exported) {
        for (const element of node.name.elements) {
          if (element.dotDotDotToken || element.initializer || !ts.isIdentifier(element.name)) continue;
          const importedName = element.propertyName && ts.isIdentifier(element.propertyName)
            ? element.propertyName.text : element.name.text;
          changed = setAlias(context.aliases, element.name.text, exported.get(importedName)) || changed;
        }
      }
    }
    ts.forEachChild(node, visitDynamicImports);
  };
  visitDynamicImports(context.source);
  return changed;
}

function dynamicLocalImportTargets(context: SourceContext, contexts: Map<string, SourceContext>): string[] {
  const targets: string[] = [];
  const visitNode = (node: ts.Node): void => {
    if (ts.isCallExpression(node) && node.expression.kind === ts.SyntaxKind.ImportKeyword
      && node.arguments.length === 1 && ts.isStringLiteralLike(node.arguments[0])) {
      const target = resolveLocalModule(context.item.relativePath, node.arguments[0].text, contexts);
      if (target) targets.push(target);
    }
    ts.forEachChild(node, visitNode);
  };
  visitNode(context.source);
  return uniqueStrings(targets);
}

function dynamicImportSpecifier(input: ts.Expression): string | null {
  let expression = unwrapAliasExpression(input);
  if (ts.isAwaitExpression(expression)) expression = unwrapAliasExpression(expression.expression);
  return ts.isCallExpression(expression) && expression.expression.kind === ts.SyntaxKind.ImportKeyword
    && expression.arguments.length === 1 && ts.isStringLiteralLike(expression.arguments[0])
    ? expression.arguments[0].text : null;
}

function updateExportedAliases(context: SourceContext, exportsByFile: Map<string, Map<string, string[]>>): boolean {
  const exported = exportsByFile.get(context.item.relativePath) ?? new Map<string, string[]>();
  let changed = false;
  const exportedPrefix = (name: string): string[] | undefined => resolveRuntimeAlias(name, context.source, context.source,
    context.aliases, context.factoryAliases, new Map(), new Set())?.prefix;
  for (const statement of context.source.statements) {
    if (ts.isVariableStatement(statement) && statement.modifiers?.some((modifier) => modifier.kind === ts.SyntaxKind.ExportKeyword)) {
      for (const declaration of statement.declarationList.declarations) {
        if (ts.isIdentifier(declaration.name)) changed = setAlias(exported, declaration.name.text, exportedPrefix(declaration.name.text)) || changed;
      }
    }
    if (ts.isExportDeclaration(statement) && statement.exportClause && ts.isNamedExports(statement.exportClause) && !statement.moduleSpecifier) {
      for (const element of statement.exportClause.elements) {
        const localName = element.propertyName?.text ?? element.name.text;
        changed = setAlias(exported, element.name.text, exportedPrefix(localName)) || changed;
      }
    }
  }
  exportsByFile.set(context.item.relativePath, exported);
  return changed;
}

function resolveLocalModule(fromFile: string, specifier: string, contexts: Map<string, SourceContext>): string | null {
  const bases: string[] = [];
  if (specifier.startsWith("@/")) bases.push(`src/${specifier.slice(2)}`);
  if (specifier.startsWith(".")) {
    bases.push(path.posix.normalize(path.posix.join(path.posix.dirname(fromFile), specifier)));
  }
  for (const alias of localModuleAliases(contexts)) {
    const capture = moduleAliasMatch(alias.pattern, specifier);
    if (capture === null) continue;
    bases.push(...alias.targets.map((target) => target.replace("*", capture)));
  }
  if (!specifier.startsWith(".") && !specifier.startsWith("@/") && !/^[a-z]+:/iu.test(specifier)) {
    bases.push(...(localModuleAliasCache.get(contexts)?.baseUrls ?? [])
      .map((baseUrl) => path.posix.normalize(path.posix.join(baseUrl, specifier))));
  }
  if (!specifier.startsWith(".") && !specifier.startsWith("@/")
    && !isDeclaredExternalModule(specifier, contexts)) {
    bases.push(specifier, `src/${specifier}`);
  }
  for (const base of [...new Set(bases)]) {
    if (base.startsWith("../") || path.posix.isAbsolute(base)) continue;
    for (const candidate of [base, ...[".ts", ".tsx", ".js", ".jsx"].map((extension) => `${base}${extension}`), ...[".ts", ".tsx", ".js", ".jsx"].map((extension) => `${base}/index${extension}`)]) {
      if (contexts.has(candidate)) return candidate;
    }
  }
  return null;
}

function moduleAliasMatch(pattern: string, specifier: string): string | null {
  const firstWildcard = pattern.indexOf("*");
  if (firstWildcard < 0) return pattern === specifier ? "" : null;
  if (pattern.indexOf("*", firstWildcard + 1) >= 0) return null;
  const prefix = pattern.slice(0, firstWildcard);
  const suffix = pattern.slice(firstWildcard + 1);
  return specifier.startsWith(prefix) && specifier.endsWith(suffix)
    ? specifier.slice(prefix.length, suffix.length ? specifier.length - suffix.length : undefined)
    : null;
}

function setAlias(target: Map<string, string[]>, name: string, value: string[] | undefined): boolean {
  if (!value) return false;
  const existing = target.get(name);
  if (existing && JSON.stringify(existing) === JSON.stringify(value)) return false;
  target.set(name, value);
  return true;
}
