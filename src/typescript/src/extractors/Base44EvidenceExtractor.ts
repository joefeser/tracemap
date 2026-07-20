import fs from "node:fs/promises";
import path from "node:path";
import ts from "typescript";
import { CodeFact, EvidenceTiers, FactTypes, FileInventoryItem, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { hash } from "../util/Hash";

const entityOperations = new Set(["list", "filter", "get", "create", "update", "delete", "bulkCreate", "subscribe"]);
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
  const aliasMaps = await buildAliasMaps(sourceItems);
  for (const item of inventory.filter((file) => !file.skipped)) {
    if (item.relativePath.endsWith(".sql")) {
      if (isMigrationPath(item.relativePath)) facts.push(await sqlFact(manifest, item));
      continue;
    }
    if (!/\.[jt]sx?$/.test(item.relativePath) || item.relativePath.endsWith(".d.ts")) {
      continue;
    }
    const text = await fs.readFile(item.absolutePath, "utf8");
    const source = ts.createSourceFile(item.absolutePath, text, ts.ScriptTarget.Latest, true, scriptKind(item.relativePath));
    const aliases = aliasMaps.get(item.relativePath) ?? new Map<string, string[]>();
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
    visit(source, source, item.relativePath, text, aliases, manifest, facts);
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
  return facts;
}

function visit(node: ts.Node, source: ts.SourceFile, filePath: string, text: string, aliases: Map<string, string[]>, manifest: ScanManifest, facts: CodeFact[]): void {
  if (ts.isCallExpression(node)) {
    const chain = expressionChain(node.expression);
    const prefix = chain ? aliases.get(chain[0]) : undefined;
    if (chain && prefix) addSdkCall([...prefix, ...chain.slice(1)], node, source, filePath, text, manifest, facts);
    if (chain?.join(".") === "Deno.env.get") {
      const name = stringArgument(node.arguments[0]);
      facts.push(fact(manifest, FactTypes.Base44EnvironmentAccess, RuleIds.Base44EnvironmentAccess, node, source, filePath, name ?? "dynamic", {
        environmentName: name ?? "dynamic",
        accessKind: name ? "static" : "dynamic",
        sourceFileSha256: hash(text, 64)
      }, name ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown));
    }
    if (chain?.at(-1) === "fetch" || (chain?.[0] === "axios" && chain.length === 2 && axiosMethods.has(chain[1]))) {
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
  ts.forEachChild(node, (child) => visit(child, source, filePath, text, aliases, manifest, facts));
}

function addSdkCall(chain: string[], node: ts.CallExpression, source: ts.SourceFile, filePath: string, text: string, manifest: ScanManifest, facts: CodeFact[]): void {
  const rootIndex = chain.findIndex((part) => primitiveRoots.has(part));
  if (rootIndex < 0) return;
  const relative = chain.slice(rootIndex);
  const capability = relative.join(".");
  facts.push(fact(manifest, FactTypes.Base44SdkPrimitive, RuleIds.Base44SdkPrimitive, node, source, filePath, capability, {
    capability,
    primitiveRoot: relative[0],
    sourceFileSha256: hash(text, 64)
  }));
  const functionsIndex = sdkRootIndex(relative, "functions");
  if (functionsIndex >= 0 && relative[functionsIndex + 1] === "invoke") {
    const functionName = stringArgument(node.arguments[0]);
    facts.push(fact(manifest, FactTypes.Base44FunctionInvocation, RuleIds.Base44FunctionInvocation, node, source, filePath, functionName ?? "dynamic", {
      functionName: functionName ?? "dynamic",
      bindingKind: functionName ? "static" : "dynamic",
      sourceFileSha256: hash(text, 64)
    }, functionName ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown));
  }
  const entitiesIndex = sdkRootIndex(relative, "entities");
  if (entitiesIndex >= 0 && entityOperations.has(relative[entitiesIndex + 2])) {
    facts.push(fact(manifest, FactTypes.Base44EntityOperation, RuleIds.Base44EntityOperation, node, source, filePath, relative[entitiesIndex + 1], {
      entityName: relative[entitiesIndex + 1],
      operationName: relative[entitiesIndex + 2],
      sourceFileSha256: hash(text, 64)
    }));
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

async function buildAliasMaps(items: readonly FileInventoryItem[]): Promise<Map<string, Map<string, string[]>>> {
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
  return new Map([...contexts].map(([filePath, context]) => [filePath, context.aliases]));
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
