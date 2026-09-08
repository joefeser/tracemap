import ts from "typescript";
import { describe, expect, it } from "vitest";
import { extractQuerySemantics } from "../src/extractors/Base44QuerySemantics";

function extract(method: string, args: string) {
  const code = `base44.entities.Widget.${method}(${args});`;
  const source = ts.createSourceFile("fixture.ts", code, ts.ScriptTarget.Latest, true);
  const call = (source.statements[0] as ts.ExpressionStatement).expression as ts.CallExpression;
  return extractQuerySemantics(call, source, method)!;
}

function extractProgram(code: string, entity = "Widget") {
  const source = ts.createSourceFile("fixture.ts", code, ts.ScriptTarget.Latest, true);
  let selected: ts.CallExpression | undefined;
  const visit = (node: ts.Node): void => {
    if (ts.isCallExpression(node) && node.expression.getText(source) === `base44.entities.${entity}.filter`) selected = node;
    ts.forEachChild(node, visit);
  };
  visit(source);
  if (!selected) throw new Error("fixture has no selected Base44 filter call");
  return extractQuerySemantics(selected, source, "filter")!;
}

describe("normalized Base44 query syntax", () => {
  it("captures the full literal query and excludes runtime operands", () => {
    const result = extract("filter", `{tenant_id: {$eq: user.id}, state: 'SECRET_OPERAND', cost: {$gte: 12.5, $in: [1, 2]}}, '-created_date,name', 25, 0, ['id','name']`);
    expect(result.completeness).toBe("complete");
    expect(result.arguments[1].value).toEqual({kind:"sort",encoding:"string",fields:[{name:"created_date",direction:"desc"},{name:"name",direction:"asc"}]});
    expect(result.arguments[2].value).toEqual({kind:"integer",value:25});
    expect(result.arguments[3].value).toEqual({kind:"integer",value:0});
    expect(JSON.stringify(result)).not.toContain("SECRET_OPERAND");
    expect(JSON.stringify(result)).not.toContain("12.5");
  });
  it("preserves omission, explicit null, and only source-unshadowed undefined", () => {
    expect(extract("list","").arguments.every(arg => arg.presence === "absent")).toBe(true);
    expect(extract("list","null, 0").arguments[0]).toMatchObject({presence:"supplied",value:{kind:"null"}});
    expect(extract("list","undefined").arguments[0]).toMatchObject({presence:"supplied",value:{kind:"undefined"}});
    const shadowed = extractProgram("const undefined = runtimeSort; base44.entities.Widget.filter({}, undefined);");
    expect(shadowed.completeness).toBe("unresolved");
    expect(shadowed.gaps).toContain("query:sort_unresolved");
    expect(extract("deleteMany","").gaps).toContain("query:filter_missing");
  });
  it("records direct runtime references as complete deferred wire values", () => {
    const result = extract("filter", "{tenant_id: tenantId, owner_id: user.id, selected_id: ids[index], keyed: rows['fixed'], contextual: this.user.id}");
    expect(result).toMatchObject({
      schemaVersion: "88mph.entity-query.v2",
      completeness: "complete",
      gaps: [],
    });
    expect((result.arguments[0].value as any).entries.map((entry: any) => entry.operand.kind))
      .toEqual(["reference", "reference", "reference", "reference", "reference"]);
    expect(JSON.stringify(result)).not.toContain("tenantId");
    expect(JSON.stringify(result)).not.toContain("user.id");
    expect(JSON.stringify(result)).not.toContain("ids[index]");
  });
  it.each([
    "{id: helper().id}",
    "{id: values[makeKey()]}",
    "{id: helper()?.id}",
    "{id: values[helper().key]}",
  ])("keeps nested executable reference syntax blocked: %s", (args) => {
    const result = extract("filter", args);
    expect(result.completeness).toBe("unresolved");
    expect(result.gaps).toContain("query:operand_expression_unsupported");
  });
  it.each([
    ["filter", "query"], ["filter", "{...query}"], ["filter", "{[field]: 1}"],
    ["filter", "{$or: [{a:1},{a:2}]}"], ["filter", "{a: {$unknown: 1}}"],
    ["filter", "{a: {}}"], ["filter", "{a:1,a:2}"], ["filter", "{get a(){return 1}}"],
    ["filter", "{a: helper()}"], ["filter", "{a: {$in: [,1]}}"],
    ["list", "sort"], ["list", "'-id', count"], ["list", "'-id', -1"],
    ["list", "'-id', 1.5"], ["list", "'-id', 10, 0, fields"],
    ["list", "'-id', 10, 0, ['id', ...extra]"], ["list", "'-id', 10, 0, ['id'], 1"]
  ])("marks unsupported %s(%s) unresolved", (method,args) => {
    const result=extract(method,args);
    expect(result.completeness).toBe("unresolved");
    expect(result.gaps.length).toBeGreaterThan(0);
  });
  it("bounds size/depth and keeps source offsets stable", () => {
    expect(extract("filter",`{a: {$in: ${"[".repeat(20)}1${"]".repeat(20)}}}`).gaps).toContain("query:complexity_limit");
    expect(extract("filter",`{${Array.from({length:600},(_,i)=>`f${i}: 1`).join(",")}}`).gaps).toContain("query:complexity_limit");
    expect(extract("filter","{b: 1, a: user.id}").arguments[0].span).toEqual({start:30,end:48});
  });
  it('accepts static backtick strings and transparent satisfies wrappers', () => {
    const result = extract('filter', '({state: `SECRET_STATIC`} satisfies Filter), (`-id` satisfies string), (25 satisfies number), 0, [`id`]');
    expect(result.completeness).toBe('complete');
    expect(result.arguments[1].value).toEqual({kind:'sort',encoding:'string',fields:[{name:'id',direction:'desc'}]});
    expect(result.arguments[4].value).toEqual({kind:'selection',encoding:'array',fields:['id']});
    expect(JSON.stringify(result)).not.toContain('SECRET_STATIC');
    expect(extract('filter','{state: `prefix-${dynamic}` }').completeness).toBe('unresolved');
  });
  it('bounds transparent wrapper traversal before it can recurse without limit', () => {
    const result = extract('filter', '({state: "open"})' + '!'.repeat(60));
    expect(result.completeness).toBe('unresolved');
    expect(result.gaps).toContain('query:complexity_limit');
  });

  it('retains full signed numeric predicate spans without storing the number', () => {
    const args = '{balance: -98765.5, cost: {$gte: +12345, $in: [-2, +3]}}';
    const result = extract('filter', args);
    expect(result.completeness).toBe('complete');
    const operand = (result.arguments[0].value as any).entries[0].operand;
    const code = `base44.entities.Widget.filter(${args});`;
    expect(operand).toMatchObject({kind:'literal',type:'number'});
    expect(code.slice(operand.span.start, operand.span.end)).toBe('-98765.5');
    expect(JSON.stringify(result)).not.toContain('98765.5');
    expect(JSON.stringify(result)).not.toContain('12345');
    expect(extract('filter','{a: -runtimeValue}').completeness).toBe('unresolved');
    expect(extract('list',"'-id', -1").completeness).toBe('unresolved');
  });

  it("emits query v3 with source-derived always and conditional binding fields", () => {
    const result = extractProgram(`
function load(toolingItem, organizationId, preselectedPrintId) {
  const filters = { tooling_item_id: toolingItem.id, organization_id: organizationId };
  if (preselectedPrintId) filters.print_id = preselectedPrintId;
  return base44.entities.Widget.filter(filters);
}`);
    expect(result).toMatchObject({
      schemaVersion: "88mph.entity-query.v3",
      method: "filter",
      completeness: "complete",
      gaps: []
    });
    const entries = (result.arguments[0].value as any).entries;
    expect(entries.map((entry: any) => ({ field: entry.field, presence: entry.presence }))).toEqual([
      { field: "tooling_item_id", presence: "always" },
      { field: "organization_id", presence: "always" },
      { field: "print_id", presence: "conditional" }
    ]);
    expect(entries[2].derivation).toEqual([expect.objectContaining({ kind: "conditional-property-assignment" })]);
    expect(JSON.stringify(result)).not.toContain("preselectedPrintId");
  });

  it("binds conditional presence and branch loss into different descriptors", () => {
    const conditional = extractProgram(`
function load(value, flag) {
  const filters = { tenant_id: value };
  if (flag) filters.state = value;
  return base44.entities.Widget.filter(filters);
}`);
    const always = extractProgram(`
function load(value) {
  const filters = { tenant_id: value };
  filters.state = value;
  return base44.entities.Widget.filter(filters);
}`);
    expect(conditional.schemaVersion).toBe("88mph.entity-query.v3");
    expect(always.schemaVersion).toBe("88mph.entity-query.v3");
    expect((conditional.arguments[0].value as any).entries[1].presence).toBe("conditional");
    expect((always.arguments[0].value as any).entries[1].presence).toBe("always");
    expect(JSON.stringify(conditional)).not.toBe(JSON.stringify(always));
  });

  it.each([
    "inspect(filters);",
    "const alias = filters;",
    "filters.state ||= value;",
    "delete filters.state;",
    "filters.configure();",
    "queueMicrotask(() => { filters.state = value; });"
  ])("keeps mutated or escaped query bindings fail closed: %s", (mutation) => {
    const result = extractProgram(`
function load(value) {
  const filters = { tenant_id: value };
  ${mutation}
  return base44.entities.Widget.filter(filters);
}`);
    expect(result.schemaVersion).toBe("88mph.entity-query.v3");
    expect(result.completeness).toBe("unresolved");
    expect(result.gaps).toEqual(expect.arrayContaining([
      expect.stringMatching(/^query:(binding-mutation|binding-state-or-escape|binding-nested-capture)-unresolved$/)
    ]));
  });

});
