import ts from "typescript";

/** Independently projected from TypeScript syntax; never includes operand text. */
export function extractQuerySemantics(call: ts.CallExpression, source: ts.SourceFile, method: string) {
  const roles = new Map([
    ["filter", ["filter", "sort", "limit", "skip", "fields"]],
    ["list", ["sort", "limit", "skip", "fields"]],
    ["deleteMany", ["filter"]]
  ]).get(method);
  if (!roles) return undefined;
  const gaps = new Set<string>();
  let budget = 512;
  const span = (node: ts.Node) => ({ start: node.getStart(source), end: node.getEnd() });
  function unwrap(node: ts.Expression): ts.Expression {
    let current = node;
    let depth = 0;
    while (ts.isParenthesizedExpression(current) || ts.isAsExpression(current) || ts.isTypeAssertionExpression(current)
      || ts.isNonNullExpression(current) || ts.isSatisfiesExpression(current)) {
      if (++depth > 16 || --budget < 0) { gaps.add("query:complexity_limit"); break; }
      current = current.expression;
    }
    return current;
  }
  function gap(reason: string, node?: ts.Node): Record<string, unknown> {
    gaps.add(reason);
    return { kind: "unknown", ...(node ? { span: span(node) } : {}) };
  }
  function admit(node: ts.Node, depth: number): boolean {
    if (--budget < 0 || depth > 16) { gaps.add("query:complexity_limit"); return false; }
    if (ts.isOmittedExpression(node)) { gaps.add("query:missing_expression"); return false; }
    return true;
  }
  function admittedReference(input: ts.Expression, depth: number): boolean {
    const node = unwrap(input);
    if (!admit(node, depth)) return false;
    if (ts.isIdentifier(node) || node.kind === ts.SyntaxKind.ThisKeyword) return true;
    if (ts.isPropertyAccessExpression(node)) {
      return admittedReference(node.expression, depth + 1);
    }
    if (ts.isElementAccessExpression(node)) {
      if (!admittedReference(node.expression, depth + 1)) return false;
      const index = node.argumentExpression;
      if (!index) {
        gaps.add("query:missing_expression");
        return false;
      }
      const value = unwrap(index);
      if (!admit(value, depth + 1)) return false;
      if (ts.isStringLiteralLike(value) || ts.isNumericLiteral(value)) return true;
      return admittedReference(value, depth + 1);
    }
    return false;
  }
  function operand(input: ts.Expression, depth = 0): Record<string, unknown> {
    const node = unwrap(input);
    if (!admit(node, depth)) return { kind: "unknown" };
    const type = ts.isStringLiteralLike(node) ? "string" : ts.isNumericLiteral(node) ? "number"
      : node.kind === ts.SyntaxKind.TrueKeyword || node.kind === ts.SyntaxKind.FalseKeyword ? "boolean"
      : node.kind === ts.SyntaxKind.NullKeyword ? "null" : null;
    if (type) return { kind: "literal", type, span: span(node) };
    if (ts.isPrefixUnaryExpression(node) && [ts.SyntaxKind.PlusToken, ts.SyntaxKind.MinusToken].includes(node.operator)
      && ts.isNumericLiteral(unwrap(node.operand))) {
      return { kind: "literal", type: "number", span: span(node) };
    }
    if ((ts.isIdentifier(node) || node.kind === ts.SyntaxKind.ThisKeyword
      || ts.isPropertyAccessExpression(node) || ts.isElementAccessExpression(node))
      && admittedReference(node, depth + 1)) {
      return { kind: "reference", span: span(node) };
    }
    if (ts.isArrayLiteralExpression(node)) {
      const items: Array<Record<string, unknown>> = [];
      for (const element of node.elements) {
        if (budget <= 0) { gaps.add("query:complexity_limit"); break; }
        items.push(operand(element, depth + 1));
      }
      return { kind: "array", span: span(node), items };
    }
    return gap("query:operand_expression_unsupported", node);
  }
  function propertyName(node: ts.ObjectLiteralElementLike): string | null {
    if (!ts.isPropertyAssignment(node) && !ts.isShorthandPropertyAssignment(node)) return null;
    return ts.isIdentifier(node.name) || ts.isStringLiteral(node.name) ? node.name.text : null;
  }
  function propertyValue(node: ts.ObjectLiteralElementLike): ts.Expression {
    return ts.isPropertyAssignment(node) ? node.initializer : (node as ts.ShorthandPropertyAssignment).name;
  }
  const fieldNamePattern = /^[A-Za-z_][A-Za-z0-9_.]*$/u;
  const knownOperators = new Set(["$eq", "$ne", "$gt", "$gte", "$lt", "$lte", "$in", "$nin", "$exists", "$regex"]);
  function queryEntry(field: string, input: ts.Expression): Record<string, unknown> {
    const value = unwrap(input);
    if (ts.isObjectLiteralExpression(value)) {
      const operators: Array<Record<string, unknown>> = [];
      const visited = new Set<string>();
      if (!value.properties.length) gaps.add("query:operator_object_empty");
      for (const operatorProperty of value.properties) {
        if (!admit(operatorProperty, 0)) break;
        const operator = propertyName(operatorProperty);
        if (!operator || !knownOperators.has(operator) || visited.has(operator)) {
          gaps.add("query:operator_unresolved"); continue;
        }
        visited.add(operator);
        operators.push({ operator, operand: operand(propertyValue(operatorProperty)) });
      }
      return { field, form: "operators", operators };
    }
    // A direct identifier/member reference is complete source evidence for a
    // runtime-deferred value expression. Its eventual structure belongs to
    // exact-SDK/runtime conformance, not static type inference.
    return { field, form: "implicit", operand: operand(value) };
  }
  function query(input: ts.Expression): Record<string, unknown> {
    const node = unwrap(input);
    if (!ts.isObjectLiteralExpression(node)) return gap("query:filter_construction_unresolved", node);
    const entries: Array<Record<string, unknown>> = [];
    const fields = new Set<string>();
    for (const property of node.properties) {
      if (!admit(property, 0)) break;
      const field = propertyName(property);
      if (!field || !fieldNamePattern.test(field) || field === "__proto__" || fields.has(field)) {
        gaps.add("query:field_or_composition_unresolved"); continue;
      }
      fields.add(field);
      entries.push(queryEntry(field, propertyValue(property)));
    }
    return { kind: "filter", entries };
  }
  function boundQuery(input: ts.Expression): Record<string, unknown> | null {
    const node = unwrap(input);
    if (!ts.isIdentifier(node)) return null;
    const declaration = resolveBindingDeclaration(node.text, node, source);
    if (!declaration?.initializer || !ts.isIdentifier(declaration.name)
      || !ts.isObjectLiteralExpression(unwrap(declaration.initializer))
      || executionScope(declaration) !== executionScope(call)) return null;

    const entries = new Map<string, Record<string, unknown>>();
    const initial = unwrap(declaration.initializer) as ts.ObjectLiteralExpression;
    for (const property of initial.properties) {
      if (!admit(property, 0)) break;
      const field = propertyName(property);
      if (!field || !fieldNamePattern.test(field) || field === "__proto__" || entries.has(field)) {
        gaps.add("query:field_or_composition_unresolved");
        continue;
      }
      entries.set(field, {
        ...queryEntry(field, propertyValue(property)),
        presence: "always",
        derivation: [{ kind: "binding-initializer", span: span(property) }]
      });
    }

    const scope = executionScope(call);
    const declarationEnd = declaration.getEnd();
    const callStart = call.getStart(source);
    const visitBindingFlow = (candidate: ts.Node): void => {
      if (candidate !== scope && ts.isFunctionLike(candidate)) {
        let captures = false;
        const findCapture = (child: ts.Node): void => {
          if (captures) return;
          if (ts.isIdentifier(child) && child.text === node.text
            && resolveBindingDeclaration(child.text, child, source) === declaration) captures = true;
          else ts.forEachChild(child, findCapture);
        };
        ts.forEachChild(candidate, findCapture);
        if (captures) gaps.add("query:binding-nested-capture-unresolved");
        return;
      }
      const position = candidate.getStart(source);
      if (position <= declarationEnd || position >= callStart) return ts.forEachChild(candidate, visitBindingFlow);
      if (ts.isBinaryExpression(candidate) && referencesBinding(candidate.left, node.text, declaration, source)) {
        const field = assignedStaticField(candidate.left, node.text);
        if (candidate.operatorToken.kind !== ts.SyntaxKind.EqualsToken || !field || field === "__proto__") {
          gaps.add("query:binding-mutation-unresolved");
          return;
        }
        const conditional = conditionallyExecuted(candidate, scope);
        if (entries.has(field)) {
          gaps.add(conditional ? "query:conditional-value-reassignment-unresolved" : "query:value-reassignment-unresolved");
          return;
        }
        entries.set(field, {
          ...queryEntry(field, candidate.right),
          presence: conditional ? "conditional" : "always",
          derivation: [{ kind: conditional ? "conditional-property-assignment" : "property-assignment", span: span(candidate) }]
        });
        return;
      }
      if (ts.isIdentifier(candidate) && candidate.text === node.text
        && resolveBindingDeclaration(candidate.text, candidate, source) === declaration
        && !safeBindingReference(candidate, node, declaration)) {
        gaps.add("query:binding-state-or-escape-unresolved");
        return;
      }
      ts.forEachChild(candidate, visitBindingFlow);
    };
    visitBindingFlow(scope);
    return {
      kind: "filter",
      construction: {
        kind: "binding",
        span: span(node),
        derivation: [{ kind: "variable-declaration", span: span(declaration) }]
      },
      entries: [...entries.values()]
    };
  }
  function namedFields(input: ts.Expression, role: string): Record<string, unknown> {
    const node = unwrap(input);
    const array = ts.isArrayLiteralExpression(node);
    const values = array ? node.elements : [node];
    const fields: unknown[] = [];
    for (const element of values) {
      const value = unwrap(element);
      if (!admit(value, 0)) break;
      if (!ts.isStringLiteralLike(value)) return gap(`query:${role}_unresolved`, value);
      for (const part of value.text.split(",")) {
        const text = part.trim();
        const name = role === "sort" ? text.replace(/^[-+]/u, "") : text;
        if (!fieldNamePattern.test(name)) return gap(`query:${role}_unresolved`, value);
        fields.push(role === "sort" ? { name, direction: text.startsWith("-") ? "desc" : "asc" } : name);
        if (fields.length > 512) return gap("query:complexity_limit", value);
      }
    }
    return { kind: role === "sort" ? "sort" : "selection", encoding: array ? "array" : "string", fields };
  }
  let descriptorVersion = "88mph.entity-query.v2";
  const args = roles.map((role, index) => {
    const input = call.arguments[index];
    if (!input) {
      if (role === "filter") gaps.add("query:filter_missing");
      return { index, role, presence: "absent" };
    }
    const node = unwrap(input);
    let value: Record<string, unknown>;
    if (role === "filter") {
      const bound = boundQuery(node);
      if (bound) {
        descriptorVersion = "88mph.entity-query.v3";
        value = bound;
      } else value = query(node);
    }
    else if (node.kind === ts.SyntaxKind.NullKeyword) value = { kind: "null" };
    else if (role === "sort" || role === "fields") value = namedFields(node, role);
    else if (ts.isNumericLiteral(node) && Number.isSafeInteger(Number(node.text)) && Number(node.text) >= 0) value = { kind: "integer", value: Number(node.text) };
    else value = gap(`query:${role}_unresolved`, node);
    return { index, role, presence: "supplied", span: span(node), value };
  });
  if (call.arguments.length > roles.length) gaps.add("query:extra_arguments");
  return { schemaVersion: descriptorVersion, method, completeness: gaps.size ? "unresolved" : "complete", arguments: args, gaps: [...gaps].sort() };
}

function resolveBindingDeclaration(name: string, use: ts.Node, source: ts.SourceFile): ts.VariableDeclaration | null {
  const candidates: ts.VariableDeclaration[] = [];
  const visit = (node: ts.Node): void => {
    if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name) && node.name.text === name
      && declarationScope(node).getStart(source) <= use.getStart(source)
      && isAncestor(declarationScope(node), use)) candidates.push(node);
    ts.forEachChild(node, visit);
  };
  visit(source);
  const selected = candidates.sort((left, right) => scopeDepth(declarationScope(right)) - scopeDepth(declarationScope(left))
    || right.getStart(source) - left.getStart(source))[0] ?? null;
  if (!selected || selected.getStart(source) >= use.getStart(source)) return null;
  for (let current: ts.Node | undefined = use.parent; current && current !== declarationScope(selected); current = current.parent) {
    if (ts.isFunctionLike(current) && current.parameters.some((parameter) => bindingNames(parameter.name).includes(name))) return null;
  }
  return selected;
}

function safeBindingReference(reference: ts.Identifier, endpoint: ts.Identifier, declaration: ts.VariableDeclaration): boolean {
  if (reference === endpoint) return true;
  const parent = reference.parent;
  if ((ts.isPropertyAccessExpression(parent) || ts.isElementAccessExpression(parent)) && parent.expression === reference) {
    if (ts.isCallExpression(parent.parent) && parent.parent.expression === parent) return false;
    if (ts.isDeleteExpression(parent.parent) || ts.isPrefixUnaryExpression(parent.parent) || ts.isPostfixUnaryExpression(parent.parent)) return false;
    if (ts.isBinaryExpression(parent.parent) && parent.parent.left === parent) return true;
    return true;
  }
  return ts.isVariableDeclaration(parent) && parent === declaration;
}

function referencesBinding(expression: ts.Expression, name: string, declaration: ts.VariableDeclaration, source: ts.SourceFile): boolean {
  const owner = ts.isPropertyAccessExpression(expression) || ts.isElementAccessExpression(expression) ? expression.expression : expression;
  return ts.isIdentifier(owner) && owner.text === name
    && resolveBindingDeclaration(name, owner, source) === declaration;
}

function assignedStaticField(expression: ts.Expression, name: string): string | null {
  if (ts.isPropertyAccessExpression(expression) && ts.isIdentifier(expression.expression) && expression.expression.text === name) return expression.name.text;
  if (ts.isElementAccessExpression(expression) && ts.isIdentifier(expression.expression) && expression.expression.text === name
    && expression.argumentExpression && ts.isStringLiteralLike(expression.argumentExpression)) return expression.argumentExpression.text;
  return null;
}

function conditionallyExecuted(node: ts.Node, boundary: ts.Node): boolean {
  for (let parent = node.parent; parent && parent !== boundary; parent = parent.parent) {
    if (ts.isIfStatement(parent) || ts.isConditionalExpression(parent) || ts.isSwitchStatement(parent)
      || ts.isForStatement(parent) || ts.isForInStatement(parent) || ts.isForOfStatement(parent)
      || ts.isWhileStatement(parent) || ts.isDoStatement(parent) || ts.isTryStatement(parent)) return true;
    if (ts.isBinaryExpression(parent)
      && [ts.SyntaxKind.AmpersandAmpersandToken, ts.SyntaxKind.BarBarToken, ts.SyntaxKind.QuestionQuestionToken].includes(parent.operatorToken.kind)
      && isAncestor(parent.right, node)) return true;
  }
  return false;
}

function executionScope(node: ts.Node): ts.Node {
  for (let current: ts.Node | undefined = node; current; current = current.parent) {
    if (ts.isFunctionLike(current) || ts.isSourceFile(current)) return current;
  }
  return node.getSourceFile();
}

function declarationScope(declaration: ts.VariableDeclaration): ts.Node {
  const list = declaration.parent;
  const functionScoped = ts.isVariableDeclarationList(list) && (list.flags & (ts.NodeFlags.Let | ts.NodeFlags.Const)) === 0;
  for (let current: ts.Node | undefined = declaration.parent; current; current = current.parent) {
    if (functionScoped && (ts.isFunctionLike(current) || ts.isSourceFile(current))) return current;
    if (!functionScoped && (ts.isBlock(current) || ts.isSourceFile(current) || ts.isCaseBlock(current)
      || ts.isForStatement(current) || ts.isForInStatement(current) || ts.isForOfStatement(current))) return current;
  }
  return declaration.getSourceFile();
}

function bindingNames(name: ts.BindingName): string[] {
  if (ts.isIdentifier(name)) return [name.text];
  return name.elements.flatMap((element) => ts.isBindingElement(element) ? bindingNames(element.name) : []);
}

function scopeDepth(node: ts.Node): number {
  let depth = 0;
  for (let current: ts.Node | undefined = node; current; current = current.parent) depth += 1;
  return depth;
}

function isAncestor(ancestor: ts.Node, node: ts.Node): boolean {
  for (let current: ts.Node | undefined = node; current; current = current.parent) if (current === ancestor) return true;
  return false;
}
