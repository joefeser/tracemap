import ts from "typescript";
import { extractQuerySemantics } from "./Base44QuerySemantics";
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
  evidenceStartLine: number;
  evidenceEndLine: number;
  evidenceSnippetHash: string;
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
  callNode: ts.Node;
  callPosition: number;
  declarations: Map<string, ts.VariableDeclaration[]>;
}

interface ParameterBinding {
  parameter: ts.ParameterDeclaration;
  bindingName: string;
  propertyPath: string[];
  gap?: string;
}

interface MutationHookContext {
  declaration: ts.VariableDeclaration;
  bindingName: string;
  gap?: string;
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
  ["bulkCreate", 0]
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

  if (input.operationName === "upsert") {
    const payloadIndex = input.node.arguments.findIndex((argument) => ts.isObjectLiteralExpression(unwrapExpression(argument)));
    const analysis = payloadIndex >= 0
      ? analyzeExpression(input.node.arguments[payloadIndex], context, new Set(), "unconditional")
      : unresolvedAnalysis("upsert-unpinned", "upsert-argument-contract-unpinned");
    analysis.gaps.push("upsert-argument-contract-unpinned");
    return [shapeFact(input, FactTypes.Base44EntityPayload, RuleIds.Base44EntityPayload, payloadIndex, "payload", analysis)];
  }

  if (input.operationName === "importEntities") {
    const expression = input.node.arguments[0];
    const analysis = expression
      ? unresolvedAnalysis(expressionType(expression), "import-file-payload-not-entity-shape", expressionOrigin(expression))
      : unresolvedAnalysis("missing", "import-file-argument-missing");
    return [shapeFact(input, FactTypes.Base44EntityPayload, RuleIds.Base44EntityPayload, 0, "import-file", analysis)];
  }

  if (!["filter", "list", "get", "delete", "deleteMany", "subscribe"].includes(input.operationName)) return [];
  return [queryFact(input, context)];
}

function queryFact(input: EntityShapeInput, context: ShapeContext): CodeFact {
  const accumulated = emptyAnalysis(`${input.operationName}-arguments`);
  const { fields, spreads, candidateBindings, gaps } = accumulated;
  const querySemantics = extractQuerySemantics(input.node, input.source, input.operationName);
  if (querySemantics?.completeness === "unresolved") gaps.push(...querySemantics.gaps);

  if (input.operationName === "filter" || input.operationName === "deleteMany") {
    const filter = input.node.arguments[0];
    if (filter) mergeAnalysis(accumulated, analyzeExpression(filter, context, new Set(), "unconditional"), "filter");
    else gaps.push("filter-argument-missing");
    if (input.operationName === "filter") {
      addSortEvidence(input.node.arguments[1], fields, candidateBindings, gaps, context);
      addSelectEvidence(input.node.arguments[4], fields, candidateBindings, gaps, context);
    }
  } else if (input.operationName === "list") {
    addSortEvidence(input.node.arguments[0], fields, candidateBindings, gaps, context);
    addSelectEvidence(input.node.arguments[3], fields, candidateBindings, gaps, context);
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
  return shapeFact(input, FactTypes.Base44EntityQuery, RuleIds.Base44EntityQuery, -1, "query", analysis, querySemantics);
}

function addSortEvidence(expression: ts.Expression | undefined, fields: ShapeField[], bindings: string[], gaps: string[], context?: ShapeContext): void {
  if (!expression) return;
  expression = unwrapExpression(expression);
  if (expression.kind === ts.SyntaxKind.UndefinedKeyword || expression.kind === ts.SyntaxKind.NullKeyword) return;
  if (ts.isStringLiteralLike(expression)) {
    const names = expression.text.split(",").map((item) => item.trim().replace(/^[-+]/, "")).filter(isSafeFieldName);
    if (names.length === 0) gaps.push("sort-field-literal-unresolved");
    for (const name of names) fields.push(fieldEvidence(name, "unconditional", "string-literal", "sort-argument", expression, context));
    return;
  }
  if (ts.isArrayLiteralExpression(expression)) {
    for (const element of expression.elements) addSortEvidence(element as ts.Expression, fields, bindings, gaps, context);
    return;
  }
  const origin = expressionOrigin(expression);
  bindings.push(origin);
  gaps.push("sort-field-dynamic");
}

function addSelectEvidence(expression: ts.Expression | undefined, fields: ShapeField[], bindings: string[], gaps: string[], context?: ShapeContext): void {
  if (!expression) return;
  expression = unwrapExpression(expression);
  if (expression.kind === ts.SyntaxKind.UndefinedKeyword || expression.kind === ts.SyntaxKind.NullKeyword) return;
  if (ts.isStringLiteralLike(expression)) {
    const names = expression.text.split(",").map((item) => item.trim()).filter(isSafeFieldName);
    if (names.length === 0) gaps.push("select-field-literal-unresolved");
    for (const name of names) fields.push(fieldEvidence(name, "unconditional", "string-literal", "select-argument", expression, context));
    return;
  }
  if (ts.isArrayLiteralExpression(expression)) {
    for (const element of expression.elements) addSelectEvidence(element as ts.Expression, fields, bindings, gaps, context);
    return;
  }
  bindings.push(expressionOrigin(expression));
  gaps.push("select-field-dynamic");
}

function analyzeExpression(expression: ts.Expression, context: ShapeContext, visitedBindings: Set<string>, presence: Presence): ShapeAnalysis {
  const unwrapped = unwrapExpression(expression);
  if (ts.isObjectLiteralExpression(unwrapped)) return analyzeObjectLiteral(unwrapped, context, visitedBindings, presence);
  if (ts.isArrayLiteralExpression(unwrapped)) return analyzeArrayLiteral(unwrapped, context, visitedBindings, presence);
  if (ts.isIdentifier(unwrapped)) return analyzeIdentifier(unwrapped, context, visitedBindings, presence);
  if (ts.isConditionalExpression(unwrapped)) {
    const result = emptyAnalysis("conditional-expression");
    mergeAnalysis(result, analyzeExpression(unwrapped.whenTrue, context, new Set(visitedBindings), "conditional"));
    mergeAnalysis(result, analyzeExpression(unwrapped.whenFalse, context, new Set(visitedBindings), "conditional"));
    return result;
  }
  if (ts.isBinaryExpression(unwrapped) && [ts.SyntaxKind.AmpersandAmpersandToken, ts.SyntaxKind.BarBarToken, ts.SyntaxKind.QuestionQuestionToken].includes(unwrapped.operatorToken.kind)) {
    const result = emptyAnalysis("conditional-logical-expression");
    mergeAnalysis(result, analyzeExpression(unwrapped.left, context, new Set(visitedBindings), "conditional"), "logical-left");
    mergeAnalysis(result, analyzeExpression(unwrapped.right, context, new Set(visitedBindings), "conditional"), "logical-right");
    result.constructionKind = "conditional-logical-expression";
    return result;
  }
  return unresolvedAnalysis(expressionType(unwrapped), `payload-expression-unresolved:${expressionType(unwrapped)}`, expressionOrigin(unwrapped));
}

function analyzeObjectLiteral(node: ts.ObjectLiteralExpression, context: ShapeContext, visitedBindings: Set<string>, inheritedPresence: Presence): ShapeAnalysis {
  const result = emptyAnalysis("object-literal");
  for (const property of node.properties) {
    if (ts.isSpreadAssignment(property)) {
      const spread = analyzeExpression(property.expression, context, new Set(visitedBindings), inheritedPresence === "conditional" ? "conditional" : "spread-derived");
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
        result.fields.push({ ...field, presence: inheritedPresence === "conditional" || field.presence === "conditional" ? "conditional" : "spread-derived" });
      }
      result.spreads.push(...spread.spreads);
      continue;
    }
    if (ts.isPropertyAssignment(property)) {
      const name = staticPropertyName(property.name);
      if (!name) {
        result.fields.push(fieldEvidence("<dynamic>", "dynamic-computed", expressionType(property.initializer), expressionOrigin(property.initializer), property, context));
        result.gaps.push("dynamic-computed-property");
      } else {
        result.fields.push(fieldEvidence(name, inheritedPresence, expressionType(property.initializer), expressionOrigin(property.initializer), property, context));
      }
      continue;
    }
    if (ts.isShorthandPropertyAssignment(property)) {
      result.fields.push(fieldEvidence(property.name.text, inheritedPresence, "identifier-reference", `binding:${property.name.text}`, property, context));
      result.candidateBindings.push(`binding:${property.name.text}`);
      continue;
    }
    if (ts.isMethodDeclaration(property) || ts.isGetAccessorDeclaration(property) || ts.isSetAccessorDeclaration(property)) {
      const name = staticPropertyName(property.name);
      if (name) result.fields.push(fieldEvidence(name, inheritedPresence, "method", "inline-method", property, context));
      else {
        result.fields.push(fieldEvidence("<dynamic>", "dynamic-computed", "method", "inline-method", property, context));
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
  const referencePosition = identifier.getStart(context.source);
  const declaration = resolveDeclarationAt(bindingName, identifier, context);
  if (!declaration?.initializer) {
    const parameter = resolveParameterBindingAt(bindingName, identifier);
    // A nearer lexical declaration, including one in its temporal dead zone or
    // without an initializer, prevents this identifier from denoting an outer
    // mutation callback parameter. Never fall through that shadow boundary.
    const visibleDeclaration = resolveVisibleVariableDeclaration(bindingName, identifier, context);
    if (visibleDeclaration && (!parameter || isAncestor(parameter.parameter.parent, declarationScope(visibleDeclaration)))) {
      return unresolvedAnalysis("identifier-reference", "mutation-hook-parameter-shadowed", candidate);
    }
    if (parameter && hasVisibleFunctionOrClassShadow(bindingName, identifier, parameter.parameter.parent)) {
      return unresolvedAnalysis("identifier-reference", "mutation-hook-parameter-shadowed", candidate);
    }
    const callsiteAnalysis = parameter && analyzeMutationHookParameter(parameter, context, visitedBindings, presence);
    return callsiteAnalysis ?? unresolvedAnalysis("identifier-reference", "binding-initializer-unresolved", candidate);
  }
  if (!ts.isIdentifier(declaration.name)) return unresolvedAnalysis("identifier-reference", "destructured-binding-unresolved", candidate);
  const bindingContext: ShapeContext = { ...context, callNode: identifier, callPosition: referencePosition };
  const nextVisited = new Set(visitedBindings).add(bindingName);
  const result = analyzeExpression(declaration.initializer, bindingContext, nextVisited, presence);
  result.constructionKind = `identifier:${result.constructionKind}`;
  addBindingMutations(bindingName, declaration, bindingContext, result, presence);
  if (executionScope(declaration) !== executionScope(identifier)) {
    result.gaps.push("binding-cross-execution-scope");
  }
  if (context.callPosition > referencePosition && hasPostCaptureAliasMutation(bindingName, declaration, referencePosition, context)) {
    result.gaps.push("post-capture-alias-mutation-unresolved");
  }
  result.candidateBindings.push(candidate);
  return result;
}

function resolveParameterBindingAt(bindingName: string, useNode: ts.Node): ParameterBinding | null {
  for (let current: ts.Node | undefined = useNode.parent; current; current = current.parent) {
    if (!ts.isFunctionLike(current)) continue;
    for (const [index, parameter] of current.parameters.entries()) {
      const propertyPath = bindingPropertyPath(parameter.name, bindingName);
      if (propertyPath) {
        if (parameter.dotDotDotToken) return { parameter, bindingName, propertyPath, gap: "mutation-hook-rest-parameter-unsupported" };
        if (index !== 0) return { parameter, bindingName, propertyPath, gap: "mutation-hook-parameter-position-unsupported" };
        return { parameter, bindingName, propertyPath };
      }
      if (bindingNames(parameter.name).includes(bindingName)) {
        return { parameter, bindingName, propertyPath: [], gap: "mutation-hook-parameter-binding-pattern-unsupported" };
      }
    }
  }
  return null;
}

function bindingPropertyPath(name: ts.BindingName, bindingName: string): string[] | null {
  if (ts.isIdentifier(name)) return name.text === bindingName ? [] : null;
  if (!ts.isObjectBindingPattern(name)) return null;
  for (const element of name.elements) {
    if (element.dotDotDotToken) continue;
    const nested = bindingPropertyPath(element.name, bindingName);
    if (!nested) continue;
    const propertyName = element.propertyName ? staticPropertyName(element.propertyName) : ts.isIdentifier(element.name) ? element.name.text : null;
    return propertyName ? [propertyName, ...nested] : null;
  }
  return null;
}

function analyzeMutationHookParameter(binding: ParameterBinding, context: ShapeContext, visitedBindings: Set<string>, presence: Presence): ShapeAnalysis | null {
  const hook = mutationHookContext(binding.parameter, context.source);
  if (!hook) return null;
  const result = emptyAnalysis("react-query-mutation-callsites");
  const parameterIdentity = binding.propertyPath.length > 0
    ? binding.propertyPath.join(".")
    : ts.isIdentifier(binding.parameter.name) ? binding.parameter.name.text : "parameter";
  result.candidateBindings.push(`binding:${parameterIdentity}`, `mutation-hook:${hook.bindingName}`);
  if (binding.gap || hook.gap) {
    result.gaps.push(binding.gap ?? hook.gap!);
    return result;
  }
  const parameterStateGap = mutationHookParameterStateGap(binding, hook, context);
  if (parameterStateGap) {
    result.gaps.push(parameterStateGap);
    return result;
  }
  const hookVisitKey = `mutation-hook-parameter:${binding.parameter.getStart(context.source)}`;
  if (visitedBindings.has(hookVisitKey)) {
    result.gaps.push("mutation-hook-recursion-unresolved");
    return result;
  }
  const callsiteVisited = new Set(visitedBindings).add(hookVisitKey);
  const calls: ts.CallExpression[] = [];
  const gaps: string[] = [];
  const visit = (node: ts.Node): void => {
    if (ts.isIdentifier(node) && node.text === hook.bindingName
      && !hasVisibleFunctionOrClassShadow(hook.bindingName, node, context.source)
      && resolveDeclarationAt(hook.bindingName, node, context) === hook.declaration) {
      if (node === hook.declaration.name) return;
      const access = node.parent;
      if ((ts.isPropertyAccessExpression(access) || ts.isElementAccessExpression(access)) && access.expression === node) {
        const member = ts.isPropertyAccessExpression(access)
          ? access.name.text
          : access.argumentExpression && ts.isStringLiteralLike(access.argumentExpression) ? access.argumentExpression.text : null;
        if (ts.isElementAccessExpression(access) && member === null) {
          gaps.push("mutation-hook-computed-member-unresolved");
          return;
        }
        if (member === "mutate" || member === "mutateAsync") {
          if (ts.isCallExpression(access.parent) && access.parent.expression === access) calls.push(access.parent);
          else gaps.push("mutation-hook-method-escape-unresolved");
        }
        return;
      }
      gaps.push("mutation-hook-binding-escape-unresolved");
    }
    ts.forEachChild(node, visit);
  };
  visit(context.source);

  const analyses: ShapeAnalysis[] = [];
  for (const call of calls) {
    const argument = call.arguments[0];
    if (!argument || ts.isSpreadElement(argument)) {
      gaps.push("mutation-hook-argument-unresolved");
      continue;
    }
    const callsiteContext: ShapeContext = { ...context, callNode: call, callPosition: call.getStart(context.source) };
    const resolved = resolvePropertyPath(argument, binding.propertyPath, callsiteContext);
    if (!resolved.expression) {
      gaps.push(resolved.gap ?? "mutation-hook-argument-unresolved");
      continue;
    }
    const analysis = analyzeExpression(resolved.expression, callsiteContext, new Set(callsiteVisited), presence);
    analyses.push(analysis);
    mergeAnalysis(result, analysis, `mutation-hook:${hook.bindingName}`);
  }
  if (calls.length === 0) gaps.push("mutation-hook-callsite-missing");
  if (analyses.length !== calls.length) gaps.push("mutation-hook-callsite-incomplete");
  result.gaps.push(...gaps);

  const completeCallsites = gaps.length === 0 && analyses.every((analysis) => analysis.gaps.length === 0);
  const fieldCoverage = new Map<string, number>();
  for (const analysis of analyses) {
    for (const name of new Set(analysis.fields.filter((field) => field.presence === "unconditional").map((field) => field.name))) {
      fieldCoverage.set(name, (fieldCoverage.get(name) ?? 0) + 1);
    }
  }
  result.fields = result.fields.map((field) => ({
    ...field,
    presence: completeCallsites && fieldCoverage.get(field.name) === analyses.length && field.presence === "unconditional"
      ? "unconditional"
      : "conditional"
  }));
  return result;
}

function mutationHookParameterStateGap(binding: ParameterBinding, hook: MutationHookContext, context: ShapeContext): string | null {
  const callback = binding.parameter.parent;
  const bindingName = binding.bindingName;
  const backedgeLoop = enclosingIteration(context.callNode, callback);
  let unsafe = false;
  const visit = (node: ts.Node): void => {
    if (unsafe) return;
    if (isAncestor(context.callNode, node)) return;
    if (node.getStart(context.source) >= context.callPosition
      && (!backedgeLoop || !isAncestor(backedgeLoop, node))) return;
    if (node !== callback && ts.isFunctionLike(node)) {
      const findCapture = (child: ts.Node): void => {
        if (unsafe) return;
        if (ts.isIdentifier(child) && resolvesToParameterBinding(child, binding, context)) unsafe = true;
        else ts.forEachChild(child, findCapture);
      };
      ts.forEachChild(node, findCapture);
      return;
    }
    if (ts.isIdentifier(node) && resolvesToParameterBinding(node, binding, context)) {
      for (let current: ts.Node | undefined = node.parent; current && current !== callback; current = current.parent) {
        if (ts.isBinaryExpression(current) && isAssignmentOperator(current.operatorToken.kind)
          && (isAncestor(current.left, node) || isAncestor(current.right, node))) {
          unsafe = true;
          return;
        }
        if (((ts.isPrefixUnaryExpression(current)
          && [ts.SyntaxKind.PlusPlusToken, ts.SyntaxKind.MinusMinusToken].includes(current.operator))
          || ts.isPostfixUnaryExpression(current))
          && isAncestor(current.operand, node)) {
          unsafe = true;
          return;
        }
        if (ts.isDeleteExpression(current) && isAncestor(current.expression, node)) {
          unsafe = true;
          return;
        }
        if (ts.isCallExpression(current) && isSameMutationHookInvocation(current, hook, context)) {
          continue;
        }
        if ((ts.isCallExpression(current) || ts.isNewExpression(current))
          && (isAncestor(current.expression, node)
            || current.arguments?.some((argument) => isAncestor(argument, node)))) {
          unsafe = true;
          return;
        }
        if ((ts.isVariableDeclaration(current) && current.initializer && isAncestor(current.initializer, node))
          || (ts.isReturnStatement(current) && current.expression && isAncestor(current.expression, node))
          || (ts.isThrowStatement(current) && current.expression && isAncestor(current.expression, node))) {
          unsafe = true;
          return;
        }
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(callback);
  return unsafe ? "mutation-hook-parameter-state-unresolved" : null;
}

function enclosingIteration(node: ts.Node, boundary: ts.Node): ts.IterationStatement | null {
  for (let current: ts.Node | undefined = node.parent; current && current !== boundary; current = current.parent) {
    if (ts.isForStatement(current) || ts.isForInStatement(current) || ts.isForOfStatement(current)
      || ts.isWhileStatement(current) || ts.isDoStatement(current)) return current;
  }
  return null;
}

function resolvesToParameterBinding(node: ts.Identifier, binding: ParameterBinding, context: ShapeContext): boolean {
  if (node === binding.parameter.name || node.text !== binding.bindingName) return false;
  const resolved = resolveParameterBindingAt(binding.bindingName, node);
  if (!resolved || resolved.parameter !== binding.parameter
    || resolved.propertyPath.length !== binding.propertyPath.length
    || resolved.propertyPath.some((part, index) => part !== binding.propertyPath[index])) return false;
  const visibleDeclaration = resolveVisibleVariableDeclaration(binding.bindingName, node, context);
  if (visibleDeclaration && isAncestor(binding.parameter.parent, declarationScope(visibleDeclaration))) return false;
  return !hasVisibleFunctionOrClassShadow(binding.bindingName, node, binding.parameter.parent);
}

function isSameMutationHookInvocation(call: ts.CallExpression, hook: MutationHookContext, context: ShapeContext): boolean {
  const access = unwrapExpression(call.expression);
  if (!ts.isPropertyAccessExpression(access) && !ts.isElementAccessExpression(access)) return false;
  const member = ts.isPropertyAccessExpression(access)
    ? access.name.text
    : access.argumentExpression && ts.isStringLiteralLike(access.argumentExpression)
      ? access.argumentExpression.text
      : null;
  const owner = unwrapExpression(access.expression);
  return (member === "mutate" || member === "mutateAsync")
    && ts.isIdentifier(owner)
    && owner.text === hook.bindingName
    && resolveDeclarationAt(hook.bindingName, owner, context) === hook.declaration;
}

function mutationHookContext(parameter: ts.ParameterDeclaration, source: ts.SourceFile): MutationHookContext | null {
  const callback = parameter.parent;
  if (!ts.isArrowFunction(callback) && !ts.isFunctionExpression(callback)) return null;
  const property = callback.parent;
  if (!ts.isPropertyAssignment(property) || staticPropertyName(property.name) !== "mutationFn") return null;
  const options = property.parent;
  if (!ts.isObjectLiteralExpression(options)) return null;
  const call = options.parent;
  if (!ts.isCallExpression(call) || !isProvenUseMutationCall(call, source)) return null;
  let owner: ts.Node = call;
  while (owner.parent && (ts.isParenthesizedExpression(owner.parent) || ts.isAsExpression(owner.parent)
    || ts.isTypeAssertionExpression(owner.parent) || ts.isNonNullExpression(owner.parent) || ts.isSatisfiesExpression(owner.parent))) owner = owner.parent;
  if (!ts.isVariableDeclaration(owner.parent) || !ts.isIdentifier(owner.parent.name)) return null;
  let gap: string | undefined;
  const propertyIndex = options.properties.indexOf(property);
  for (const later of options.properties.slice(propertyIndex + 1)) {
    if (ts.isSpreadAssignment(later)) {
      gap = "mutation-hook-callback-override-unresolved";
      break;
    }
    const laterName = "name" in later ? staticPropertyName(later.name) : null;
    if (laterName === "mutationFn") {
      gap = "mutation-hook-callback-overridden";
      break;
    }
    if ("name" in later && laterName === null) {
      gap = "mutation-hook-callback-override-unresolved";
      break;
    }
  }
  return { declaration: owner.parent, bindingName: owner.parent.name.text, gap };
}

function isProvenUseMutationCall(call: ts.CallExpression, source: ts.SourceFile): boolean {
  const callee = unwrapExpression(call.expression);
  for (const statement of source.statements) {
    if (!ts.isImportDeclaration(statement) || !ts.isStringLiteral(statement.moduleSpecifier)
      || statement.moduleSpecifier.text !== "@tanstack/react-query" || statement.importClause?.isTypeOnly) continue;
    const clause = statement.importClause;
    if (ts.isIdentifier(callee) && clause?.namedBindings && ts.isNamedImports(clause.namedBindings)) {
      if (clause.namedBindings.elements.some((element) => !element.isTypeOnly
        && (element.propertyName?.text ?? element.name.text) === "useMutation" && element.name.text === callee.text)
        && isImportedBindingUnshadowed(callee.text, call, source)) return true;
    }
    if (ts.isPropertyAccessExpression(callee) && ts.isIdentifier(callee.expression)
      && callee.name.text === "useMutation" && clause?.namedBindings && ts.isNamespaceImport(clause.namedBindings)
      && clause.namedBindings.name.text === callee.expression.text
      && isImportedBindingUnshadowed(callee.expression.text, call, source)) return true;
  }
  return false;
}

function isImportedBindingUnshadowed(bindingName: string, useNode: ts.Node, source: ts.SourceFile): boolean {
  const declarations = collectVariableDeclarations(source);
  if ((declarations.get(bindingName) ?? []).some((declaration) => isDeclarationVisibleAt(declaration, useNode))) return false;
  for (let current: ts.Node | undefined = useNode.parent; current; current = current.parent) {
    if (ts.isFunctionLike(current)
      && current.parameters.some((parameter) => bindingNames(parameter.name).includes(bindingName))) return false;
    if ((ts.isFunctionExpression(current) || ts.isClassExpression(current)) && current.name?.text === bindingName) return false;
    if (ts.isSourceFile(current) || ts.isBlock(current)) {
      if (current.statements.some((statement) => (ts.isFunctionDeclaration(statement) || ts.isClassDeclaration(statement))
        && statement.name?.text === bindingName)) return false;
    }
  }
  return true;
}

function resolvePropertyPath(expression: ts.Expression, propertyPath: string[], context: ShapeContext): { expression?: ts.Expression; gap?: string } {
  if (propertyPath.length === 0) return { expression };
  const unwrapped = unwrapExpression(expression);
  if (ts.isIdentifier(unwrapped)) {
    const declaration = resolveDeclarationAt(unwrapped.text, unwrapped, context);
    if (!declaration?.initializer || !ts.isIdentifier(declaration.name)) return { gap: "mutation-hook-argument-binding-unresolved" };
    if (executionScope(declaration) !== executionScope(context.callNode)
      || hasPriorBindingReference(unwrapped.text, declaration, context)) {
      return { gap: "mutation-hook-destructured-argument-binding-state-unresolved" };
    }
    return resolvePropertyPath(declaration.initializer, propertyPath, context);
  }
  if (!ts.isObjectLiteralExpression(unwrapped)) return { gap: "mutation-hook-destructured-argument-unresolved" };
  const [head, ...tail] = propertyPath;
  let selected: ts.Expression | null = null;
  let unresolvedSpread = false;
  for (const property of unwrapped.properties) {
    if (ts.isSpreadAssignment(property)) {
      selected = null;
      unresolvedSpread = true;
      continue;
    }
    const propertyName = "name" in property ? staticPropertyName(property.name) : null;
    if (propertyName !== head) continue;
    if (ts.isPropertyAssignment(property)) selected = property.initializer;
    else if (ts.isShorthandPropertyAssignment(property)) selected = property.name;
    else return { gap: "mutation-hook-destructured-property-unresolved" };
    unresolvedSpread = false;
  }
  return selected
    ? resolvePropertyPath(selected, tail, context)
    : { gap: unresolvedSpread ? "mutation-hook-destructured-spread-unresolved" : "mutation-hook-destructured-property-missing" };
}

function hasPriorBindingReference(bindingName: string, declaration: ts.VariableDeclaration, context: ShapeContext): boolean {
  const scope = executionScope(context.callNode);
  const declarationEnd = declaration.getEnd();
  let found = false;
  const visit = (node: ts.Node): void => {
    if (found || (node !== scope && ts.isFunctionLike(node))) return;
    const position = node.getStart(context.source);
    if (position <= declarationEnd) return ts.forEachChild(node, visit);
    // The mutate/mutateAsync invocation itself is the endpoint whose argument
    // state we are resolving, not an earlier escape of that state.
    if (position >= context.callPosition) return;
    if (ts.isIdentifier(node) && node.text === bindingName
      && resolveDeclarationAt(bindingName, node, context) === declaration) {
      found = true;
      return;
    }
    ts.forEachChild(node, visit);
  };
  visit(scope);
  return found;
}

function addBindingMutations(bindingName: string, declaration: ts.VariableDeclaration, context: ShapeContext, result: ShapeAnalysis, inheritedPresence: Presence): void {
  const declarationEnd = declaration.getEnd();
  const scope = executionScope(context.callNode);
  const visit = (node: ts.Node): void => {
    if (node !== scope && ts.isFunctionLike(node)) return;
    const position = node.getStart(context.source);
    if (position <= declarationEnd || position >= context.callPosition) return ts.forEachChild(node, visit);
    const referenceGap = unmodeledReferenceUse(node, bindingName, declaration, context);
    if (referenceGap) result.gaps.push(referenceGap);
    if (ts.isBinaryExpression(node) && isAssignmentOperator(node.operatorToken.kind)) {
      if (ts.isIdentifier(node.left) && node.left.text === bindingName && resolveDeclarationAt(bindingName, node, context) === declaration) {
        if (node.operatorToken.kind === ts.SyntaxKind.EqualsToken) {
          applyBindingReassignment(bindingName, node, context, result,
            inheritedPresence === "conditional" || isConditionallyExecuted(node, scope) ? "conditional" : inheritedPresence);
        } else {
          result.gaps.push(`binding-compound-reassignment:${ts.tokenToString(node.operatorToken.kind) ?? "unknown"}`);
        }
        return;
      }
      const field = assignedField(node.left, bindingName);
      if (field && resolveDeclarationAt(bindingName, node, context) !== declaration) {
        return;
      }
      if (field === "<dynamic>") {
        result.fields.push(fieldEvidence(field, "dynamic-computed", expressionType(node.right), expressionOrigin(node.right), node, context));
        result.gaps.push("dynamic-computed-assignment");
      } else if (field) {
        const simpleAssignment = node.operatorToken.kind === ts.SyntaxKind.EqualsToken;
        result.fields.push(fieldEvidence(field, simpleAssignment && inheritedPresence !== "conditional" && !isConditionallyExecuted(node, scope) ? inheritedPresence : "conditional", expressionType(node.right), expressionOrigin(node.right), node, context));
        if (!simpleAssignment) result.gaps.push(`compound-property-assignment:${ts.tokenToString(node.operatorToken.kind) ?? "unknown"}`);
      }
    }
    if (ts.isCallExpression(node) && expressionChain(node.expression)?.join(".") === "Object.assign" && ts.isIdentifier(node.arguments[0]) && node.arguments[0].text === bindingName
      && resolveDeclarationAt(bindingName, node, context) === declaration) {
      const conditional = inheritedPresence === "conditional" || isConditionallyExecuted(node, scope);
      for (const source of node.arguments.slice(1)) {
        const spread = analyzeExpression(source, context, new Set([bindingName]), conditional ? "conditional" : "spread-derived");
        if (conditional) spread.fields = spread.fields.map((field) => ({ ...field, presence: "conditional" }));
        mergeAnalysis(result, spread);
        result.gaps.push(...spread.gaps.map((gap) => `object-assign:${gap}`));
      }
    }
    if (((ts.isPrefixUnaryExpression(node) && [ts.SyntaxKind.PlusPlusToken, ts.SyntaxKind.MinusMinusToken].includes(node.operator))
      || ts.isPostfixUnaryExpression(node))
      && assignedField(node.operand, bindingName)
      && resolveDeclarationAt(bindingName, node, context) === declaration) {
      result.gaps.push("unary-property-mutation");
    }
    if (ts.isDeleteExpression(node)) {
      const field = assignedField(node.expression, bindingName);
      if (field && resolveDeclarationAt(bindingName, node, context) === declaration) {
        if (field === "<dynamic>") {
          result.gaps.push("dynamic-computed-deletion");
        } else if (isConditionallyExecuted(node, scope)) {
          result.fields = result.fields.map((candidate) => candidate.name === field ? { ...candidate, presence: "conditional" } : candidate);
        } else {
          result.fields = result.fields.filter((candidate) => candidate.name !== field);
        }
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(scope);
}

function applyBindingReassignment(bindingName: string, node: ts.BinaryExpression, context: ShapeContext, result: ShapeAnalysis, presence: Presence): void {
  const replacement = analyzeExpression(node.right, context, new Set([bindingName]), presence);
  if (presence === "conditional") {
    result.fields = result.fields.map((field) => ({ ...field, presence: "conditional" }));
    mergeAnalysis(result, replacement, "conditional-reassignment");
    result.gaps.push("conditional-binding-reassignment");
    return;
  }
  result.fields = replacement.fields;
  result.spreads = replacement.spreads;
  result.candidateBindings = replacement.candidateBindings;
  result.gaps = replacement.gaps;
  result.constructionKind = `reassigned:${replacement.constructionKind}`;
}

function isAssignmentOperator(kind: ts.SyntaxKind): boolean {
  return kind >= ts.SyntaxKind.FirstAssignment && kind <= ts.SyntaxKind.LastAssignment;
}

function executionScope(node: ts.Node): ts.Node {
  for (let current: ts.Node | undefined = node; current; current = current.parent) {
    if (ts.isFunctionLike(current) || ts.isSourceFile(current)) return current;
  }
  return node.getSourceFile();
}

function hasPostCaptureAliasMutation(bindingName: string, declaration: ts.VariableDeclaration, start: number, context: ShapeContext): boolean {
  const scope = executionScope(context.callNode);
  let found = false;
  const visit = (node: ts.Node): void => {
    if (found || (node !== scope && ts.isFunctionLike(node))) return;
    const position = node.getStart(context.source);
    if (position <= start || position >= context.callPosition) return ts.forEachChild(node, visit);
    if (unmodeledReferenceUse(node, bindingName, declaration, context)) {
      found = true;
      return;
    }
    if (ts.isBinaryExpression(node) && assignedField(node.left, bindingName)
      && resolveDeclarationAt(bindingName, node, context) === declaration) {
      found = true;
      return;
    }
    if (ts.isCallExpression(node) && expressionChain(node.expression)?.join(".") === "Object.assign"
      && ts.isIdentifier(node.arguments[0]) && node.arguments[0].text === bindingName
      && resolveDeclarationAt(bindingName, node, context) === declaration) {
      found = true;
      return;
    }
    if (ts.isDeleteExpression(node) && assignedField(node.expression, bindingName)
      && resolveDeclarationAt(bindingName, node, context) === declaration) {
      found = true;
      return;
    }
    if (((ts.isPrefixUnaryExpression(node) && [ts.SyntaxKind.PlusPlusToken, ts.SyntaxKind.MinusMinusToken].includes(node.operator))
      || ts.isPostfixUnaryExpression(node))
      && assignedField(node.operand, bindingName)
      && resolveDeclarationAt(bindingName, node, context) === declaration) {
      found = true;
      return;
    }
    ts.forEachChild(node, visit);
  };
  visit(scope);
  return found;
}

// Reference escapes can mutate an object without a direct assignment to its binding.
// Keep the observed fields but do not infer the behavior of aliases or helpers.
function unmodeledReferenceUse(node: ts.Node, bindingName: string, declaration: ts.VariableDeclaration, context: ShapeContext): string | null {
  // The containing call/capture has not executed yet at this reference.
  if (isAncestor(node, context.callNode)) return null;
  const referencesBinding = (expression: ts.Expression | undefined): boolean => {
    if (!expression) return false;
    const unwrapped = unwrapExpression(expression);
    return ts.isIdentifier(unwrapped) && unwrapped.text === bindingName
      && resolveDeclarationAt(bindingName, unwrapped, context) === declaration;
  };
  if ((ts.isVariableDeclaration(node) && referencesBinding(node.initializer))
    || (ts.isBinaryExpression(node) && isAssignmentOperator(node.operatorToken.kind) && referencesBinding(node.right))
    || (ts.isPropertyAssignment(node) && referencesBinding(node.initializer))) {
    return "binding-alias-escape-unresolved";
  }
  if (ts.isCallExpression(node) || ts.isNewExpression(node)) {
    // Object.assign is already modeled as a shallow copy, with no source escape.
    if (ts.isCallExpression(node) && expressionChain(node.expression)?.join(".") === "Object.assign") return null;
    const callee = unwrapExpression(node.expression);
    if ((ts.isPropertyAccessExpression(callee) || ts.isElementAccessExpression(callee))
      && referencesBinding(callee.expression)) return "binding-method-call-unresolved";
    if (node.arguments?.some(referencesBinding)) return "binding-call-escape-unresolved";
  }
  return null;
}

function resolveDeclarationAt(bindingName: string, useNode: ts.Node, context: ShapeContext): ts.VariableDeclaration | null {
  // Select the lexical binding before checking source order: a later declaration
  // still shadows an outer one (including a temporal dead zone).
  const declaration = resolveVisibleVariableDeclaration(bindingName, useNode, context);
  const scope = declaration && declarationScope(declaration);
  for (let current: ts.Node | undefined = useNode; current; current = current.parent) {
    if (ts.isFunctionLike(current) && current.parameters.some((parameter) => bindingNames(parameter.name).includes(bindingName))) return null;
    if (current === scope) break;
  }
  return declaration && declaration.getStart(context.source) < useNode.getStart(context.source) ? declaration : null;
}

function resolveVisibleVariableDeclaration(bindingName: string, useNode: ts.Node, context: ShapeContext): ts.VariableDeclaration | null {
  return [...(context.declarations.get(bindingName) ?? [])]
    .filter((item) => isDeclarationVisibleAt(item, useNode))
    .sort((left, right) => scopeDepth(declarationScope(right)) - scopeDepth(declarationScope(left))
      || right.getStart(context.source) - left.getStart(context.source))[0] ?? null;
}

function hasVisibleFunctionOrClassShadow(bindingName: string, useNode: ts.Node, boundary: ts.Node): boolean {
  for (let current: ts.Node | undefined = useNode.parent; current; current = current.parent) {
    if (ts.isBlock(current) || ts.isSourceFile(current) || ts.isCaseBlock(current)) {
      const statements = ts.isCaseBlock(current)
        ? current.clauses.flatMap((clause) => [...clause.statements])
        : current.statements;
      if (statements.some((statement) =>
        (ts.isFunctionDeclaration(statement) || ts.isClassDeclaration(statement))
        && statement.name?.text === bindingName)) return true;
    }
    if (current === boundary) break;
  }
  return false;
}

function isDeclarationVisibleAt(declaration: ts.VariableDeclaration, useNode: ts.Node): boolean {
  const scope = declarationScope(declaration);
  return isAncestor(scope, useNode);
}

function declarationScope(declaration: ts.VariableDeclaration): ts.Node {
  if (ts.isCatchClause(declaration.parent)) return declaration.parent;
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

function analyzeArrayLiteral(node: ts.ArrayLiteralExpression, context: ShapeContext, visitedBindings: Set<string>, inheritedPresence: Presence): ShapeAnalysis {
  const result = emptyAnalysis("array-literal");
  if (node.elements.length === 0) return result;
  const perElement: ShapeAnalysis[] = [];
  let hasUnresolvedSpread = false;
  for (const element of node.elements) {
    if (ts.isSpreadElement(element)) {
      hasUnresolvedSpread = true;
      result.spreads.push({ expressionType: expressionType(element.expression), origin: expressionOrigin(element.expression), resolution: "unresolved", fieldNames: [] });
      result.candidateBindings.push(expressionOrigin(element.expression));
      result.gaps.push("array-spread-unresolved");
      continue;
    }
    perElement.push(analyzeExpression(element as ts.Expression, context, new Set(visitedBindings), inheritedPresence));
  }
  const elementCount = perElement.length;
  const occurrences = new Map<string, number>();
  for (const analysis of perElement) {
    for (const name of new Set(normalizeFields(analysis.fields).map((field) => field.name))) occurrences.set(name, (occurrences.get(name) ?? 0) + 1);
    mergeAnalysis(result, analysis);
  }
  result.fields = result.fields.map((field) => ({
    ...field,
    presence: !hasUnresolvedSpread && occurrences.get(field.name) === elementCount && field.presence === "unconditional" ? "unconditional" : "conditional"
  }));
  return result;
}

function shapeFact(input: EntityShapeInput, factType: string, ruleId: string, argumentIndex: number, argumentRole: string, analysis: ShapeAnalysis, querySemantics?: ReturnType<typeof extractQuerySemantics>): CodeFact {
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
        querySemanticsJson: querySemantics ? JSON.stringify(querySemantics) : undefined,
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
    if (ts.isVariableDeclaration(node)) {
      for (const name of bindingNames(node.name)) {
        const current = declarations.get(name) ?? [];
        current.push(node);
        declarations.set(name, current);
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(source);
  return declarations;
}

function bindingNames(name: ts.BindingName): string[] {
  if (ts.isIdentifier(name)) return [name.text];
  return name.elements.flatMap((element) => ts.isBindingElement(element) ? bindingNames(element.name) : []);
}

function assignedField(left: ts.Expression, bindingName: string): string | null {
  if (ts.isPropertyAccessExpression(left) && ts.isIdentifier(left.expression) && left.expression.text === bindingName) return left.name.text;
  if (ts.isElementAccessExpression(left) && ts.isIdentifier(left.expression) && left.expression.text === bindingName) {
    return left.argumentExpression && ts.isStringLiteralLike(left.argumentExpression) ? left.argumentExpression.text : "<dynamic>";
  }
  return null;
}

function isConditionallyExecuted(node: ts.Node, source: ts.Node): boolean {
  for (let parent = node.parent; parent && parent !== source; parent = parent.parent) {
    if (ts.isIfStatement(parent) || ts.isConditionalExpression(parent) || ts.isSwitchStatement(parent)
      || ts.isForStatement(parent) || ts.isForInStatement(parent) || ts.isForOfStatement(parent)
      || ts.isWhileStatement(parent) || ts.isDoStatement(parent) || ts.isTryStatement(parent)) return true;
    if (ts.isBinaryExpression(parent)
      && [ts.SyntaxKind.AmpersandAmpersandToken, ts.SyntaxKind.BarBarToken, ts.SyntaxKind.QuestionQuestionToken].includes(parent.operatorToken.kind)
      && isAncestor(parent.right, node)) return true;
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
    return parent ? [...parent, "<literal-key>"] : null;
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

function fieldEvidence(name: string, presence: Presence, expressionTypeName: string, origin: string, node: ts.Node, context?: ShapeContext): ShapeField {
  const source = context?.source ?? node.getSourceFile();
  const start = source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1;
  const end = source.getLineAndCharacterOfPosition(node.getEnd()).line + 1;
  return {
    name,
    presence,
    expressionType: expressionTypeName,
    origin,
    evidenceStartLine: start,
    evidenceEndLine: end,
    evidenceSnippetHash: hash(node.getText(source), 64)
  };
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
