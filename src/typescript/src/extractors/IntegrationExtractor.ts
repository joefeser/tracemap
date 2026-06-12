import ts from "typescript";
import { CodeFact, EvidenceTiers, FactTypes, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { hash } from "../util/Hash";
import { repoRelative } from "../util/Paths";
import { LoadedProject } from "./TypeScriptProjectLoader";

export function extractIntegrationFacts(repoPath: string, manifest: ScanManifest, projects: readonly LoadedProject[]): CodeFact[] {
  const facts: CodeFact[] = [];
  for (const project of projects) {
    for (const sourceFile of project.sourceFiles) {
      visit(repoPath, manifest, project, sourceFile, sourceFile, facts);
    }
  }
  return facts;
}

function visit(repoPath: string, manifest: ScanManifest, project: LoadedProject, sourceFile: ts.SourceFile, node: ts.Node, facts: CodeFact[]): void {
  if (ts.isCallExpression(node)) {
    addHttpFact(repoPath, manifest, project, sourceFile, node, facts);
    addExpressRouteFact(repoPath, manifest, project, sourceFile, node, facts);
    addSerializerFact(repoPath, manifest, project, sourceFile, node, facts);
    addZodFact(repoPath, manifest, project, sourceFile, node, facts);
    addPrismaFact(repoPath, manifest, project, sourceFile, node, facts);
  }
  if (ts.isPropertyAccessExpression(node)) {
    addProcessEnvFact(repoPath, manifest, project, sourceFile, node, facts);
  }
  ts.forEachChild(node, (child) => visit(repoPath, manifest, project, sourceFile, child, facts));
}

function addHttpFact(repoPath: string, manifest: ScanManifest, project: LoadedProject, sourceFile: ts.SourceFile, node: ts.CallExpression, facts: CodeFact[]): void {
  const name = callName(node.expression, sourceFile);
  if (name !== "fetch" && name !== "axios" && !name.match(/^(get|post|put|patch|delete)$/i)) {
    return;
  }
  const receiver = ts.isPropertyAccessExpression(node.expression) ? node.expression.expression.getText(sourceFile) : "";
  if (name !== "fetch" && receiver !== "axios") {
    return;
  }
  const url = node.arguments[0];
  const method = name === "fetch" ? methodFromFetch(node, sourceFile) : name.toUpperCase();
  facts.push(
    createFact(
      manifest,
      FactTypes.HttpCallDetected,
      RuleIds.TypeScriptIntegrationHttp,
      packageEvidenceTier(project, node) ?? EvidenceTiers.Tier3SyntaxOrTextual,
      evidence(repoPath, sourceFile, node),
      {
        projectPath: project.projectPath,
        targetSymbol: name,
        contractElement: name,
        properties: {
          name,
          methodName: method,
          targetSymbol: name,
          contractElement: name,
          urlKind: url ? ts.SyntaxKind[url.kind] : "none",
          urlHash: url && ts.isStringLiteralLike(url) ? hash(url.text) : url ? hash(url.getText(sourceFile)) : "",
          valueStored: "hash-only"
        }
      }
    )
  );
}

function addExpressRouteFact(repoPath: string, manifest: ScanManifest, project: LoadedProject, sourceFile: ts.SourceFile, node: ts.CallExpression, facts: CodeFact[]): void {
  if (!ts.isPropertyAccessExpression(node.expression)) {
    return;
  }
  const method = node.expression.name.text.toLowerCase();
  if (!["get", "post", "put", "patch", "delete"].includes(method)) {
    return;
  }
  const route = node.arguments[0];
  if (!route || !ts.isStringLiteralLike(route)) {
    return;
  }
  const receiver = node.expression.expression.getText(sourceFile);
  if (!["app", "router", "routes"].includes(receiver)) {
    return;
  }
  facts.push(
    createFact(
      manifest,
      FactTypes.HttpRouteBinding,
      RuleIds.TypeScriptIntegrationRoute,
      EvidenceTiers.Tier2Structural,
      evidence(repoPath, sourceFile, node),
      {
        projectPath: project.projectPath,
        targetSymbol: `${method.toUpperCase()} ${hash(route.text)}`,
        contractElement: `${method.toUpperCase()} route`,
        properties: {
          methodName: method.toUpperCase(),
          routePatternHash: hash(route.text),
          routePatternLength: route.text.length,
          targetSymbol: `${method.toUpperCase()} ${hash(route.text)}`,
          contractElement: `${method.toUpperCase()} route`
        }
      }
    )
  );
}

function addSerializerFact(repoPath: string, manifest: ScanManifest, project: LoadedProject, sourceFile: ts.SourceFile, node: ts.CallExpression, facts: CodeFact[]): void {
  if (!ts.isPropertyAccessExpression(node.expression) || node.expression.expression.getText(sourceFile) !== "JSON") {
    return;
  }
  const operation = node.expression.name.text;
  if (operation !== "parse" && operation !== "stringify") {
    return;
  }
  facts.push(
    createFact(
      manifest,
      FactTypes.SerializationLogic,
      RuleIds.TypeScriptIntegrationSerializer,
      EvidenceTiers.Tier3SyntaxOrTextual,
      evidence(repoPath, sourceFile, node),
      {
        projectPath: project.projectPath,
        targetSymbol: `JSON.${operation}`,
        contractElement: operation,
        properties: {
          operationName: operation,
          methodName: operation,
          targetSymbol: `JSON.${operation}`,
          contractElement: operation
        }
      }
    )
  );
}

function addZodFact(repoPath: string, manifest: ScanManifest, project: LoadedProject, sourceFile: ts.SourceFile, node: ts.CallExpression, facts: CodeFact[]): void {
  if (!ts.isPropertyAccessExpression(node.expression)) {
    return;
  }
  const receiver = node.expression.expression.getText(sourceFile);
  if (receiver !== "z") {
    return;
  }
  const name = node.expression.name.text;
  if (!["object", "string", "number", "boolean", "array", "enum"].includes(name)) {
    return;
  }
  facts.push(
    createFact(
      manifest,
      FactTypes.SerializerContractMember,
      RuleIds.TypeScriptIntegrationContractMapping,
      EvidenceTiers.Tier2Structural,
      evidence(repoPath, sourceFile, node),
      {
        projectPath: project.projectPath,
        targetSymbol: `z.${name}`,
        contractElement: name,
        properties: {
          name,
          memberName: name,
          targetSymbol: `z.${name}`,
          contractElement: name,
          schemaLibrary: "zod"
        }
      }
    )
  );
}

function addPrismaFact(repoPath: string, manifest: ScanManifest, project: LoadedProject, sourceFile: ts.SourceFile, node: ts.CallExpression, facts: CodeFact[]): void {
  if (!ts.isPropertyAccessExpression(node.expression)) {
    return;
  }
  const operation = node.expression.name.text;
  if (!["findMany", "findUnique", "findFirst", "create", "update", "delete", "upsert"].includes(operation)) {
    return;
  }
  const receiver = node.expression.expression.getText(sourceFile);
  if (!receiver.includes("prisma")) {
    return;
  }
  facts.push(
    createFact(
      manifest,
      FactTypes.DatabaseColumnMapping,
      RuleIds.TypeScriptIntegrationDatabase,
      EvidenceTiers.Tier2Structural,
      evidence(repoPath, sourceFile, node),
      {
        projectPath: project.projectPath,
        targetSymbol: operation,
        contractElement: operation,
        properties: {
          methodName: operation,
          targetSymbol: operation,
          contractElement: operation,
          orm: "prisma",
          receiverHash: hash(receiver)
        }
      }
    )
  );
}

function addProcessEnvFact(repoPath: string, manifest: ScanManifest, project: LoadedProject, sourceFile: ts.SourceFile, node: ts.PropertyAccessExpression, facts: CodeFact[]): void {
  if (!ts.isPropertyAccessExpression(node.expression)) {
    return;
  }
  if (node.expression.expression.getText(sourceFile) !== "process" || node.expression.name.text !== "env") {
    return;
  }
  const key = node.name.text;
  facts.push(
    createFact(
      manifest,
      FactTypes.ConfigKeyDeclared,
      RuleIds.TypeScriptIntegrationBoundary,
      EvidenceTiers.Tier3SyntaxOrTextual,
      evidence(repoPath, sourceFile, node),
      {
        projectPath: project.projectPath,
        targetSymbol: key,
        contractElement: key,
        properties: {
          keyPath: key,
          memberName: key,
          name: key,
          targetSymbol: key
        }
      }
    )
  );
}

function methodFromFetch(node: ts.CallExpression, sourceFile: ts.SourceFile): string {
  const init = node.arguments[1];
  if (init && ts.isObjectLiteralExpression(init)) {
    const method = init.properties.find((prop): prop is ts.PropertyAssignment => ts.isPropertyAssignment(prop) && prop.name.getText(sourceFile) === "method");
    if (method && ts.isStringLiteralLike(method.initializer)) {
      return method.initializer.text.toUpperCase();
    }
  }
  return "GET";
}

function packageEvidenceTier(project: LoadedProject, node: ts.Node): string | null {
  const symbol = project.checker.getSymbolAtLocation(ts.isCallExpression(node) ? node.expression : node);
  if (!symbol) {
    return null;
  }
  return EvidenceTiers.Tier1Semantic;
}

function callName(expression: ts.Expression, sourceFile: ts.SourceFile): string {
  if (ts.isPropertyAccessExpression(expression)) {
    return expression.name.text;
  }
  if (ts.isIdentifier(expression)) {
    return expression.text;
  }
  return expression.getText(sourceFile);
}

function evidence(repoPath: string, sourceFile: ts.SourceFile, node: ts.Node) {
  const start = sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile)).line + 1;
  const end = sourceFile.getLineAndCharacterOfPosition(node.getEnd()).line + 1;
  return createEvidence(repoRelative(repoPath, sourceFile.fileName), start, end, "typescript-integration", ScannerVersions.TypeScriptIntegrationExtractor);
}
