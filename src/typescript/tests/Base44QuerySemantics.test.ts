import ts from "typescript";
import { describe, expect, it } from "vitest";
import { extractQuerySemantics } from "../src/extractors/Base44QuerySemantics";

function extract(method: string, args: string) {
  const code = `base44.entities.Widget.${method}(${args});`;
  const source = ts.createSourceFile("fixture.ts", code, ts.ScriptTarget.Latest, true);
  const call = (source.statements[0] as ts.ExpressionStatement).expression as ts.CallExpression;
  return extractQuerySemantics(call, source, method)!;
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
  it("preserves omission and explicit null without treating a shadowable identifier as absent", () => {
    expect(extract("list","").arguments.every(arg => arg.presence === "absent")).toBe(true);
    expect(extract("list","null, 0").arguments[0]).toMatchObject({presence:"supplied",value:{kind:"null"}});
    expect(extract("list","undefined").completeness).toBe("unresolved");
    expect(extract("deleteMany","").gaps).toContain("query:filter_missing");
  });
  it.each([
    ["filter", "query"], ["filter", "{a: criteria}"], ["filter", "{...query}"], ["filter", "{[field]: 1}"],
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

});
