import ts from "typescript";
import { CodeFact, EvidenceTiers, FactTypes, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { hash } from "../util/Hash";

type Presence = "unconditional" | "conditional" | "spread-derived" | "dynamic-computed" | "unresolved";

interface ShapeField {
  name: string;
  presence: Presence;
  expressionType: string;
  origin: string;
}

interface ShapeSpread {
  expressionType: string;
  origin: string;
  resolution: "resolved" | "partial" | "unresolved";
  fieldNames: string[];
}

interface ShapeAnalysis {
  constructionKind: string;
  fields: ShapeField[];
  spreads: ShapeSpread[];
  candidateBindings: string[];
  gaps: string[];
}

interface ShapeContext {
  source: ts.SourceFile;
  callNode: ts.CallExpression;
  callPosition: number;
  declarations: Map<string, ts.VariableDeclaration[]>;
}

export interface EntityShapeInput {
  manifest: ScanManifest;
  node: ts.CallExpression;
  source: ts.SourceFile;
  filePath: string;
  sourceText: string;
  entityName: string;
  operationName: string;
  operationEvidenceId: string;
}

const mutationPayloadIndex = new Map<string, number>([
  ["create", 0],
  ["update", 1],
  ["upsert", 0],
  ["bulkCreate", 0],
  ["createMany", 0],
  ["updateMany", 1]
]);

export function extractEntityShapeFacts(input: EntityShapeInput): CodeFact[] {
  const declarations = collectVariableDeclarations(input.source);
  const context: ShapeContext = { source: input.source, callNode: input.node, callPosition: input.node.getStart(input.source), declarations };
  const payloadIndex = mutationPayloadIndex.get(input.operationName);
  if (payloadIndex !== undefined) {
    const expression = input.node.arguments[payloadIndex];
    const analysis = expression
      ? analyzeExpression(expression, context, new Set(), "unconditional")
      : unresolvedAnalysis("missing", "payload-argument-missing");
    return [shapeFact(input, FactTypes.Base44EntityPayload, RuleIds.Base44EntityPayload, payloadIndex, "payload", analysis)];
  }

  if (!["filter", "list", "get", "delete", "subscribe"].includes(input.operationName)) return [];
  return [queryFact(input, context)];
}

function queryFact(input: EntityShapeInput, context: ShapeContext): CodeFact {
  const accumulated = emptyAnalysis(`${input.operationName}-arguments`);
  const { fields, spreads, candidateBindings, gaps } = accumulated;

  if (input.operationName === "filter") {
    const filter = input.node.arguments[0];
    if (filter) mergeAnalysis(accumulated, analyzeExpression(filter, context, new Set(), "unconditional"), "filter");
    else gaps.push("filter-argument-missing");
    addSortEvidence(input.node.arguments[1], fields, candidateBindings, gaps);
    addSelectEvidence(input.node.arguments[4], fields, candidateBindings, gaps);
  } else if (input.operationName === "list") {
    addSortEvidence(input.node.arguments[0], fields, candidateBindings, gaps);
    addSelectEvidence(input.node.arguments[3], fields, candidateBindings, gaps);
  } else if (input.operationName === "get" || input.operationName === "delete") {
    if (!input.node.arguments[0]) gaps.push("identity-argument-missing");
  } else if (input.operationName === "subscribe" && !input.node.arguments[0]) {
    gaps.push("subscription-callback-missing");
  }

  const analysis: ShapeAnalysis = {
    constructionKind: `${input.operationName}-arguments`,
    fields: normalizeFields(fields),
    spreads: normalizeSpreads(spreads),
    candidateBindings: unique(candidateBindings),
    gaps: unique(gaps)
  };
  return shapeFact(input, FactTypes.Base44EntityQuery, RuleIds.Base44EntityQuery, -1, "query", analysis);
}

function addSortEvidence(expression: ts.Expression | undefined, fields: ShapeField[], bindings: string[], gaps: string[]): void {
  if (!expression || expression.kind === ts.SyntaxKind.UndefinedKeyword) return;
  if (ts.isStringLiteralLike(expression)) {
    const names = expression.text.split(",").map((item) => item.trim().replace(/^[-+]/, "")).filter(isSafeFieldName);
    if (names.length === 0) gaps.push("sort-field-literal-unresolved");
    for (const name of names) fields.push({ name, presence: "unconditional", expressionType: "string-literal", origin: "sort-argument" });
    return;
  }
  if (ts.isArrayLiteralExpression(expression)) {
    for (const element of expression.elements) addSortEvidence(element as ts.Expression, fields, bindings, gaps);
    return;
  }
  const origin = expressionOrigin(expression);
  bindings.push(origin);
  gaps.push("sort-field-dynamic");
}

function addSelectEvidence(expression: ts.Expression | undefined, fields: ShapeField[], bindings: string[], gaps: string[]): void {
  if (!expression || expression.kind === ts.SyntaxKind.UndefinedKeyword) return;
  if (ts.isStringLiteralLike(expression)) {
    const names = expression.text.split(",").map((item) => item.trim()).filter(isSafeFieldName);
    if (names.length === 0) gaps.push("select-field-literal-unresolved");
    for (const name of names) fields.push({ name, presence: "unconditional", expressionType: "string-literal", origin: "select-argument" });
    return;
  }
  if (ts.isArrayLiteralExpression(expression)) {
    for (const element of expression.elements) addSelectEvidence(element as ts.Expression, fields, bindings, gaps);
    return;
  }
  bindings.push(expressionOrigin(expression));
  gaps.push("select-field-dynamic");
}

function analyzeExpression(expression: ts.Expression, context: ShapeContext, visitedBindings: Set<string>, presence: Presence): ShapeAnalysis {
  const unwrapped = unwrapExpression(expression);
  if (ts.isObjectLiteralExpression(unwrapped)) return analyzeObjectLiteral(unwrapped, context, visitedBindings, presence);
  if (ts.isArrayLiteralExpression(unwrapped)) return analyzeArrayLiteral(unwrapped, context, visitedBindings);
  if (ts.isIdentifier(unwrapped)) return analyzeIdentifier(unwrapped, context, visitedBindings, presence);
  if (ts.isConditionalExpression(unwrapped)) {
    const result = emptyAnalysis("conditional-expression");
    mergeAnalysis(result, analyzeExpression(unwrapped.whenTrue, context, new Set(visitedBindings), "conditional"));
    mergeAnalysis(result, analyzeExpression(unwrapped.whenFalse, context, new Set(visitedBindings), "conditional"));
    return result;
  }
  if (ts.isBinaryExpression(unwrapped) && [ts.SyntaxKind.AmpersandAmpersandToken, ts.SyntaxKind.BarBarToken, ts.SyntaxKind.QuestionQuestionToken].includes(unwrapped.operatorToken.kind)) {
    const result = analyzeExpression(unwrapped.right, context, visitedBindings, "conditional");
    result.constructionKind = "conditional-logical-expression";
    return result;
  }
  return unresolvedAnalysis(expressionType(unwrapped), `payload-expression-unresolved:${expressionType(unwrapped)}`, expressionOrigin(unwrapped));
}

function analyzeObjectLiteral(node: ts.ObjectLiteralExpression, context: ShapeContext, visitedBindings: Set<string>, inheritedPresence: Presence): ShapeAnalysis {
  const result = emptyAnalysis("object-literal");
  for (const property of node.properties) {
    if (ts.isSpreadAssignment(property)) {
      const spread = analyzeExpression(property.expression, context, new Set(visitedBindings), "spread-derived");
      const spreadOrigin = expressionOrigin(property.expression);
      result.spreads.push({
        expressionType: expressionType(property.expression),
        origin: spreadOrigin,
        resolution: spread.gaps.length === 0 ? "resolved" : spread.fields.length > 0 ? "partial" : "unresolved",
        fieldNames: unique(spread.fields.map((field) => field.name).filter((name) => name !== "<dynamic>"))
      });
      result.candidateBindings.push(spreadOrigin, ...spread.candidateBindings);
      result.gaps.push(...spread.gaps.map((gap) => `spread:${gap}`));
      for (const field of spread.fields) {
        result.fields.push({ ...field, presence: field.presence === "conditional" ? "conditional" : "spread-derived" });
      }
      result.spreads.push(...spread.spreads);
      continue;
    }
    if (ts.isPropertyAssignment(property)) {
      const name = staticPropertyName(property.name);
      if (!name) {
        result.fields.push({ name: "<dynamic>", presence: "dynamic-computed", expressionType: expressionType(property.initializer), origin: expressionOrigin(property.initializer) });
        result.gaps.push("dynamic-computed-property");
      } else {
        result.fields.push({ name, presence: inheritedPresence, expressionType: expressionType(property.initializer), origin: expressionOrigin(property.initializer) });
      }
      continue;
    }
    if (ts.isShorthandPropertyAssignment(property)) {
      result.fields.push({ name: property.name.text, presence: inheritedPresence, expressionType: "identifier-reference", origin: `binding:${property.name.text}` });
      result.candidateBindings.push(`binding:${property.name.text}`);
      continue;
    }
    if (ts.isMethodDeclaration(property) || ts.isGetAccessorDeclaration(property) || ts.isSetAccessorDeclaration(property)) {
      const name = staticPropertyName(property.name);
      if (name) result.fields.push({ name, presence: inheritedPresence, expressionType: "method", origin: "inline-method" });
      else {
        result.fields.push({ name: "<dynamic>", presence: "dynamic-computed", expressionType: "method", origin: "inline-method" });
        result.gaps.push("dynamic-computed-property");
      }
    }
  }
  return result;
}

function analyzeIdentifier(identifier: ts.Identifier, context: ShapeContext, visitedBindings: Set<string>, presence: Presence): ShapeAnalysis {
  const bindingName = identifier.text;
  const candidate = `binding:${bindingName}`;
  if (visitedBindings.has(bindingName)) return unresolvedAnalysis("identifier-reference", "binding-cycle", candidate);
  const declaration = [...(context.declarations.get(bindingName) ?? [])]
    .filter((item) => item.getStart(context.source) < context.callPosition)
    .filter((item) => isDeclarationVisibleAt(item, context.callNode))
    .sort((left, right) => scopeDepth(declarationScope(right)) - scopeDepth(declarationScope(left))
      || right.getStart(context.source) - left.getStart(context.source))[0];
  if (!declaration?.initializer) return unresolvedAnalysis("identifier-reference", "binding-initializer-unresolved", candidate);
  const nextVisited = new Set(visitedBindings).add(bindingName);
  const result = analyzeExpression(declaration.initializer, context, nextVisited, presence);
  result.constructionKind = `identifier:${result.constructionKind}`;
  result.candidateBindings.push(candidate);
  addBindingMutations(bindingName, declaration, context, result);
  return result;
}

function addBindingMutations(bindingName: string, declaration: ts.VariableDeclaration, context: ShapeContext, result: ShapeAnalysis): void {
  const declarationEnd = declaration.getEnd();
  const visit = (node: ts.Node): void => {
    const position = node.getStart(context.source);
    if (position <= declarationEnd || position >= context.callPosition) return ts.forEachChild(node, visit);
    if (ts.isBinaryExpression(node) && node.operatorToken.kind === ts.SyntaxKind.EqualsToken) {
      const field = assignedField(node.left, bindingName);
      if (field && resolveDeclarationAt(bindingName, node, context) !== declaration) {
        return;
      }
      if (field === "<dynamic>") {
        result.fields.push({ name: field, presence: "dynamic-computed", expressionType: expressionType(node.right), origin: expressionOrigin(node.right) });
        result.gaps.push("dynamic-computed-assignment");
      } else if (field) {
        result.fields.push({ name: field, presence: isConditionallyExecuted(node, context.source) ? "conditional" : "unconditional", expressionType: expressionType(node.right), origin: expressionOrigin(node.right) });
      }
    }
    if (ts.isCallExpression(node) && expressionChain(node.expression)?.join(".") === "Object.assign" && ts.isIdentifier(node.arguments[0]) && node.arguments[0].text === bindingName
      && resolveDeclarationAt(bindingName, node, context) === declaration) {
      for (const source of node.arguments.slice(1)) {
        const spread = analyzeExpression(source, context, new Set([bindingName]), "spread-derived");
        mergeAnalysis(result, spread);
        result.gaps.push(...spread.gaps.map((gap) => `object-assign:${gap}`));
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(context.source);
}

function resolveDeclarationAt(bindingName: string, useNode: ts.Node, context: ShapeContext): ts.VariableDeclaration | null {
  return [...(context.declarations.get(bindingName) ?? [])]
    .filter((item) => item.getStart(context.source) < useNode.getStart(context.source))
    .filter((item) => isDeclarationVisibleAt(item, useNode))
    .sort((left, right) => scopeDepth(declarationScope(right)) - scopeDepth(declarationScope(left))
      || right.getStart(context.source) - left.getStart(context.source))[0] ?? null;
}

function isDeclarationVisibleAt(declaration: ts.VariableDeclaration, useNode: ts.Node): boolean {
  const scope = declarationScope(declaration);
  return isAncestor(scope, useNode);
}

function declarationScope(declaration: ts.VariableDeclaration): ts.Node {
  const declarationList = declaration.parent;
  const functionScoped = ts.isVariableDeclarationList(declarationList)
    && (declarationList.flags & (ts.NodeFlags.Let | ts.NodeFlags.Const)) === 0;
  for (let current: ts.Node | undefined = declaration.parent; current; current = current.parent) {
    if (functionScoped && (ts.isFunctionLike(current) || ts.isSourceFile(current))) return current;
    if (!functionScoped && (ts.isBlock(current) || ts.isSourceFile(current) || ts.isCaseBlock(current)
      || ts.isForStatement(current) || ts.isForInStatement(current) || ts.isForOfStatement(current))) return current;
  }
  return declaration.getSourceFile();
}

function isAncestor(ancestor: ts.Node, node: ts.Node): boolean {
  for (let current: ts.Node | undefined = node; current; current = current.parent) if (current === ancestor) return true;
  return false;
}

function scopeDepth(node: ts.Node): number {
  let depth = 0;
  for (let current: ts.Node | undefined = node; current; current = current.parent) depth += 1;
  return depth;
}

function analyzeArrayLiteral(node: ts.ArrayLiteralExpression, context: ShapeContext, visitedBindings: Set<string>): ShapeAnalysis {
  const result = emptyAnalysis("array-literal");
  if (node.elements.length === 0) return result;
  const perElement: ShapeAnalysis[] = [];
  for (const element of node.elements) {
    if (ts.isSpreadElement(element)) {
      result.spreads.push({ expressionType: expressionType(element.expression), origin: expressionOrigin(element.expression), resolution: "unresolved", fieldNames: [] });
      result.candidateBindings.push(expressionOrigin(element.expression));
      result.gaps.push("array-spread-unresolved");
      continue;
    }
    perElement.push(analyzeExpression(element as ts.Expression, context, new Set(visitedBindings), "unconditional"));
  }
  const elementCount = perElement.length;
  const occurrences = new Map<string, number>();
  for (const analysis of perElement) {
    for (const field of normalizeFields(analysis.fields)) occurrences.set(field.name, (occurrences.get(field.name) ?? 0) + 1);
    mergeAnalysis(result, analysis);
  }
  result.fields = result.fields.map((field) => ({
    ...field,
    presence: occurrences.get(field.name) === elementCount && field.presence === "unconditional" ? "unconditional" : "conditional"
  }));
  return result;
}

function shapeFact(input: EntityShapeInput, factType: string, ruleId: string, argumentIndex: number, argumentRole: string, analysis: ShapeAnalysis): CodeFact {
  const fields = normalizeFields(analysis.fields);
  const spreads = normalizeSpreads(analysis.spreads);
  const gaps = unique(analysis.gaps);
  const completeness = gaps.length === 0 ? "complete" : fields.length > 0 ? "partial" : "unresolved";
  const start = input.source.getLineAndCharacterOfPosition(input.node.getStart(input.source)).line + 1;
  const end = input.source.getLineAndCharacterOfPosition(input.node.getEnd()).line + 1;
  return createFact(
    input.manifest,
    factType,
    ruleId,
    completeness === "complete" ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown,
    createEvidence(input.filePath, start, end, "base44-evidence", ScannerVersions.Base44EvidenceExtractor, hash(input.node.getText(input.source), 64)),
    {
      targetSymbol: input.entityName,
      contractElement: `${input.entityName}.${input.operationName}`,
      properties: {
        analysisGapsJson: stableArray(gaps),
        argumentIndex,
        argumentRole,
        candidateBindingsJson: stableArray(unique(analysis.candidateBindings)),
        completeness,
        constructionKind: analysis.constructionKind,
        entityName: input.entityName,
        fieldsJson: stableArray(fields),
        operationEvidenceId: input.operationEvidenceId,
        operationName: input.operationName,
        shapeVersion: "1",
        sourceFileSha256: hash(input.sourceText, 64),
        spreadsJson: stableArray(spreads)
      }
    }
  );
}

function collectVariableDeclarations(source: ts.SourceFile): Map<string, ts.VariableDeclaration[]> {
  const declarations = new Map<string, ts.VariableDeclaration[]>();
  const visit = (node: ts.Node): void => {
    if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name)) {
      const current = declarations.get(node.name.text) ?? [];
      current.push(node);
      declarations.set(node.name.text, current);
    }
    ts.forEachChild(node, visit);
  };
  visit(source);
  return declarations;
}

function assignedField(left: ts.Expression, bindingName: string): string | null {
  if (ts.isPropertyAccessExpression(left) && ts.isIdentifier(left.expression) && left.expression.text === bindingName) return left.name.text;
  if (ts.isElementAccessExpression(left) && ts.isIdentifier(left.expression) && left.expression.text === bindingName) {
    return left.argumentExpression && ts.isStringLiteralLike(left.argumentExpression) ? left.argumentExpression.text : "<dynamic>";
  }
  return null;
}

function isConditionallyExecuted(node: ts.Node, source: ts.SourceFile): boolean {
  for (let parent = node.parent; parent && parent !== source; parent = parent.parent) {
    if (ts.isIfStatement(parent) || ts.isConditionalExpression(parent) || ts.isSwitchStatement(parent)
      || ts.isForStatement(parent) || ts.isForInStatement(parent) || ts.isForOfStatement(parent)
      || ts.isWhileStatement(parent) || ts.isDoStatement(parent) || ts.isTryStatement(parent)) return true;
  }
  return false;
}

function staticPropertyName(name: ts.PropertyName): string | null {
  if (ts.isIdentifier(name) || ts.isStringLiteralLike(name) || ts.isNumericLiteral(name)) return name.text;
  if (ts.isComputedPropertyName(name) && ts.isStringLiteralLike(name.expression)) return name.expression.text;
  return null;
}

function expressionType(expression: ts.Expression): string {
  const node = unwrapExpression(expression);
  if (ts.isStringLiteralLike(node)) return "string-literal";
  if (ts.isNumericLiteral(node)) return /[.eE]/.test(node.getText()) ? "decimal-number-literal" : "integer-number-literal";
  if (node.kind === ts.SyntaxKind.TrueKeyword || node.kind === ts.SyntaxKind.FalseKeyword) return "boolean-literal";
  if (node.kind === ts.SyntaxKind.NullKeyword) return "null-literal";
  if (ts.isObjectLiteralExpression(node)) return "object-literal";
  if (ts.isArrayLiteralExpression(node)) return "array-literal";
  if (ts.isTemplateExpression(node)) return "template-expression";
  if (ts.isIdentifier(node)) return "identifier-reference";
  if (ts.isPropertyAccessExpression(node) || ts.isElementAccessExpression(node)) return "property-access";
  if (ts.isCallExpression(node)) {
    const callee = expressionChain(node.expression)?.at(-1);
    return callee && ["Number", "parseFloat", "parseInt"].includes(callee) ? "number-coercion-call" : "call-expression";
  }
  if (ts.isBinaryExpression(node)) {
    return [ts.SyntaxKind.MinusToken, ts.SyntaxKind.AsteriskToken, ts.SyntaxKind.SlashToken, ts.SyntaxKind.PercentToken, ts.SyntaxKind.AsteriskAsteriskToken].includes(node.operatorToken.kind)
      ? "numeric-binary-expression"
      : "binary-expression";
  }
  if (ts.isConditionalExpression(node)) return "conditional-expression";
  if (ts.isArrowFunction(node) || ts.isFunctionExpression(node)) return "function-expression";
  if (ts.isPrefixUnaryExpression(node) || ts.isPostfixUnaryExpression(node)) return "unary-expression";
  return `syntax-${ts.SyntaxKind[node.kind] ?? "unknown"}`;
}

function expressionOrigin(expression: ts.Expression): string {
  const node = unwrapExpression(expression);
  if (ts.isIdentifier(node)) return `binding:${node.text}`;
  if (ts.isPropertyAccessExpression(node) || ts.isElementAccessExpression(node)) {
    const chain = expressionChain(node);
    return chain ? `property:${chain.join(".")}` : "property:dynamic";
  }
  if (ts.isCallExpression(node)) {
    const chain = expressionChain(node.expression);
    return chain ? `call:${chain.join(".")}` : "call:dynamic";
  }
  if (ts.isStringLiteralLike(node) || ts.isNumericLiteral(node) || node.kind === ts.SyntaxKind.TrueKeyword || node.kind === ts.SyntaxKind.FalseKeyword || node.kind === ts.SyntaxKind.NullKeyword) return "literal";
  return `expression:${expressionType(node)}`;
}

function expressionChain(expression: ts.Expression): string[] | null {
  if (ts.isIdentifier(expression)) return [expression.text];
  if (ts.isPropertyAccessExpression(expression)) {
    const parent = expressionChain(expression.expression);
    return parent ? [...parent, expression.name.text] : null;
  }
  if (ts.isElementAccessExpression(expression) && expression.argumentExpression && ts.isStringLiteralLike(expression.argumentExpression)) {
    const parent = expressionChain(expression.expression);
    return parent ? [...parent, expression.argumentExpression.text] : null;
  }
  return null;
}

function unwrapExpression(expression: ts.Expression): ts.Expression {
  let current = expression;
  while (ts.isParenthesizedExpression(current) || ts.isAsExpression(current) || ts.isTypeAssertionExpression(current) || ts.isNonNullExpression(current) || ts.isSatisfiesExpression(current)) current = current.expression;
  return current;
}

function mergeAnalysis(target: ShapeAnalysis, source: ShapeAnalysis, originPrefix?: string): void {
  target.fields.push(...source.fields.map((field) => originPrefix ? { ...field, origin: `${originPrefix}:${field.origin}` } : field));
  target.spreads.push(...source.spreads);
  target.candidateBindings.push(...source.candidateBindings);
  target.gaps.push(...source.gaps);
}

function emptyAnalysis(constructionKind: string): ShapeAnalysis {
  return { constructionKind, fields: [], spreads: [], candidateBindings: [], gaps: [] };
}

function unresolvedAnalysis(constructionKind: string, gap: string, binding?: string): ShapeAnalysis {
  return { constructionKind, fields: [], spreads: [], candidateBindings: binding ? [binding] : [], gaps: [gap] };
}

function normalizeFields(fields: ShapeField[]): ShapeField[] {
  const byValue = new Map<string, ShapeField>();
  for (const field of fields) byValue.set(JSON.stringify(field), field);
  return [...byValue.values()].sort((left, right) => left.name.localeCompare(right.name)
    || left.presence.localeCompare(right.presence)
    || left.expressionType.localeCompare(right.expressionType)
    || left.origin.localeCompare(right.origin));
}

function normalizeSpreads(spreads: ShapeSpread[]): ShapeSpread[] {
  return spreads.map((spread) => ({ ...spread, fieldNames: unique(spread.fieldNames) }))
    .sort((left, right) => left.origin.localeCompare(right.origin) || left.expressionType.localeCompare(right.expressionType));
}

function unique(values: string[]): string[] { return [...new Set(values)].sort((left, right) => left.localeCompare(right)); }
function stableArray(value: unknown[]): string { return JSON.stringify(value); }
function isSafeFieldName(value: string): boolean { return /^[A-Za-z_$][A-Za-z0-9_$]*$/.test(value); }
