import fs from "node:fs/promises";
import path from "node:path";
import ts from "typescript";
import { CodeFact, EvidenceTiers, FactTypes, FileInventoryItem, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { hash } from "../util/Hash";

const sdkRoots = new Set(["base44", "base44Client"]);
const entityOperations = new Set(["list", "filter", "get", "create", "update", "delete", "bulkCreate", "subscribe"]);
const primitiveRoots = new Set(["auth", "entities", "functions", "integrations", "Analytics", "AppLogs"]);

export async function extractBase44Facts(manifest: ScanManifest, inventory: readonly FileInventoryItem[]): Promise<CodeFact[]> {
  const facts: CodeFact[] = [];
  for (const item of inventory.filter((file) => !file.skipped)) {
    if (item.relativePath.endsWith(".sql")) {
      facts.push(await sqlFact(manifest, item));
      continue;
    }
    if (!/\.[jt]sx?$/.test(item.relativePath) || item.relativePath.endsWith(".d.ts")) {
      continue;
    }
    const text = await fs.readFile(item.absolutePath, "utf8");
    const source = ts.createSourceFile(item.absolutePath, text, ts.ScriptTarget.Latest, true, scriptKind(item.relativePath));
    const aliases = new Set<string>(sdkRoots);
    for (const statement of source.statements) {
      if (!ts.isImportDeclaration(statement) || !ts.isStringLiteral(statement.moduleSpecifier)) continue;
      const requested = statement.moduleSpecifier.text;
      if (!isBase44Sdk(requested)) continue;
      const clause = statement.importClause;
      if (clause?.name) aliases.add(clause.name.text);
      if (clause?.namedBindings && ts.isNamedImports(clause.namedBindings)) {
        for (const element of clause.namedBindings.elements) aliases.add(element.name.text);
      }
      facts.push(fact(manifest, FactTypes.Base44SdkImport, RuleIds.Base44SdkImport, statement, source, item.relativePath, requested, {
        requestedPackage: requested,
        requestedVersion: packageVersion(requested),
        importKind: clause?.isTypeOnly ? "type-only" : "runtime",
        sourceFileSha256: hash(text, 64)
      }));
    }
    discoverClientAliases(source, aliases);
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

function visit(node: ts.Node, source: ts.SourceFile, filePath: string, text: string, aliases: Set<string>, manifest: ScanManifest, facts: CodeFact[]): void {
  if (ts.isCallExpression(node)) {
    const chain = expressionChain(node.expression);
    if (chain && aliases.has(chain[0])) addSdkCall(chain, node, source, filePath, text, manifest, facts);
    if (chain?.join(".") === "Deno.env.get") {
      const name = stringArgument(node.arguments[0]);
      facts.push(fact(manifest, FactTypes.Base44EnvironmentAccess, RuleIds.Base44EnvironmentAccess, node, source, filePath, name ?? "dynamic", {
        environmentName: name ?? "dynamic",
        accessKind: name ? "static" : "dynamic",
        sourceFileSha256: hash(text, 64)
      }, name ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown));
    }
    if (chain?.at(-1) === "fetch" || chain?.join(".") === "axios.request") {
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
  if (relative[0] === "functions" && relative[1] === "invoke") {
    const functionName = stringArgument(node.arguments[0]);
    facts.push(fact(manifest, FactTypes.Base44FunctionInvocation, RuleIds.Base44FunctionInvocation, node, source, filePath, functionName ?? "dynamic", {
      functionName: functionName ?? "dynamic",
      bindingKind: functionName ? "static" : "dynamic",
      sourceFileSha256: hash(text, 64)
    }, functionName ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown));
  }
  if (relative[0] === "entities" && relative.length >= 3 && entityOperations.has(relative[2])) {
    facts.push(fact(manifest, FactTypes.Base44EntityOperation, RuleIds.Base44EntityOperation, node, source, filePath, relative[1], {
      entityName: relative[1],
      operationName: relative[2],
      sourceFileSha256: hash(text, 64)
    }));
  }
}

async function sqlFact(manifest: ScanManifest, item: FileInventoryItem): Promise<CodeFact> {
  const text = await fs.readFile(item.absolutePath, "utf8");
  const kinds = [...new Set([...text.matchAll(/\b(create|alter|drop)\s+(table|index|policy|function|trigger)\b/gi)].map((match) => `${match[1].toLowerCase()}-${match[2].toLowerCase()}`))].sort();
  return createFact(manifest, FactTypes.Base44MigrationSurface, RuleIds.Base44MigrationSurface, EvidenceTiers.Tier3SyntaxOrTextual,
    createEvidence(item.relativePath, 1, Math.max(1, text.split(/\r?\n/).length), "base44-evidence", ScannerVersions.Base44EvidenceExtractor, hash(text, 64)), {
      targetSymbol: item.relativePath,
      properties: { sourceFileSha256: hash(text, 64), statementKinds: kinds.join(";"), statementKindCount: kinds.length }
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

function isBase44Sdk(value: string): boolean {
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

function discoverClientAliases(source: ts.SourceFile, aliases: Set<string>): void {
  const visit = (node: ts.Node): void => {
    if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name) && node.initializer && ts.isCallExpression(node.initializer)) {
      const callee = expressionChain(node.initializer.expression);
      if (callee && callee.some((part) => /^(createClient|createClientFromRequest)$/.test(part))) aliases.add(node.name.text);
    }
    ts.forEachChild(node, visit);
  };
  visit(source);
}
