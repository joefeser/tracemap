import ts from "typescript";
import fs from "node:fs";
import path from "node:path";
import { CodeFact, EvidenceTiers, FactTypes, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { hash } from "../util/Hash";
import { repoRelative } from "../util/Paths";
import { LoadedProject } from "./TypeScriptProjectLoader";
import { normalizeEndpointRoute } from "../endpoints/RouteTemplateNormalizer";

interface VisitContext {
  httpClientNames: Set<string>;
  environmentBases: Map<string, string>;
}

interface UrlAnalysis {
  urlKind: string;
  dynamicReason: string;
  routeTemplate: string;
  baseUrlSymbol: string;
  basePathPrefix: string;
  urlHash: string;
  responseType: string;
}

export function extractIntegrationFacts(repoPath: string, manifest: ScanManifest, projects: readonly LoadedProject[]): CodeFact[] {
  const facts: CodeFact[] = [];
  const environmentBases = readEnvironmentBasePaths(repoPath);
  for (const project of projects) {
    for (const sourceFile of project.sourceFiles) {
      const context: VisitContext = {
        httpClientNames: collectHttpClientReceivers(sourceFile),
        environmentBases
      };
      visit(repoPath, manifest, project, sourceFile, sourceFile, facts, context);
    }
  }
  return facts;
}

function visit(repoPath: string, manifest: ScanManifest, project: LoadedProject, sourceFile: ts.SourceFile, node: ts.Node, facts: CodeFact[], context: VisitContext): void {
  if (ts.isCallExpression(node)) {
    addAngularHttpClientFact(repoPath, manifest, project, sourceFile, node, facts, context);
    addHttpFact(repoPath, manifest, project, sourceFile, node, facts);
    addExpressRouteFact(repoPath, manifest, project, sourceFile, node, facts);
    addSerializerFact(repoPath, manifest, project, sourceFile, node, facts);
    addZodFact(repoPath, manifest, project, sourceFile, node, facts);
    addPrismaFact(repoPath, manifest, project, sourceFile, node, facts);
    addEntityApiFact(repoPath, manifest, project, sourceFile, node, facts);
  }
  if (ts.isPropertyAccessExpression(node)) {
    addProcessEnvFact(repoPath, manifest, project, sourceFile, node, facts);
  }
  ts.forEachChild(node, (child) => visit(repoPath, manifest, project, sourceFile, child, facts, context));
}

function addAngularHttpClientFact(repoPath: string, manifest: ScanManifest, project: LoadedProject, sourceFile: ts.SourceFile, node: ts.CallExpression, facts: CodeFact[], context: VisitContext): void {
  if (!ts.isPropertyAccessExpression(node.expression)) {
    return;
  }

  const verb = node.expression.name.text.toLowerCase();
  if (!["get", "post", "put", "patch", "delete", "head", "options", "request"].includes(verb)) {
    return;
  }

  const receiver = node.expression.expression.getText(sourceFile);
  if (!isHttpClientReceiver(receiver, context.httpClientNames)) {
    return;
  }

  const methodArg = verb === "request" ? node.arguments[0] : undefined;
  const urlArg = verb === "request" ? node.arguments[1] : node.arguments[0];
  if (!urlArg) {
    return;
  }

  const method = verb === "request" && methodArg && ts.isStringLiteralLike(methodArg)
    ? methodArg.text.toUpperCase()
    : verb.toUpperCase();
  const analysis = analyzeUrlExpression(urlArg, sourceFile, context.environmentBases);
  const normalized = analysis.routeTemplate
    ? normalizeEndpointRoute(analysis.routeTemplate, analysis.basePathPrefix)
    : null;
  const properties: Record<string, string> = {
    name: "HttpClient." + verb,
    methodName: method,
    httpMethod: method,
    urlKind: analysis.urlKind,
    dynamicReason: analysis.dynamicReason,
    urlHash: analysis.urlHash,
    valueStored: "hash-only",
    clientFramework: "angular",
    baseUrlSymbol: analysis.baseUrlSymbol,
    basePathPrefix: analysis.basePathPrefix,
    sourceClass: containingClass(sourceFile, node),
    sourceMethod: containingFunction(sourceFile, node),
    responseType: analysis.responseType,
    targetSymbol: `HttpClient.${verb}`,
    contractElement: method
  };
  if (normalized) {
    properties.normalizedPathTemplate = normalized.pathTemplate;
    properties.normalizedPathKey = normalized.pathKey;
    properties.pathParameterNames = normalized.parameterNames.join(";");
    properties.queryParameterNames = normalized.queryParameterNames.join(";");
    properties.hasQueryParameters = String(normalized.hasQueryParameters);
    properties.staticMatchQuality = normalized.staticMatchQuality;
  }

  facts.push(
    createFact(
      manifest,
      FactTypes.HttpCallDetected,
      RuleIds.TypeScriptIntegrationAngularHttpClient,
      context.httpClientNames.size > 0 ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier3SyntaxOrTextual,
      createEvidence(repoRelative(repoPath, sourceFile.fileName), sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile)).line + 1, sourceFile.getLineAndCharacterOfPosition(node.getEnd()).line + 1, "typescript-angular-httpclient", ScannerVersions.TypeScriptAngularHttpClientExtractor),
      {
        projectPath: project.projectPath,
        targetSymbol: `HttpClient.${verb}`,
        contractElement: method,
        properties
      }
    )
  );
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
  const pattern = extractPrismaPattern(node, sourceFile);
  if (pattern.fieldCount > 0) {
    facts.push(
      createFact(
        manifest,
        FactTypes.QueryPatternDetected,
        RuleIds.TypeScriptIntegrationQueryPattern,
        EvidenceTiers.Tier2Structural,
        evidence(repoPath, sourceFile, node),
        {
          projectPath: project.projectPath,
          targetSymbol: operation,
          contractElement: operation,
          properties: {
            operationName: operation,
            receiverHash: hash(receiver),
            filterFields: pattern.filterFields.join(";"),
            sortFields: pattern.sortFields.join(";"),
            selectFields: pattern.selectFields.join(";"),
            mutationFields: pattern.mutationFields.join(";"),
            includeFields: pattern.includeFields.join(";"),
            fieldCount: pattern.fieldCount,
            patternHash: pattern.patternHash,
            orm: "prisma"
          }
        }
      )
    );
  }
}

function addEntityApiFact(repoPath: string, manifest: ScanManifest, project: LoadedProject, sourceFile: ts.SourceFile, node: ts.CallExpression, facts: CodeFact[]): void {
  if (!ts.isPropertyAccessExpression(node.expression)) {
    return;
  }
  const operation = node.expression.name.text;
  if (!["filter", "list", "get", "create", "update", "delete", "upsert", "bulkCreate"].includes(operation)) {
    return;
  }
  const receiver = node.expression.expression.getText(sourceFile);
  if (!receiver.includes(".entities.") && !receiver.startsWith("base44.entities.")) {
    return;
  }
  const pattern = extractEntityApiPattern(operation, node, sourceFile);
  if (pattern.fieldCount === 0) {
    return;
  }
  facts.push(
    createFact(
      manifest,
      FactTypes.QueryPatternDetected,
      RuleIds.TypeScriptIntegrationQueryPattern,
      EvidenceTiers.Tier2Structural,
      evidence(repoPath, sourceFile, node),
      {
        projectPath: project.projectPath,
        targetSymbol: operation,
        contractElement: operation,
        properties: {
          operationName: operation,
          receiverHash: hash(receiver),
          entityName: entityNameFromReceiver(receiver),
          filterFields: pattern.filterFields.join(";"),
          sortFields: pattern.sortFields.join(";"),
          selectFields: pattern.selectFields.join(";"),
          mutationFields: pattern.mutationFields.join(";"),
          includeFields: pattern.includeFields.join(";"),
          fieldCount: pattern.fieldCount,
          patternHash: pattern.patternHash,
          integration: "base44-entity"
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

function collectHttpClientReceivers(sourceFile: ts.SourceFile): Set<string> {
  const receivers = new Set<string>();
  let importsHttpClient = false;
  for (const statement of sourceFile.statements) {
    if (!ts.isImportDeclaration(statement) || !statement.importClause?.namedBindings || !ts.isNamedImports(statement.importClause.namedBindings)) {
      continue;
    }
    for (const element of statement.importClause.namedBindings.elements) {
      if ((element.propertyName?.text ?? element.name.text) === "HttpClient") {
        importsHttpClient = true;
      }
    }
  }

  const visit = (node: ts.Node): void => {
    if (ts.isParameter(node) && node.type?.getText(sourceFile).endsWith("HttpClient")) {
      receivers.add(node.name.getText(sourceFile));
      receivers.add(`this.${node.name.getText(sourceFile)}`);
    }
    if (ts.isPropertyDeclaration(node) && node.type?.getText(sourceFile).endsWith("HttpClient") && ts.isIdentifier(node.name)) {
      receivers.add(node.name.text);
      receivers.add(`this.${node.name.text}`);
    }
    ts.forEachChild(node, visit);
  };
  if (importsHttpClient) {
    visit(sourceFile);
  }
  return receivers;
}

function isHttpClientReceiver(receiver: string, knownReceivers: Set<string>): boolean {
  if (knownReceivers.has(receiver)) {
    return true;
  }
  return /(^|\.)(http|httpClient)$/.test(receiver);
}

function analyzeUrlExpression(expression: ts.Expression, sourceFile: ts.SourceFile, environmentBases: Map<string, string>): UrlAnalysis {
  const responseType = "";
  if (ts.isStringLiteralLike(expression)) {
    return {
      urlKind: "literal",
      dynamicReason: "",
      routeTemplate: expression.text,
      baseUrlSymbol: "",
      basePathPrefix: "",
      urlHash: hash(expression.text),
      responseType
    };
  }
  if (ts.isNoSubstitutionTemplateLiteral(expression)) {
    return {
      urlKind: "template",
      dynamicReason: "",
      routeTemplate: expression.text,
      baseUrlSymbol: "",
      basePathPrefix: "",
      urlHash: hash(expression.text),
      responseType
    };
  }
  if (ts.isTemplateExpression(expression)) {
    const template = templateExpressionToRoute(expression, sourceFile, environmentBases);
    return {
      urlKind: template.dynamicReason ? "dynamic" : "template",
      dynamicReason: template.dynamicReason,
      routeTemplate: template.routeTemplate,
      baseUrlSymbol: template.baseUrlSymbol,
      basePathPrefix: template.basePathPrefix,
      urlHash: hash(expression.getText(sourceFile)),
      responseType
    };
  }
  if (ts.isBinaryExpression(expression) && expression.operatorToken.kind === ts.SyntaxKind.PlusToken) {
    const concat = concatenateUrlExpression(expression, sourceFile, environmentBases);
    return {
      urlKind: concat.dynamicReason ? "dynamic" : "template",
      dynamicReason: concat.dynamicReason || "VariableConcatenation",
      routeTemplate: concat.routeTemplate,
      baseUrlSymbol: concat.baseUrlSymbol,
      basePathPrefix: concat.basePathPrefix,
      urlHash: hash(expression.getText(sourceFile)),
      responseType
    };
  }
  return {
    urlKind: "dynamic",
    dynamicReason: ts.isCallExpression(expression) ? "HelperFunctionCall" : "ComplexExpression",
    routeTemplate: "",
    baseUrlSymbol: "",
    basePathPrefix: "",
    urlHash: hash(expression.getText(sourceFile)),
    responseType
  };
}

function templateExpressionToRoute(expression: ts.TemplateExpression, sourceFile: ts.SourceFile, environmentBases: Map<string, string>): { routeTemplate: string; baseUrlSymbol: string; basePathPrefix: string; dynamicReason: string } {
  let routeTemplate = expression.head.text;
  let baseUrlSymbol = "";
  let basePathPrefix = "";
  let dynamicReason = "";
  for (const span of expression.templateSpans) {
    const resolved = expressionToTemplatePart(span.expression, sourceFile, environmentBases);
    if (resolved.kind === "base") {
      baseUrlSymbol = resolved.name;
      basePathPrefix = resolved.basePathPrefix;
    } else if (resolved.kind === "param") {
      routeTemplate += `{${resolved.name}}`;
    } else {
      dynamicReason = "TemplateExpressionNotResolvable";
    }
    routeTemplate += span.literal.text;
  }
  return { routeTemplate, baseUrlSymbol, basePathPrefix, dynamicReason };
}

function concatenateUrlExpression(expression: ts.Expression, sourceFile: ts.SourceFile, environmentBases: Map<string, string>): { routeTemplate: string; baseUrlSymbol: string; basePathPrefix: string; dynamicReason: string } {
  const parts = flattenPlusExpression(expression);
  let routeTemplate = "";
  let baseUrlSymbol = "";
  let basePathPrefix = "";
  let dynamicReason = "";
  for (const part of parts) {
    if (ts.isStringLiteralLike(part) || ts.isNoSubstitutionTemplateLiteral(part)) {
      routeTemplate += part.text;
      continue;
    }
    const resolved = expressionToTemplatePart(part, sourceFile, environmentBases);
    if (resolved.kind === "base") {
      baseUrlSymbol = resolved.name;
      basePathPrefix = resolved.basePathPrefix;
    } else if (resolved.kind === "param") {
      routeTemplate += `{${resolved.name}}`;
    } else {
      dynamicReason = "VariableConcatenation";
    }
  }
  return { routeTemplate, baseUrlSymbol, basePathPrefix, dynamicReason };
}

function flattenPlusExpression(expression: ts.Expression): ts.Expression[] {
  if (ts.isBinaryExpression(expression) && expression.operatorToken.kind === ts.SyntaxKind.PlusToken) {
    return [...flattenPlusExpression(expression.left), ...flattenPlusExpression(expression.right)];
  }
  return [expression];
}

function expressionToTemplatePart(expression: ts.Expression, sourceFile: ts.SourceFile, environmentBases: Map<string, string>): { kind: "base"; name: string; basePathPrefix: string } | { kind: "param"; name: string } | { kind: "unknown" } {
  const text = expression.getText(sourceFile);
  if (environmentBases.has(text)) {
    return { kind: "base", name: text, basePathPrefix: environmentBases.get(text) ?? "" };
  }
  if (ts.isIdentifier(expression)) {
    return { kind: "param", name: expression.text };
  }
  if (ts.isPropertyAccessExpression(expression)) {
    return { kind: "param", name: expression.name.text };
  }
  return { kind: "unknown" };
}

function readEnvironmentBasePaths(repoPath: string): Map<string, string> {
  const result = new Map<string, string>();
  const envDir = path.join(repoPath, "src", "environments");
  if (!fs.existsSync(envDir)) {
    return result;
  }
  try {
    for (const file of fs.readdirSync(envDir).filter((name) => /^environment.*\.ts$/.test(name)).sort()) {
      const fullPath = path.join(envDir, file);
      try {
        const text = fs.readFileSync(fullPath, "utf8");
        for (const match of text.matchAll(/([A-Za-z_$][\w$]*(?:Uri|Url|BasePath|BaseUrl))\s*:\s*["']([^"']+)["']/g)) {
          const key = match[1];
          const value = match[2];
          const pathSuffix = pathSuffixFromUrl(value);
          result.set(`environment.${key}`, pathSuffix);
        }
      } catch {
        continue;
      }
    }
  } catch {
    return result;
  }
  return result;
}

function pathSuffixFromUrl(value: string): string {
  try {
    return new URL(value).pathname || "/";
  } catch {
    return value.startsWith("/") ? value : "";
  }
}

function containingClass(sourceFile: ts.SourceFile, node: ts.Node): string {
  let current: ts.Node | undefined = node.parent;
  while (current) {
    if (ts.isClassDeclaration(current)) {
      return current.name?.text ?? "";
    }
    current = current.parent;
  }
  return "";
}

function containingFunction(sourceFile: ts.SourceFile, node: ts.Node): string {
  let current: ts.Node | undefined = node.parent;
  while (current) {
    if ((ts.isMethodDeclaration(current) || ts.isFunctionDeclaration(current)) && current.name) {
      return current.name.getText(sourceFile);
    }
    if (ts.isArrowFunction(current) || ts.isFunctionExpression(current)) {
      return "anonymous";
    }
    current = current.parent;
  }
  return "";
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
  return ts.SyntaxKind[expression.kind];
}

function evidence(repoPath: string, sourceFile: ts.SourceFile, node: ts.Node) {
  const start = sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile)).line + 1;
  const end = sourceFile.getLineAndCharacterOfPosition(node.getEnd()).line + 1;
  return createEvidence(repoRelative(repoPath, sourceFile.fileName), start, end, "typescript-integration", ScannerVersions.TypeScriptIntegrationExtractor);
}

function extractPrismaPattern(node: ts.CallExpression, sourceFile: ts.SourceFile): {
  filterFields: string[];
  sortFields: string[];
  selectFields: string[];
  mutationFields: string[];
  includeFields: string[];
  fieldCount: number;
  patternHash: string;
} {
  const firstObject = node.arguments.find(ts.isObjectLiteralExpression);
  const empty = { filterFields: [], sortFields: [], selectFields: [], mutationFields: [], includeFields: [], fieldCount: 0, patternHash: "" };
  if (!firstObject) {
    return empty;
  }
  const sectionFields = (section: string) => {
    const value = objectProperty(firstObject, section, sourceFile);
    return value && ts.isObjectLiteralExpression(value) ? objectLiteralFieldNames(value, sourceFile) : [];
  };
  const filterFields = sectionFields("where");
  const sortFields = sectionFields("orderBy");
  const selectFields = sectionFields("select");
  const mutationFields = sectionFields("data");
  const includeFields = sectionFields("include");
  const allFields = [...filterFields, ...sortFields, ...selectFields, ...mutationFields, ...includeFields];
  return {
    filterFields,
    sortFields,
    selectFields,
    mutationFields,
    includeFields,
    fieldCount: allFields.length,
    patternHash: hash(allFields.sort().join("|"))
  };
}

function extractEntityApiPattern(operation: string, node: ts.CallExpression, sourceFile: ts.SourceFile): {
  filterFields: string[];
  sortFields: string[];
  selectFields: string[];
  mutationFields: string[];
  includeFields: string[];
  fieldCount: number;
  patternHash: string;
} {
  const objectArgs = node.arguments.filter(ts.isObjectLiteralExpression);
  const filterSource = ["filter", "get", "delete"].includes(operation) ? objectArgs[0] : undefined;
  const mutationSource = ["create", "bulkCreate"].includes(operation)
    ? objectArgs[0]
    : ["update", "upsert"].includes(operation) ? objectArgs[1] ?? objectArgs[0] : undefined;
  const filterFields = filterSource ? objectLiteralFieldNames(filterSource, sourceFile) : [];
  const mutationFields = mutationSource ? objectLiteralFieldNames(mutationSource, sourceFile) : [];
  const sortFields = ["filter", "list"].includes(operation)
    ? node.arguments.flatMap((argument, index) => index === 0 && operation === "filter" ? [] : sortFieldNames(argument, sourceFile))
    : [];
  const selectFields: string[] = [];
  const includeFields: string[] = [];
  const allFields = [...filterFields, ...sortFields, ...selectFields, ...mutationFields, ...includeFields];
  return {
    filterFields,
    sortFields,
    selectFields,
    mutationFields,
    includeFields,
    fieldCount: allFields.length,
    patternHash: hash([operation, ...allFields.sort()].join("|"))
  };
}

function sortFieldNames(node: ts.Expression, sourceFile: ts.SourceFile): string[] {
  if (ts.isStringLiteralLike(node)) {
    const field = node.text.replace(/^-/, "").trim();
    return field.length > 0 ? [field] : [];
  }
  if (ts.isObjectLiteralExpression(node)) {
    return objectLiteralFieldNames(node, sourceFile);
  }
  if (ts.isArrayLiteralExpression(node)) {
    return node.elements.flatMap((element) => sortFieldNames(element, sourceFile));
  }
  return [];
}

function entityNameFromReceiver(receiver: string): string {
  const match = receiver.match(/(?:^|\.)entities\.([A-Za-z_$][\w$]*)/);
  return match?.[1] ?? "";
}

function objectProperty(node: ts.ObjectLiteralExpression, name: string, sourceFile: ts.SourceFile): ts.Expression | null {
  for (const property of node.properties) {
    if (!ts.isPropertyAssignment(property)) {
      continue;
    }
    const propertyName = propertyNameText(property.name);
    if (propertyName === name) {
      return property.initializer;
    }
  }
  return null;
}

function objectLiteralFieldNames(node: ts.ObjectLiteralExpression, sourceFile: ts.SourceFile): string[] {
  return [...new Set(node.properties
    .map((property) => {
      if (ts.isPropertyAssignment(property) || ts.isShorthandPropertyAssignment(property) || ts.isMethodDeclaration(property)) {
        return property.name ? propertyNameText(property.name) : undefined;
      }
      return undefined;
    })
    .filter((name): name is string => !!name && name.length > 0))]
    .sort((left, right) => left.localeCompare(right));
}

function propertyNameText(name: ts.PropertyName): string {
  if (ts.isIdentifier(name) || ts.isStringLiteral(name) || ts.isNumericLiteral(name)) {
    return name.text;
  }
  if (ts.isPrivateIdentifier(name)) {
    return name.text;
  }
  return "computed";
}
