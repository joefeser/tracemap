import fs from "node:fs/promises";
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
  for (const item of inventory.filter((file) => !file.skipped)) {
    if (item.relativePath.endsWith(".sql")) {
      continue;
    }
    if (!/\.[jt]sx?$/.test(item.relativePath) || item.relativePath.endsWith(".d.ts")) {
      continue;
    }
    const text = await fs.readFile(item.absolutePath, "utf8");
    const source = ts.createSourceFile(item.absolutePath, text, ts.ScriptTarget.Latest, true, scriptKind(item.relativePath));
    const aliases = aliasDiscovery.aliasesByFile.get(item.relativePath) ?? new Map<string, string[]>();
    const injectedParameters = aliasDiscovery.injectedParametersByFile.get(item.relativePath) ?? new Map<number, string[]>();
    const base44Context = aliases.size > 0
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
    visit(source, source, item.relativePath, text, aliases, injectedParameters, new Map(), base44Context, manifest, facts);
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
  const hasBase44Signal = facts.some((candidate) => candidate.factType === FactTypes.Base44SdkImport
    || candidate.factType === FactTypes.Base44FunctionSurface
    || candidate.factType === FactTypes.Base44CustomerBoundary);
  if (hasBase44Signal) {
    for (const item of migrationItems) facts.push(await sqlFact(manifest, item));
  }
  return facts;
}

type ScopedAlias = string[] | null;

function visit(node: ts.Node, source: ts.SourceFile, filePath: string, text: string, aliases: Map<string, string[]>, injectedParameters: Map<number, string[]>, inheritedAliases: Map<string, ScopedAlias>, base44Context: boolean, manifest: ScanManifest, facts: CodeFact[]): void {
  const scopedAliases = ts.isFunctionLike(node)
    ? enterFunctionScope(node, source, injectedParameters, inheritedAliases)
    : inheritedAliases;
  if (ts.isCallExpression(node)) {
    const chain = expressionChain(node.expression);
    const scoped = chain && scopedAliases.has(chain[0]) ? scopedAliases.get(chain[0]) : undefined;
    const prefix = chain && !scopedAliases.has(chain[0]) ? aliases.get(chain[0]) : scoped ?? undefined;
    if (chain && prefix) addSdkCall([...prefix, ...chain.slice(1)], node, source, filePath, text, manifest, facts,
      scopedAliases.has(chain[0]) ? "callsite-proven-parameter" : "source-import-or-derived");
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
  ts.forEachChild(node, (child) => visit(child, source, filePath, text, aliases, injectedParameters, scopedAliases, base44Context, manifest, facts));
}

function enterFunctionScope(node: ts.SignatureDeclaration, source: ts.SourceFile, injectedParameters: Map<number, string[]>, inherited: Map<string, ScopedAlias>): Map<string, ScopedAlias> {
  const scoped = new Map(inherited);
  for (const parameter of node.parameters) {
    for (const name of bindingNames(parameter.name)) scoped.set(name, null);
    if (ts.isIdentifier(parameter.name)) {
      const injected = injectedParameters.get(parameter.getStart(source));
      if (injected) scoped.set(parameter.name.text, injected);
    }
  }
  return scoped;
}

function addSdkCall(chain: string[], node: ts.CallExpression, source: ts.SourceFile, filePath: string, text: string, manifest: ScanManifest, facts: CodeFact[], clientBindingKind: string): void {
  const rootIndex = chain.findIndex((part) => primitiveRoots.has(part));
  if (rootIndex < 0) return;
  const relative = chain.slice(rootIndex);
  const capability = relative.join(".");
  const clientBindingEvidence: Record<string, string> = clientBindingKind === "callsite-proven-parameter" ? { clientBindingKind } : {};
  facts.push(fact(manifest, FactTypes.Base44SdkPrimitive, RuleIds.Base44SdkPrimitive, node, source, filePath, capability, {
    capability,
    ...clientBindingEvidence,
    primitiveRoot: relative[0],
    sourceFileSha256: hash(text, 64)
  }));
  const functionsIndex = sdkRootIndex(relative, "functions");
  if (functionsIndex >= 0 && relative[functionsIndex + 1] === "invoke" && functionsIndex + 2 === relative.length) {
    const functionName = stringArgument(node.arguments[0]);
    facts.push(fact(manifest, FactTypes.Base44FunctionInvocation, RuleIds.Base44FunctionInvocation, node, source, filePath, functionName ?? "dynamic", {
      functionName: functionName ?? "dynamic",
      bindingKind: functionName ? "static" : "dynamic",
      sourceFileSha256: hash(text, 64)
    }, functionName ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown));
  }
  const entitiesIndex = sdkRootIndex(relative, "entities");
  if (entitiesIndex >= 0
    && entityOperations.has(relative[entitiesIndex + 2])
    && entitiesIndex + 3 === relative.length) {
    const entityName = relative[entitiesIndex + 1];
    const operationName = relative[entitiesIndex + 2];
    const operationStartLine = source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1;
    const operationEndLine = source.getLineAndCharacterOfPosition(node.getEnd()).line + 1;
    const operationEvidenceId = `operation-${hash([
      filePath,
      String(operationStartLine),
      String(operationEndLine),
      String(node.getStart(source)),
      String(node.getEnd()),
      entityName,
      operationName,
      hash(node.getText(source), 64)
    ].join("|"), 20)}`;
    const operationFact = fact(manifest, FactTypes.Base44EntityOperation, RuleIds.Base44EntityOperation, node, source, filePath, entityName, {
      entityName,
      ...clientBindingEvidence,
      operationEvidenceId,
      operationName,
      sourceFileSha256: hash(text, 64)
    });
    facts.push(operationFact);
    facts.push(...extractEntityShapeFacts({ manifest, node, source, filePath, sourceText: text, entityName, operationName, operationEvidenceId }));
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

interface AliasDiscovery {
  aliasesByFile: Map<string, Map<string, string[]>>;
  injectedParametersByFile: Map<string, Map<number, string[]>>;
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

  const exportsByFile = new Map<string, Map<string, string[]>>();
  for (let pass = 0; pass < contexts.size + 2; pass++) {
    let changed = false;
    for (const context of contexts.values()) {
      changed = propagateLocalImports(context, contexts, exportsByFile) || changed;
      changed = discoverDerivedAliases(context) || changed;
      changed = updateExportedAliases(context, exportsByFile) || changed;
    }
    if (!changed) break;
  }
  return {
    aliasesByFile: new Map([...contexts].map(([filePath, context]) => [filePath, context.aliases])),
    injectedParametersByFile: discoverInjectedParameterAliases(contexts)
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
            record.sources.push(argumentAliasSource(node.arguments[index], node, caller));
            inputs.set(key, record);
          }
        }
      }
      ts.forEachChild(node, visitCall);
    };
    visitCall(caller.source);
  }

  const resolved = new Map<string, string[]>();
  for (let pass = 0; pass < inputs.size + 1; pass++) {
    let changed = false;
    for (const [key, record] of inputs) {
      const candidates = record.sources.map((source) => resolveParameterSource(source, resolved));
      if (!candidates.length || candidates.some((candidate) => !candidate)) continue;
      const first = candidates[0]!;
      if (!candidates.every((candidate) => candidate!.join(".") === first.join("."))) continue;
      if (!resolved.has(key)) { resolved.set(key, first); changed = true; }
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
  const chain = expressionChain(unwrapAliasExpression(argument));
  if (!chain) return { kind: "unknown" };
  const parameter = enclosingParameter(chain[0], call);
  if (parameter) return {
    kind: "parameter",
    parameterKey: parameterKey(caller.source, parameter),
    suffix: chain.slice(1)
  };
  if (isLocallyShadowed(chain[0], call)) return { kind: "unknown" };
  const prefix = caller.aliases.get(chain[0]);
  return prefix ? { kind: "known", prefix: [...prefix, ...chain.slice(1)] } : { kind: "unknown" };
}

function unwrapAliasExpression(input: ts.Expression): ts.Expression {
  let node = input;
  while (ts.isParenthesizedExpression(node) || ts.isAsExpression(node) || ts.isTypeAssertionExpression(node)
    || ts.isNonNullExpression(node) || ts.isSatisfiesExpression(node)) node = node.expression;
  return node;
}

function enclosingParameter(name: string, use: ts.Node): ts.ParameterDeclaration | null {
  for (let current: ts.Node | undefined = use.parent; current; current = current.parent) {
    if (!ts.isFunctionLike(current)) continue;
    const parameter = current.parameters.find((candidate) => bindingNames(candidate.name).includes(name));
    if (parameter) return parameter;
  }
  return null;
}

function resolveCallableTarget(expression: ts.Expression, caller: SourceContext, contexts: Map<string, SourceContext>): ts.FunctionLikeDeclaration | null {
  expression = unwrapAliasExpression(expression);
  if (!ts.isIdentifier(expression) || enclosingParameter(expression.text, expression) || isLocallyShadowed(expression.text, expression)) return null;
  const local = findLocalCallable(caller.source, expression.text);
  if (local) return local;
  for (const statement of caller.source.statements) {
    if (!ts.isImportDeclaration(statement) || !ts.isStringLiteral(statement.moduleSpecifier) || statement.importClause?.isTypeOnly) continue;
    const targetPath = resolveLocalModule(caller.item.relativePath, statement.moduleSpecifier.text, contexts);
    const target = targetPath ? contexts.get(targetPath) : undefined;
    if (!target) continue;
    const clause = statement.importClause;
    if (clause?.name?.text === expression.text) return findExportedCallable(target.source, "default");
    if (clause?.namedBindings && ts.isNamedImports(clause.namedBindings)) {
      const binding = clause.namedBindings.elements.find((item) => !item.isTypeOnly && item.name.text === expression.text);
      if (binding) return findExportedCallable(target.source, binding.propertyName?.text ?? binding.name.text);
    }
  }
  return null;
}

function isLocallyShadowed(name: string, use: ts.Node): boolean {
  const owningFunction = enclosingFunction(use);
  if (owningFunction?.body && containsFunctionScopedVar(owningFunction.body, name)) return true;
  for (let current: ts.Node | undefined = use.parent; current && !ts.isSourceFile(current); current = current.parent) {
    if (ts.isCatchClause(current) && current.variableDeclaration
      && bindingNames(current.variableDeclaration.name).includes(name)) return true;
    if (ts.isBlock(current) && directScopeBindings(current).has(name)) return true;
    if ((ts.isForStatement(current) || ts.isForInStatement(current) || ts.isForOfStatement(current))
      && current.initializer && ts.isVariableDeclarationList(current.initializer)
      && current.initializer.declarations.some((declaration) => bindingNames(declaration.name).includes(name))) return true;
  }
  return false;
}

function enclosingFunction(node: ts.Node): ts.FunctionLikeDeclaration | null {
  for (let current: ts.Node | undefined = node.parent; current; current = current.parent) {
    if (ts.isFunctionLike(current) && "body" in current) return current as ts.FunctionLikeDeclaration;
  }
  return null;
}

function containsFunctionScopedVar(root: ts.Node, name: string): boolean {
  let found = false;
  const visitNode = (node: ts.Node): void => {
    if (found || (node !== root && ts.isFunctionLike(node))) return;
    if (ts.isVariableDeclarationList(node) && (node.flags & ts.NodeFlags.BlockScoped) === 0
      && node.declarations.some((declaration) => bindingNames(declaration.name).includes(name))) {
      found = true;
      return;
    }
    ts.forEachChild(node, visitNode);
  };
  visitNode(root);
  return found;
}

function directScopeBindings(scope: ts.Block): Set<string> {
  const names = new Set<string>();
  for (const statement of scope.statements) {
    if (ts.isVariableStatement(statement)) {
      for (const declaration of statement.declarationList.declarations) {
        for (const name of bindingNames(declaration.name)) names.add(name);
      }
    } else if ((ts.isFunctionDeclaration(statement) || ts.isClassDeclaration(statement)) && statement.name) {
      names.add(statement.name.text);
    }
  }
  return names;
}

function findLocalCallable(source: ts.SourceFile, name: string): ts.FunctionLikeDeclaration | null {
  for (const statement of source.statements) {
    if (ts.isFunctionDeclaration(statement) && statement.name?.text === name) return statement;
    if (ts.isVariableStatement(statement)) {
      for (const declaration of statement.declarationList.declarations) {
        if (ts.isIdentifier(declaration.name) && declaration.name.text === name && declaration.initializer
          && (ts.isArrowFunction(declaration.initializer) || ts.isFunctionExpression(declaration.initializer))) return declaration.initializer;
      }
    }
  }
  return null;
}

function findExportedCallable(source: ts.SourceFile, exportedName: string): ts.FunctionLikeDeclaration | null {
  for (const statement of source.statements) {
    const exported = hasModifier(statement, ts.SyntaxKind.ExportKeyword);
    const isDefault = hasModifier(statement, ts.SyntaxKind.DefaultKeyword);
    if (ts.isFunctionDeclaration(statement) && exported
      && (exportedName === "default" ? isDefault : !isDefault && statement.name?.text === exportedName)) return statement;
    if (exported && ts.isVariableStatement(statement) && exportedName !== "default") {
      for (const declaration of statement.declarationList.declarations) {
        if (ts.isIdentifier(declaration.name) && declaration.name.text === exportedName && declaration.initializer
          && (ts.isArrowFunction(declaration.initializer) || ts.isFunctionExpression(declaration.initializer))) return declaration.initializer;
      }
    }
    if (ts.isExportAssignment(statement) && exportedName === "default" && ts.isIdentifier(statement.expression)) {
      return findLocalCallable(source, statement.expression.text);
    }
    if (ts.isExportDeclaration(statement) && !statement.moduleSpecifier && statement.exportClause && ts.isNamedExports(statement.exportClause)) {
      const binding = statement.exportClause.elements.find((item) => item.name.text === exportedName);
      if (binding) return findLocalCallable(source, binding.propertyName?.text ?? binding.name.text);
    }
  }
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
  return changed;
}

function discoverDerivedAliases(context: SourceContext): boolean {
  let changed = false;
  const visitNode = (node: ts.Node): void => {
    if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name) && node.initializer) {
      if (ts.isCallExpression(node.initializer)) {
        const callee = expressionChain(node.initializer.expression);
        const directFactory = callee?.length === 1 && context.factoryAliases.has(callee[0]);
        const namespaceFactory = Boolean(callee && callee.length > 1 && context.aliases.has(callee[0]) && base44FactoryNames.has(callee.at(-1) ?? ""));
        if (directFactory || namespaceFactory) changed = setAlias(context.aliases, node.name.text, []) || changed;
      } else {
        const chain = expressionChain(node.initializer);
        const prefix = chain ? context.aliases.get(chain[0]) : undefined;
        if (chain && prefix) changed = setAlias(context.aliases, node.name.text, [...prefix, ...chain.slice(1)]) || changed;
      }
    }
    ts.forEachChild(node, visitNode);
  };
  visitNode(context.source);
  return changed;
}

function updateExportedAliases(context: SourceContext, exportsByFile: Map<string, Map<string, string[]>>): boolean {
  const exported = exportsByFile.get(context.item.relativePath) ?? new Map<string, string[]>();
  let changed = false;
  for (const statement of context.source.statements) {
    if (ts.isVariableStatement(statement) && statement.modifiers?.some((modifier) => modifier.kind === ts.SyntaxKind.ExportKeyword)) {
      for (const declaration of statement.declarationList.declarations) {
        if (ts.isIdentifier(declaration.name)) changed = setAlias(exported, declaration.name.text, context.aliases.get(declaration.name.text)) || changed;
      }
    }
    if (ts.isExportDeclaration(statement) && statement.exportClause && ts.isNamedExports(statement.exportClause) && !statement.moduleSpecifier) {
      for (const element of statement.exportClause.elements) {
        const localName = element.propertyName?.text ?? element.name.text;
        changed = setAlias(exported, element.name.text, context.aliases.get(localName)) || changed;
      }
    }
  }
  exportsByFile.set(context.item.relativePath, exported);
  return changed;
}

function resolveLocalModule(fromFile: string, specifier: string, contexts: Map<string, SourceContext>): string | null {
  let base: string;
  if (specifier.startsWith("@/")) base = `src/${specifier.slice(2)}`;
  else if (specifier.startsWith(".")) base = path.posix.normalize(path.posix.join(path.posix.dirname(fromFile), specifier));
  else return null;
  for (const candidate of [base, ...[".ts", ".tsx", ".js", ".jsx"].map((extension) => `${base}${extension}`), ...[".ts", ".tsx", ".js", ".jsx"].map((extension) => `${base}/index${extension}`)]) {
    if (contexts.has(candidate)) return candidate;
  }
  return null;
}

function setAlias(target: Map<string, string[]>, name: string, value: string[] | undefined): boolean {
  if (!value) return false;
  const existing = target.get(name);
  if (existing && existing.join(".") === value.join(".")) return false;
  target.set(name, value);
  return true;
}
