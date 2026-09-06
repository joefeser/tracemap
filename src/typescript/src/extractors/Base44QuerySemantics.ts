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
    if (ts.isIdentifier(node) || ts.isPropertyAccessExpression(node) || ts.isElementAccessExpression(node)) {
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
      const value = unwrap(propertyValue(property));
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
        entries.push({ field, form: "operators", operators });
      } else {
        const evidence = operand(value);
        if (evidence.kind === "reference") gaps.add("query:implicit_predicate_type_unresolved");
        entries.push({ field, form: "implicit", operand: evidence });
      }
    }
    return { kind: "filter", entries };
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
  const args = roles.map((role, index) => {
    const input = call.arguments[index];
    if (!input) {
      if (role === "filter") gaps.add("query:filter_missing");
      return { index, role, presence: "absent" };
    }
    const node = unwrap(input);
    let value: Record<string, unknown>;
    if (role === "filter") value = query(node);
    else if (node.kind === ts.SyntaxKind.NullKeyword) value = { kind: "null" };
    else if (role === "sort" || role === "fields") value = namedFields(node, role);
    else if (ts.isNumericLiteral(node) && Number.isSafeInteger(Number(node.text)) && Number(node.text) >= 0) value = { kind: "integer", value: Number(node.text) };
    else value = gap(`query:${role}_unresolved`, node);
    return { index, role, presence: "supplied", span: span(node), value };
  });
  if (call.arguments.length > roles.length) gaps.add("query:extra_arguments");
  return { schemaVersion: "88mph.entity-query.v1", method, completeness: gaps.size ? "unresolved" : "complete", arguments: args, gaps: [...gaps].sort() };
}
