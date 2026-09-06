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
    const factoryAliases = aliasDiscovery.factoryAliasesByFile.get(item.relativePath) ?? new Set<string>();
    const injectedParameters = aliasDiscovery.injectedParametersByFile.get(item.relativePath) ?? new Map<number, string[]>();
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
    visit(source, source, item.relativePath, text, aliases, factoryAliases, injectedParameters, base44Context, manifest, facts);
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

function visit(node: ts.Node, source: ts.SourceFile, filePath: string, text: string, aliases: Map<string, string[]>, factoryAliases: Set<string>, injectedParameters: Map<number, string[]>, base44Context: boolean, manifest: ScanManifest, facts: CodeFact[]): void {
  if (ts.isCallExpression(node)) {
    const chain = expressionChain(node.expression);
    const binding = chain ? resolveRuntimeAlias(chain[0], node, source, aliases, factoryAliases, injectedParameters, new Set()) : null;
    if (chain && binding) addSdkCall([...binding.prefix, ...chain.slice(1)], node, source, filePath, text, manifest, facts, binding.kind);
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
  ts.forEachChild(node, (child) => visit(child, source, filePath, text, aliases, factoryAliases, injectedParameters, base44Context, manifest, facts));
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
      ...clientBindingEvidence,
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
  factoryAliasesByFile: Map<string, Set<string>>;
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
      changed = updateExportedAliases(context, exportsByFile) || changed;
    }
    if (!changed) break;
  }
  return {
    aliasesByFile: new Map([...contexts].map(([filePath, context]) => [filePath, context.aliases])),
    factoryAliasesByFile: new Map([...contexts].map(([filePath, context]) => [filePath, context.factoryAliases])),
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
  expression = unwrapAliasExpression(expression);
  if (!ts.isIdentifier(expression)) return null;
  const binding = resolveLexicalBinding(expression.text, expression, caller.source);
  if (!binding) return null;
  if (binding.kind === "function") {
    return binding.node.body && !bindingWrittenBefore(binding.node.name!, expression) ? binding.node : null;
  }
  if (binding.kind === "variable") {
    return isImmutableVariable(binding.node, expression)
      && binding.node.initializer
      && (ts.isArrowFunction(binding.node.initializer) || ts.isFunctionExpression(binding.node.initializer))
      ? binding.node.initializer : null;
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
    if (!ts.isIdentifier(binding.node.name) || !isImmutableVariable(binding.node, use) || !binding.node.initializer) return null;
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
  return changed;
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
  if (existing && JSON.stringify(existing) === JSON.stringify(value)) return false;
  target.set(name, value);
  return true;
}
