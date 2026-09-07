import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { describe, expect, it } from "vitest";
import { buildBase44Evidence, diffBase44Evidence } from "../src/base44/Base44EvidencePacket";
import { createFactId } from "../src/facts/FactFactory";
import { FactTypes } from "../src/facts/Models";

const shaA = "a".repeat(64);
const shaB = "b".repeat(64);

describe("Base44 source-bound static evidence", () => {
  it("preserves complete query shapes for transparent wrapped controls", async () => {
    const repo = await fixtureRepo();
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-query-wrappers-"));
    await fs.writeFile(path.join(repo, "src/wrapped-query.ts"),
      'import { base44 } from "@base44/sdk"; base44.entities.Order.filter(({state: `open`} satisfies Filter), (`-id` satisfies string), 25, 0, ([`id`] satisfies string[]));');
    const {packet} = await buildBase44Evidence(options(repo,out));
    const fact = packet.facts.find(f => f.factType === FactTypes.Base44EntityQuery && f.evidence.filePath === "src/wrapped-query.ts");
    expect(fact?.properties.completeness).toBe("complete");
    expect(JSON.parse(fact?.properties.querySemanticsJson ?? "null").completeness).toBe("complete");
    expect(JSON.parse(fact?.properties.fieldsJson ?? "[]")).toEqual(expect.arrayContaining([
      expect.objectContaining({name:"id",origin:"sort-argument"}),
      expect.objectContaining({name:"id",origin:"select-argument"})
    ]));
  });

  it("extracts SDK, function, entity, env, provider and migration surfaces without raw secrets or URLs", async () => {
    const repo = await fixtureRepo();
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-out-"));
    const { packet, result } = await buildBase44Evidence(options(repo, out));

    expect(packet.schemaVersion).toBe("tracemap.base44.static-evidence.v1");
    expect(packet.source.acceptedSourceSha256).toBe(shaA);
    expect(packet.source.acceptedTreeSha256).toBe(shaB);
    expect(packet.facts).toEqual(expect.arrayContaining([
      expect.objectContaining({ factType: FactTypes.Base44SdkImport }),
      expect.objectContaining({ factType: FactTypes.Base44FunctionInvocation, targetSymbol: "sendReceipt" }),
      expect.objectContaining({ factType: FactTypes.Base44EntityOperation, targetSymbol: "Order" }),
      expect.objectContaining({ factType: FactTypes.Base44EnvironmentAccess, targetSymbol: "PROVIDER_TOKEN" }),
      expect.objectContaining({ factType: FactTypes.Base44HttpTarget }),
      expect.objectContaining({ factType: FactTypes.Base44FunctionSurface, targetSymbol: "sendReceipt" }),
      expect.objectContaining({ factType: FactTypes.Base44MigrationSurface })
    ]));
    expect(packet.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.Base44SdkPrimitive, contractElement: "Analytics.track" }));
    for (const capability of [
      "analytics.track",
      "appLogs.logUserInApp",
      "users.inviteUser",
      "asServiceRole.integrations.Core.SendEmail",
      "asServiceRole.entities.Order.filter",
      "asServiceRole.functions.invoke"
    ]) {
      expect(packet.facts).toContainEqual(expect.objectContaining({
        factType: FactTypes.Base44SdkPrimitive,
        contractElement: capability
      }));
    }
    expect(packet.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.Base44SdkImport,
      contractElement: "@base44/sdk/dist/utils/axios-client"
    }));
    expect(packet.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.Base44EntityOperation,
      targetSymbol: "Order",
      properties: expect.objectContaining({ operationName: "filter" })
    }));
    const createPayload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "Order");
    expect(createPayload).toEqual(expect.objectContaining({
      ruleId: "base44.entity.payload.v1",
      properties: expect.objectContaining({
        argumentIndex: "0",
        completeness: "complete",
        constructionKind: "object-literal",
        operationName: "create",
        shapeVersion: "1"
      })
    }));
    expect(JSON.parse(createPayload?.properties.fieldsJson ?? "[]")).toEqual([
      expect.objectContaining({
        expressionType: "string-literal",
        name: "status",
        origin: "literal",
        presence: "unconditional",
        evidenceStartLine: expect.any(Number),
        evidenceEndLine: expect.any(Number),
        evidenceSnippetHash: expect.stringMatching(/^[0-9a-f]{64}$/)
      })
    ]);
    const filterShape = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityQuery
      && fact.targetSymbol === "Order"
      && fact.properties.operationName === "filter");
    expect(JSON.parse(filterShape?.properties.querySemanticsJson ?? "null")).toMatchObject({
      schemaVersion: "88mph.entity-query.v2", method: "filter", completeness: "complete"
    });
    for (const fact of result.facts.filter(f => f.properties.querySemanticsJson)) {
      const id = (properties: Record<string,string>) => createFactId(fact.scanId, fact.factType, fact.ruleId,
        fact.evidence.filePath, fact.evidence.startLine, fact.evidence.endLine,
        fact.projectPath, fact.sourceSymbol, fact.targetSymbol, fact.contractElement, properties);
      expect(fact.factId).toBe(id(fact.properties));
      const withoutQuery = {...fact.properties};
      delete withoutQuery.querySemanticsJson;
      expect(fact.factId).not.toBe(id(withoutQuery));
    }
    expect(JSON.parse(filterShape?.properties.fieldsJson ?? "[]")).toEqual([
      expect.objectContaining({ expressionType: "string-literal", name: "status", origin: "filter:literal", presence: "unconditional" })
    ]);
    const operations = packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation);
    const shapes = packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityPayload || fact.factType === FactTypes.Base44EntityQuery);
    expect(shapes).toHaveLength(operations.length);
    expect(shapes.map((fact) => fact.properties.operationEvidenceId).sort()).toEqual(
      operations.map((fact) => fact.properties.operationEvidenceId).sort()
    );
    const deleteManyShape = shapes.find((fact) => fact.properties.operationName === "deleteMany");
    expect(JSON.parse(deleteManyShape?.properties.fieldsJson ?? "[]")).toEqual([
      expect.objectContaining({ expressionType: "string-literal", name: "status", origin: "filter:literal", presence: "unconditional" })
    ]);
    const importShape = shapes.find((fact) => fact.properties.operationName === "importEntities");
    expect(importShape?.properties).toEqual(expect.objectContaining({
      argumentRole: "import-file",
      completeness: "unresolved",
      analysisGapsJson: '["import-file-payload-not-entity-shape"]'
    }));
    expect(packet.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.Base44FunctionInvocation,
      targetSymbol: "serviceFunction"
    }));
    expect(packet.facts.filter((fact) => (
      fact.factType === FactTypes.Base44FunctionInvocation
      && fact.targetSymbol === "chainedServiceFunction"
    ))).toHaveLength(1);
    expect(packet.facts).not.toContainEqual(expect.objectContaining({
      factType: FactTypes.Base44FunctionInvocation,
      targetSymbol: "helperFunction"
    }));
    expect(packet.facts).not.toContainEqual(expect.objectContaining({
      factType: FactTypes.Base44EntityOperation,
      targetSymbol: "Helper"
    }));
    expect(packet.facts[0]).toEqual(expect.objectContaining({
      repo: expect.any(String),
      commitSha: expect.stringMatching(/^[0-9a-f]{40}$/),
      lineSpan: { startLine: expect.any(Number), endLine: expect.any(Number) }
    }));
    expect(packet.facts.some((fact) => fact.evidence.filePath === "src/unrelated.ts" && fact.factType === FactTypes.Base44SdkPrimitive)).toBe(false);
    expect(packet.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.Base44FunctionInvocation,
      targetSymbol: "wrappedFunction",
      evidence: expect.objectContaining({ filePath: "src/wrapped-consumer.ts" })
    }));
    expect(packet.facts.some((fact) => fact.evidence.filePath === "reports/query.sql" && fact.factType === FactTypes.Base44MigrationSurface)).toBe(false);
    const migration = packet.facts.find((fact) => fact.factType === FactTypes.Base44MigrationSurface);
    expect(migration?.properties.statementKinds).toBe("create-policy;create-table");
    const serialized = JSON.stringify(packet);
    expect(serialized).not.toContain("secret-value");
    expect(serialized).not.toContain("provider.example/private/path");
    expect(packet.facts.find((fact) => fact.factType === FactTypes.Base44HttpTarget)?.properties.originSha256).toMatch(/^[0-9a-f]{64}$/);
    for (const name of ["base44-evidence.json", "base44-evidence.md", "base44-evidence.html"]) await expect(fs.stat(path.join(out, name))).resolves.toBeTruthy();
  });

  it("follows only callsite-proven SDK clients through imported helper parameters", async () => {
    const repo = await injectedClientFixtureRepo();
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-injected-client-out-"));
    const { packet } = await buildBase44Evidence(options(repo, out));
    const helperOperations = packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.evidence.filePath === "src/helper.ts");

    expect(helperOperations.map((fact) => `${fact.targetSymbol}.${fact.properties.operationName}`).sort()).toEqual([
      "ForwardedItem.list",
      "LocalAliasItem.list",
      "MaterialTypeVendor.create",
      "MaterialTypeVendor.filter",
      "NestedItem.list",
      "OverloadedItem.list",
      "RecursiveItem.list",
      "Vendor.list"
    ]);
    expect(helperOperations.every((fact) => fact.properties.clientBindingKind === "callsite-proven-parameter")).toBe(true);
    expect(packet.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.Base44EntityOperation,
      targetSymbol: "ForwardedItem",
      properties: expect.objectContaining({ clientBindingKind: "callsite-proven-parameter" })
    }));
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityOperation
      && ["UnprovenItem", "ConflictedItem", "ShadowedItem", "LexicalShadowItem", "ArgumentShadowItem",
        "ReassignedAliasItem", "ReassignedParameterItem", "StaleHelperItem", "CatchShadowItem",
        "StaleDeclaredHelperItem", "LoopShadowItem", "ClassShadowItem", "FunctionShadowItem",
        "ConflictingRecursiveItem"].includes(fact.targetSymbol ?? ""))).toBe(false);
    expect(packet.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.Base44HttpTarget,
      evidence: expect.objectContaining({ filePath: "src/helper.ts" })
    }));
    expect(packet.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.Base44EnvironmentAccess,
      targetSymbol: "HELPER_TOKEN",
      evidence: expect.objectContaining({ filePath: "src/helper.ts" })
    }));
  });

  it("emits deterministic payload presence, type, binding, spread and ambiguity evidence without values", async () => {
    const repo = await payloadFixtureRepo();
    const firstOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-payload-first-"));
    const secondOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-payload-second-"));
    const first = await buildBase44Evidence(options(repo, firstOut));
    const second = await buildBase44Evidence(options(repo, secondOut));
    const payloadFacts = first.packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityPayload);

    expect(payloadFacts).toHaveLength(9);
    expect(payloadFacts.map((fact) => fact.factId)).toEqual(
      second.packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityPayload).map((fact) => fact.factId)
    );

    const tooling = payloadFacts.find((fact) => fact.targetSymbol === "ToolingItem");
    expect(tooling?.properties.completeness).toBe("partial");
    expect(tooling?.properties.constructionKind).toBe("identifier:object-literal");
    expect(JSON.parse(tooling?.properties.fieldsJson ?? "[]")).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "name", presence: "unconditional", expressionType: "identifier-reference" }),
      expect.objectContaining({ name: "quantity", presence: "spread-derived", expressionType: "integer-number-literal" }),
      expect.objectContaining({ name: "job_cost", presence: "conditional", expressionType: "numeric-binary-expression" }),
      expect.objectContaining({ name: "decimal_cost", presence: "unconditional", expressionType: "decimal-number-literal" }),
      expect.objectContaining({ name: "<dynamic>", presence: "dynamic-computed" }),
      expect.objectContaining({ name: "assigned_after_init", presence: "unconditional", expressionType: "integer-number-literal" }),
      expect.objectContaining({ name: "conditional_assignment", presence: "conditional" })
    ]));
    expect(JSON.parse(tooling?.properties.analysisGapsJson ?? "[]")).toContain("dynamic-computed-property");
    expect(JSON.parse(tooling?.properties.candidateBindingsJson ?? "[]")).toEqual(expect.arrayContaining(["binding:defaults", "binding:payload"]));
    expect(tooling?.properties.fieldsJson).not.toContain("shadow_only");
    expect(tooling?.properties.fieldsJson).not.toContain("phantom_nested_mutation");
    expect(JSON.parse(tooling?.properties.fieldsJson ?? "[]")).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "compound_assignment", presence: "conditional" }),
      expect.objectContaining({ name: "short_circuit_assignment", presence: "conditional" }),
      expect.objectContaining({ name: "conditional_object_assign", presence: "conditional" }),
      expect.objectContaining({ name: "lookup", origin: "property:records.<literal-key>" })
    ]));
    expect(JSON.parse(tooling?.properties.analysisGapsJson ?? "[]")).toContain("compound-property-assignment:??=");
    expect(tooling?.properties.fieldsJson).not.toContain("deleted_before_call");

    const bulk = payloadFacts.find((fact) => fact.targetSymbol === "PurchasedPart");
    expect(JSON.parse(bulk?.properties.fieldsJson ?? "[]")).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "sku", presence: "conditional" }),
      expect.objectContaining({ name: "price", presence: "conditional", expressionType: "integer-number-literal" })
    ]));
    expect(JSON.parse(bulk?.properties.fieldsJson ?? "[]")).not.toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "old_sku" })
    ]));
    expect(JSON.parse(bulk?.properties.analysisGapsJson ?? "[]")).toContain("array-spread-unresolved");

    const organization = payloadFacts.find((fact) => fact.targetSymbol === "Organization");
    expect(organization?.properties.completeness).toBe("partial");
    expect(JSON.parse(organization?.properties.fieldsJson ?? "[]")).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "status", presence: "conditional", origin: expect.stringContaining("logical-right") })
    ]));
    expect(JSON.parse(organization?.properties.analysisGapsJson ?? "[]")).toContain("binding-initializer-unresolved");

    const captured = payloadFacts.find((fact) => fact.targetSymbol === "CapturedItem");
    expect(captured?.properties.completeness).toBe("partial");
    expect(JSON.parse(captured?.properties.fieldsJson ?? "[]")).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "captured_before_reassignment", presence: "unconditional" })
    ]));
    expect(captured?.properties.fieldsJson).not.toContain("after_capture");
    expect(JSON.parse(captured?.properties.analysisGapsJson ?? "[]")).toContain("post-capture-alias-mutation-unresolved");

    const conditionalRows = payloadFacts.find((fact) => fact.targetSymbol === "ConditionalRows");
    expect(JSON.parse(conditionalRows?.properties.fieldsJson ?? "[]")).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "conditional_a", presence: "conditional" }),
      expect.objectContaining({ name: "conditional_b", presence: "conditional" })
    ]));

    const duplicateRows = payloadFacts.find((fact) => fact.targetSymbol === "DuplicateRows");
    expect(JSON.parse(duplicateRows?.properties.fieldsJson ?? "[]")).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "duplicate_a", presence: "conditional" }),
      expect.objectContaining({ name: "second_only", presence: "conditional" })
    ]));

    const conditionalPayload = payloadFacts.find((fact) => fact.targetSymbol === "ConditionalPayload");
    expect(JSON.parse(conditionalPayload?.properties.fieldsJson ?? "[]")).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "late_conditional", presence: "conditional" })
    ]));

    const moduleScoped = payloadFacts.find((fact) => fact.targetSymbol === "ModuleScopedItem");
    expect(moduleScoped?.properties.completeness).toBe("partial");
    expect(JSON.parse(moduleScoped?.properties.analysisGapsJson ?? "[]")).toContain("binding-cross-execution-scope");

    const unaryAlias = payloadFacts.find((fact) => fact.targetSymbol === "UnaryAliasItem");
    expect(unaryAlias?.properties.completeness).toBe("partial");
    expect(JSON.parse(unaryAlias?.properties.analysisGapsJson ?? "[]")).toContain("post-capture-alias-mutation-unresolved");

    const query = first.packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityQuery && fact.targetSymbol === "ToolingItem");
    expect(query?.properties.completeness).toBe("partial");
    expect(JSON.parse(query?.properties.fieldsJson ?? "[]")).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "status", origin: "filter:literal" }),
      expect.objectContaining({ name: "created_date", origin: "sort-argument" }),
      expect.objectContaining({ name: "id", origin: "select-argument" })
    ]));
    expect(JSON.parse(query?.properties.analysisGapsJson ?? "[]")).toEqual(expect.arrayContaining(["spread:binding-initializer-unresolved"]));

    const serialized = JSON.stringify(first.packet);
    for (const prohibited of ["secret-tool-name", "secret-status", "private-dynamic-field", "SKU-PRIVATE", "private-customer-id", "phantom-private-value"]) {
      expect(serialized).not.toContain(prohibited);
    }
  });

  it("derives payload fields from source-proven React Query mutation callsites", async () => {
    const { packet, replayPacket } = await mutationHookFixture(`
export function Screen(raw) {
  const createItem = useWrite({
    mutationFn: (payload) => base44.entities.DirectItem.create(payload)
  });
  createItem.mutate({ always_present: 1, sometimes_present: raw });
  createItem.mutate({ always_present: 2 });

  const updateItem = ReactQuery.useMutation({
    mutationFn: ({ id, data }) => base44.entities.NestedItem.update(id, data)
  });
  updateItem.mutate({ ...raw, id: "redacted", data: { status: "redacted", total: Number(raw) } });
}
`, true);

    const direct = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "DirectItem")!;
    expect(direct.properties.completeness).toBe("complete");
    expect(direct.properties.constructionKind).toBe("react-query-mutation-callsites");
    expect(JSON.parse(direct.properties.fieldsJson)).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "always_present", presence: "unconditional", expressionType: "integer-number-literal" }),
      expect.objectContaining({ name: "sometimes_present", presence: "conditional", expressionType: "identifier-reference" })
    ]));
    expect(JSON.parse(direct.properties.candidateBindingsJson)).toEqual(expect.arrayContaining([
      "binding:payload", "mutation-hook:createItem"
    ]));

    const nested = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "NestedItem")!;
    expect(nested.properties.completeness).toBe("complete");
    expect(JSON.parse(nested.properties.fieldsJson)).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "status", presence: "unconditional", expressionType: "string-literal" }),
      expect.objectContaining({ name: "total", presence: "unconditional", expressionType: "number-coercion-call" })
    ]));
    expect(replayPacket?.facts.filter((fact) => fact.factType === FactTypes.Base44EntityPayload).map((fact) => fact.factId)).toEqual(
      packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityPayload).map((fact) => fact.factId)
    );
    expect(JSON.stringify(packet)).not.toContain("redacted");
  });

  it("keeps mutation-hook escapes and dynamic arguments as typed payload gaps", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen(input) {
  const save = useWrite({ mutationFn: (payload) => base44.entities.ReviewItem.create(payload) });
  save.mutate({ observed_only: 1 });
  save.mutate(input);
  const escaped = save.mutate;

  const spoofed = useMutation({ mutationFn: (payload) => base44.entities.SpoofedItem.create(payload) });
  spoofed.mutate({ invented: 1 });
  return escaped;
}

export function ShadowedImport(useWrite) {
  const shadowed = useWrite({ mutationFn: (payload) => base44.entities.ShadowedHookItem.create(payload) });
  shadowed.mutate({ invented_shadow: 1 });
}

export function LaterShadowedImport() {
  const shadowed = useWrite({ mutationFn: (payload) => base44.entities.LaterShadowedHookItem.create(payload) });
  shadowed.mutate({ invented_later_shadow: 1 });
  const useWrite = () => undefined;
}
`);
    const reviewed = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "ReviewItem")!;
    expect(reviewed.properties.completeness).toBe("partial");
    expect(reviewed.evidenceTier).toBe("Tier4Unknown");
    expect(JSON.parse(reviewed.properties.fieldsJson)).toEqual([
      expect.objectContaining({ name: "observed_only", presence: "conditional" })
    ]);
    expect(JSON.parse(reviewed.properties.analysisGapsJson)).toEqual(expect.arrayContaining([
      "binding-initializer-unresolved", "mutation-hook-method-escape-unresolved"
    ]));

    const spoofed = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "SpoofedItem")!;
    expect(spoofed.properties.completeness).toBe("unresolved");
    expect(JSON.parse(spoofed.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(spoofed.properties.analysisGapsJson)).toContain("binding-initializer-unresolved");

    const shadowed = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "ShadowedHookItem")!;
    expect(shadowed.properties.completeness).toBe("unresolved");
    expect(JSON.parse(shadowed.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(shadowed.properties.analysisGapsJson)).toContain("binding-initializer-unresolved");

    const laterShadowed = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "LaterShadowedHookItem")!;
    expect(laterShadowed.properties.completeness).toBe("unresolved");
    expect(JSON.parse(laterShadowed.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(laterShadowed.properties.analysisGapsJson)).toContain("binding-initializer-unresolved");
  });

  it("does not select a destructured mutation payload through a later unknown spread", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen(extra) {
  const save = useWrite({ mutationFn: ({ data }) => base44.entities.ReviewItem.create(data) });
  save.mutate({ data: { must_not_be_claimed: 1 }, ...extra });
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "ReviewItem")!;
    expect(payload.properties.completeness).toBe("unresolved");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(payload.properties.analysisGapsJson)).toEqual(expect.arrayContaining([
      "mutation-hook-destructured-spread-unresolved", "mutation-hook-callsite-incomplete"
    ]));
  });

  it.each([
    [
      `const save = useWrite({ mutationFn: (payload) => { { let payload; return base44.entities.BlockShadowItem.create(payload); } } });
       save.mutate({ must_not_be_projected: 1 });`,
      "BlockShadowItem"
    ],
    [
      `const save = useWrite({ mutationFn: (payload) => { try { throw new Error(); } catch (payload) { return base44.entities.CatchShadowItem.create(payload); } } });
       save.mutate({ must_not_be_projected: 1 });`,
      "CatchShadowItem"
    ],
    [
      `const save = useWrite({ mutationFn: (payload) => { { function payload() {}; return base44.entities.FunctionShadowItem.create(payload); } } });
       save.mutate({ must_not_be_projected: 1 });`,
      "FunctionShadowItem"
    ],
    [
      `const save = useWrite({ mutationFn: (payload) => { { class payload {}; return base44.entities.ClassShadowItem.create(payload); } } });
       save.mutate({ must_not_be_projected: 1 });`,
      "ClassShadowItem"
    ]
  ])("rejects a locally shadowed mutation callback parameter: %s", async (body, entity) => {
    const { packet } = await mutationHookFixture(`export function Screen() { ${body} }`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === entity)!;
    expect(payload.properties.completeness).toBe("unresolved");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("mutation-hook-parameter-shadowed");
  });

  it("recognizes a callback-parameter class shadow across switch cases", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen(kind) {
  const save = useWrite({ mutationFn: (payload) => {
    switch (kind) {
      case "shadow": class payload {}; break;
      default: return base44.entities.CaseShadowItem.create(payload);
    }
  } });
  save.mutate({ must_not_be_projected: 1 });
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "CaseShadowItem")!;
    expect(payload.properties.completeness).toBe("unresolved");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("mutation-hook-parameter-shadowed");
  });

  it.each([
    "payload = { current: 1 };",
    "payload.current = 1;",
    "delete payload.stale;",
    "inspect(payload);",
    "const alias = payload;"
  ])("does not project a mutation callsite through changed or escaped callback state: %s", async (mutation) => {
    const { packet } = await mutationHookFixture(`
export function Screen() {
  const save = useWrite({ mutationFn: (payload) => {
    ${mutation}
    return base44.entities.MutatedParameterItem.create(payload);
  } });
  save.mutate({ stale: 1 });
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "MutatedParameterItem")!;
    expect(payload.properties.completeness).toBe("unresolved");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("mutation-hook-parameter-state-unresolved");
  });

  it.each([
    "data = { current: 1 };",
    "data.current = 1;",
    "inspect(data);"
  ])("does not project a mutation callsite through changed or escaped destructured callback state: %s", async (mutation) => {
    const { packet } = await mutationHookFixture(`
export function Screen() {
  const save = useWrite({ mutationFn: ({ data }) => {
    ${mutation}
    return base44.entities.DestructuredParameterItem.create(data);
  } });
  save.mutate({ data: { stale: 1 } });
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "DestructuredParameterItem")!;
    expect(payload.properties.completeness).toBe("unresolved");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("mutation-hook-parameter-state-unresolved");
  });

  it("retains a typed gap when a nested function captures callback state before the SDK call", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen() {
  const save = useWrite({ mutationFn: (payload) => {
    const rewrite = () => { payload.current = 1; };
    rewrite();
    return base44.entities.NestedCaptureItem.create(payload);
  } });
  save.mutate({ stale: 1 });
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "NestedCaptureItem")!;
    expect(payload.properties.completeness).toBe("unresolved");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("mutation-hook-parameter-state-unresolved");
  });

  it("retains a typed gap for callback state changed across a loop backedge", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen(items) {
  const save = useWrite({ mutationFn: (payload) => {
    for (const item of items) {
      base44.entities.LoopBackedgeItem.create(payload);
      payload.current = item;
    }
  } });
  save.mutate({ stale: 1 });
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "LoopBackedgeItem")!;
    expect(payload.properties.completeness).toBe("unresolved");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("mutation-hook-parameter-state-unresolved");
  });

  it("keeps a typed gap when the hook result is invoked through a computed member", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen(key) {
  const save = useWrite({ mutationFn: (payload) => base44.entities.ComputedHookItem.create(payload) });
  save[key]({ other: 1 });
  save.mutate({ known: 1 });
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "ComputedHookItem")!;
    expect(payload.properties.completeness).toBe("partial");
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("mutation-hook-computed-member-unresolved");
  });

  it.each([
    "{ function save() {}; save.mutate({ unrelated: 1 }); }",
    "{ class save { static mutate(_value) {} }; save.mutate({ unrelated: 1 }); }"
  ])("does not treat a function or class shadow as a hook-result callsite: %s", async (shadow) => {
    const { packet } = await mutationHookFixture(`
export function Screen() {
  const save = useWrite({ mutationFn: (payload) => base44.entities.HookShadowItem.create(payload) });
  ${shadow}
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "HookShadowItem")!;
    expect(payload.properties.completeness).toBe("unresolved");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("mutation-hook-callsite-missing");
  });

  it("does not confuse an outer same-named binding with a callback-local parameter", async () => {
    const { packet } = await mutationHookFixture(`
const payload = { unrelated_outer: 1 };
export function Screen() {
  const save = useWrite({ mutationFn: (payload) => base44.entities.CallbackParameterItem.create(payload) });
  save.mutate({ actual_field: 1 });
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "CallbackParameterItem")!;
    expect(payload.properties.completeness).toBe("complete");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([expect.objectContaining({ name: "actual_field" })]);
  });

  it("rejects callbacks that a later mutation option can replace while accepting the surviving callback", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen(extra, key) {
  const spreadOverride = useWrite({
    mutationFn: (payload) => base44.entities.SpreadOverrideItem.create(payload),
    ...extra
  });
  spreadOverride.mutate({ must_not_be_projected: 1 });

  const duplicateOverride = useWrite({
    mutationFn: (payload) => base44.entities.DuplicateOldItem.create(payload),
    mutationFn: (payload) => base44.entities.DuplicateCurrentItem.create(payload)
  });
  duplicateOverride.mutate({ current_field: 1 });

  const dynamicOverride = useWrite({
    mutationFn: (payload) => base44.entities.DynamicOverrideItem.create(payload),
    [key]: extra
  });
  dynamicOverride.mutate({ must_not_be_projected: 1 });

  const explicitAfterSpread = useWrite({
    ...extra,
    mutationFn: (payload) => base44.entities.ExplicitCurrentItem.create(payload)
  });
  explicitAfterSpread.mutate({ accepted_field: 1 });
}
`);
    for (const [entity, gap] of [
      ["SpreadOverrideItem", "mutation-hook-callback-override-unresolved"],
      ["DuplicateOldItem", "mutation-hook-callback-overridden"],
      ["DynamicOverrideItem", "mutation-hook-callback-override-unresolved"]
    ]) {
      const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === entity)!;
      expect(payload.properties.completeness).toBe("unresolved");
      expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
      expect(JSON.parse(payload.properties.analysisGapsJson)).toContain(gap);
    }
    for (const entity of ["DuplicateCurrentItem", "ExplicitCurrentItem"]) {
      const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === entity)!;
      expect(payload.properties.completeness).toBe("complete");
      expect(JSON.parse(payload.properties.fieldsJson)).toEqual([expect.objectContaining({
        name: entity === "DuplicateCurrentItem" ? "current_field" : "accepted_field"
      })]);
    }
  });

  it.each([
    "vars.data = { current: 1 };",
    "delete vars.data;",
    "const alias = vars; alias.data = { current: 1 };",
    "inspect(vars);"
  ])("does not project stale fields through a mutated or escaped destructured wrapper: %s", async (mutation) => {
    const { packet } = await mutationHookFixture(`
export function Screen() {
  const save = useWrite({ mutationFn: ({ data }) => base44.entities.WrappedItem.create(data) });
  const vars = { data: { stale: 1 } };
  ${mutation}
  save.mutate(vars);
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "WrappedItem")!;
    expect(payload.properties.completeness).toBe("unresolved");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(payload.properties.analysisGapsJson)).toEqual(expect.arrayContaining([
      "mutation-hook-destructured-argument-binding-state-unresolved", "mutation-hook-callsite-incomplete"
    ]));
  });

  it.each([
    ["(...args) => base44.entities.RestItem.create(args)", "mutation-hook-rest-parameter-unsupported"],
    ["(variables, context) => base44.entities.SecondItem.create(context)", "mutation-hook-parameter-position-unsupported"],
    ["([payload]) => base44.entities.ArrayBindingItem.create(payload)", "mutation-hook-parameter-binding-pattern-unsupported"]
  ])("rejects unsupported mutation callback parameter semantics: %s", async (callback, gap) => {
    const { packet } = await mutationHookFixture(`
export function Screen() {
  const save = useWrite({ mutationFn: ${callback} });
  save.mutate({ must_not_be_projected: 1 });
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload)!;
    expect(payload.properties.completeness).toBe("unresolved");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain(gap);
  });

  it("breaks recursive self-hook analysis with a typed gap", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen() {
  const save = useWrite({ mutationFn: (payload) => {
    save.mutate(payload);
    return base44.entities.RecursiveItem.create(payload);
  } });
  save.mutate({ externally_observed: 1 });
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "RecursiveItem")!;
    expect(payload.properties.completeness).toBe("partial");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([
      expect.objectContaining({ name: "externally_observed", presence: "conditional" })
    ]);
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("mutation-hook-recursion-unresolved");
  });

  it.each([
    "const defaults = { branch_only: 1 }; base44.entities.ReviewItem.create(flag ? { ...defaults } : {});",
    "const defaults = { branch_only: 1 }; base44.entities.ReviewItem.create({ ...(flag ? { ...defaults } : {}) });",
    "const defaults = { branch_only: 1 }; base44.entities.ReviewItem.create(flag && { ...defaults });"
  ])("preserves branch-conditional presence through nested spreads: %s", async (body) => {
    const { packet } = await reviewFixture(body);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload)!;
    const fields = JSON.parse(payload.properties.fieldsJson);
    expect(fields).toEqual(expect.arrayContaining([expect.objectContaining({ name: "branch_only", presence: "conditional" })]));
    expect(fields.filter((field: { name: string; presence: string }) => field.name === "branch_only").every((field: { presence: string }) => field.presence === "conditional")).toBe(true);
  });

  it.each([
    ["const payload = { initial: 1 }; const alias = payload; alias.added = 2; base44.entities.ReviewItem.create(payload);", "binding-alias-escape-unresolved"],
    ["const payload = { initial: 1 }; mutate(payload); base44.entities.ReviewItem.create(payload);", "binding-call-escape-unresolved"],
    ["const payload = [{ initial: 1 }]; payload.push({ added: 2 }); base44.entities.ReviewItem.bulkCreate(payload);", "binding-method-call-unresolved"],
    ["const original = [{ initial: 1 }]; const payload = original; original.push({ added: 2 }); base44.entities.ReviewItem.bulkCreate(payload);", "post-capture-alias-mutation-unresolved"]
  ])("does not claim a complete shape after unmodeled reference use: %s", async (body, gap) => {
    const { packet } = await reviewFixture(body);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload)!;
    expect(payload.properties.completeness).toBe("partial");
    expect(payload.evidenceTier).toBe("Tier4Unknown");
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain(gap);
  });

  it.each([
    "const payload = { unrelated_outer: 1 }; function save(payload) { base44.entities.ReviewItem.create(payload); }",
    "const payload = { unrelated_outer: 1 }; function save({ payload }) { base44.entities.ReviewItem.create(payload); }",
    "const payload = { unrelated_outer: 1 }; { const { payload } = input; base44.entities.ReviewItem.create(payload); }",
    "const payload = { unrelated_outer: 1 }; { base44.entities.ReviewItem.create(payload); const payload = {}; }",
    "try {} catch (payload) {} const payload = { own_field: 1 }; { const { payload } = input; base44.entities.ReviewItem.create(payload); }"
  ])("does not derive fields from a shadowed outer binding: %s", async (body) => {
    const { packet } = await reviewFixture(body);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload)!;
    expect(payload.properties.completeness).toBe("unresolved");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
  });

  it.each([
    "const payload = { own_field: 1 }; { const payload = {}; mutate(payload); } base44.entities.ReviewItem.create(payload);",
    "const payload = { own_field: 1 }; function neverCalled() { mutate(payload); } base44.entities.ReviewItem.create(payload);",
    "const payload = { own_field: 1 }; base44.entities.ReviewItem.create(payload); mutate(payload);",
    "const payload = { own_field: 1 }; Object.assign({}, payload); base44.entities.ReviewItem.create(payload);",
    "const payload = { own_field: 1 }; try {} catch (payload) { mutate(payload); } base44.entities.ReviewItem.create(payload);"
  ])("preserves complete evidence outside the relevant binding and call interval: %s", async (body) => {
    const { packet } = await reviewFixture(body);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload)!;
    expect(payload.properties.completeness).toBe("complete");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([expect.objectContaining({ name: "own_field" })]);
  });

  it("keeps identical calls on one line distinct and linked through artifact serialization", async () => {
    const { packet, result } = await reviewFixture("base44.entities.ReviewItem.create({ field: 1 }); base44.entities.ReviewItem.create({ field: 1 });");
    const operations = packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation);
    const shapes = packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityPayload);
    expect(operations).toHaveLength(2);
    expect(shapes).toHaveLength(2);
    expect(new Set(operations.map((fact) => fact.properties.operationEvidenceId)).size).toBe(2);
    expect(new Set(shapes.map((fact) => fact.factId)).size).toBe(2);
    expect(shapes.map((fact) => fact.properties.operationEvidenceId).sort()).toEqual(operations.map((fact) => fact.properties.operationEvidenceId).sort());
    expect(result.facts.filter((fact) => fact.factType === FactTypes.Base44EntityPayload)).toHaveLength(2);
  });

  it("ignores schema prose but produces an exact payload-shape diff when executable payload code changes", async () => {
    const repo = await payloadFixtureRepo();
    const baselineOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-payload-baseline-"));
    await buildBase44Evidence(options(repo, baselineOut));

    await fs.writeFile(path.join(repo, "database-schema.md"), "# False authority\nToolingItem requires invented_field.\n");
    execFileSync("git", ["add", "database-schema.md"], { cwd: repo });
    execFileSync("git", ["commit", "-qm", "change schema prose"], { cwd: repo });
    const proseOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-payload-prose-"));
    await buildBase44Evidence(options(repo, proseOut));
    const proseDiff = await diffBase44Evidence(path.join(baselineOut, "base44-evidence.json"), path.join(proseOut, "base44-evidence.json"), path.join(proseOut, "diff.json"));
    expect(proseDiff.added).toHaveLength(0);
    expect(proseDiff.removed).toHaveLength(0);

    const appPath = path.join(repo, "src/app.ts");
    const source = await fs.readFile(appPath, "utf8");
    await fs.writeFile(appPath, source.replace("assigned_after_init = 3", "new_payload_field = 3"));
    execFileSync("git", ["add", "src/app.ts"], { cwd: repo });
    execFileSync("git", ["commit", "-qm", "change executable payload"], { cwd: repo });
    const editedOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-payload-edited-"));
    await buildBase44Evidence(options(repo, editedOut));
    const payloadDiff = await diffBase44Evidence(path.join(proseOut, "base44-evidence.json"), path.join(editedOut, "base44-evidence.json"), path.join(editedOut, "diff.json"));
    const addedPayload = payloadDiff.added.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "ToolingItem");
    const removedPayload = payloadDiff.removed.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "ToolingItem");
    expect(JSON.parse(addedPayload?.properties.fieldsJson ?? "[]")).toEqual(expect.arrayContaining([expect.objectContaining({ name: "new_payload_field" })]));
    expect(JSON.parse(removedPayload?.properties.fieldsJson ?? "[]")).toEqual(expect.arrayContaining([expect.objectContaining({ name: "assigned_after_init" })]));
  });

  it("fails closed on invalid source identities and marks reduced-coverage diffs", async () => {
    const repo = await fixtureRepo();
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-out-"));
    await expect(buildBase44Evidence({ ...options(repo, out), acceptedSourceSha256: "not-a-sha" })).rejects.toThrow("64-character SHA-256");

    const beforeOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-before-"));
    const afterOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-after-"));
    await buildBase44Evidence(options(repo, beforeOut));
    const before = await buildBase44Evidence(options(repo, beforeOut));
    before.packet.coverage.knownGaps = ["replaced coverage gap"];
    await fs.writeFile(path.join(beforeOut, "base44-evidence.json"), `${JSON.stringify(before.packet, null, 2)}\n`);
    const after = await buildBase44Evidence(options(repo, afterOut));
    after.packet.coverage.knownGaps = ["seeded coverage loss"];
    await fs.writeFile(path.join(afterOut, "base44-evidence.json"), `${JSON.stringify(after.packet, null, 2)}\n`);
    const diff = await diffBase44Evidence(path.join(beforeOut, "base44-evidence.json"), path.join(afterOut, "base44-evidence.json"), path.join(afterOut, "diff.json"));
    expect(diff.coverageReduced).toBe(true);
    expect(diff.coverageEvidence).toEqual(expect.objectContaining({
      beforeAnalysisLevel: "Level3SyntaxAnalysis",
      afterAnalysisLevel: "Level3SyntaxAnalysis",
      addedKnownGaps: ["seeded coverage loss"],
      removedKnownGaps: ["replaced coverage gap"]
    }));
    expect(diff.limitations.join(" ")).toContain("must not be interpreted as clean absence");

    const extensionlessOutput = path.join(afterOut, "extensionless-diff");
    await diffBase44Evidence(path.join(beforeOut, "base44-evidence.json"), path.join(afterOut, "base44-evidence.json"), extensionlessOutput);
    expect(JSON.parse(await fs.readFile(extensionlessOutput, "utf8")).schemaVersion).toBe("tracemap.base44.static-diff.v1");
    expect(await fs.readFile(`${extensionlessOutput}.md`, "utf8")).toContain("# TraceMap Base44 Static Diff");
  });

  it("does not classify conventional migrations as Base44 without a Base44 signal", async () => {
    const repo = await nonBase44MigrationRepo();
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-non-base44-out-"));
    const { packet } = await buildBase44Evidence(options(repo, out));

    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44MigrationSurface)).toBe(false);
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44HttpTarget)).toBe(false);
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EnvironmentAccess)).toBe(false);
  });
});

function options(repoPath: string, outputPath: string) {
  return {
    repoPath,
    outputPath,
    projectPaths: [],
    includeGlobs: [],
    excludeGlobs: [],
    maxFileByteSize: 1024 * 1024,
    semantic: false,
    acceptedSourceSha256: shaA,
    acceptedTreeSha256: shaB,
    coverageLabel: "complete-controlled-fixture"
  };
}

async function fixtureRepo(): Promise<string> {
  const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-fixture-"));
  await fs.mkdir(path.join(repo, "src"), { recursive: true });
  await fs.mkdir(path.join(repo, "base44/functions/sendReceipt"), { recursive: true });
  await fs.mkdir(path.join(repo, "base44/migrations"), { recursive: true });
  await fs.mkdir(path.join(repo, "reports"), { recursive: true });
  await fs.writeFile(path.join(repo, "package.json"), JSON.stringify({ dependencies: { "@base44/sdk": "0.8.3" } }));
  await fs.writeFile(path.join(repo, "src/app.ts"), `import { base44 } from "@base44/sdk";
export async function run() {
  await base44.auth.me();
  await base44.entities.Order.create({ status: "new" });
  await base44.functions.invoke("sendReceipt", { id: "1" });
  return fetch("https://provider.example/private/path?token=secret-value");
}
`);
  await fs.writeFile(path.join(repo, "src/analytics.jsx"), `import { base44 } from "@base44/sdk";\nexport const send = () => base44.Analytics.track("opened");\n`);
  await fs.writeFile(path.join(repo, "src/current-sdk-surfaces.ts"), `import { base44 } from "@base44/sdk";
import sdkAxios from "@base44/sdk/dist/utils/axios-client";
export async function currentSdkSurfaces() {
  base44.analytics.track("opened");
  base44.appLogs.logUserInApp("opened");
  base44.users.inviteUser("owned@example.invalid");
  base44.asServiceRole.integrations.Core.SendEmail({ to: "owned@example.invalid" });
  base44.asServiceRole.entities.Order.filter({ status: "open" });
  base44.asServiceRole.entities.Order.deleteMany({ status: "retired" });
  base44.asServiceRole.entities.Order.importEntities(file);
  base44.asServiceRole.functions.invoke("serviceFunction");
  await base44.asServiceRole.functions.invoke("chainedServiceFunction").then(() => undefined);
  base44.integrations.functions.invoke("helperFunction");
  base44.integrations.entities.Helper.filter({ status: "open" });
  return sdkAxios;
}
`);
  await fs.writeFile(path.join(repo, "src/unrelated.ts"), `const base44 = { auth: { me: () => "not-base44" } };\nconst supabase = otherSdk.createClient();\nbase44.auth.me();\nsupabase.auth.getUser();\nsupabase.functions.invoke("not-base44");\n`);
  await fs.writeFile(path.join(repo, "src/wrapper.ts"), `import { createClient as makeBase44 } from "@base44/sdk";\nexport const wrappedClient = makeBase44({ appId: "fixture" });\n`);
  await fs.writeFile(path.join(repo, "src/wrapped-consumer.ts"), `import { wrappedClient as client } from "./wrapper";\nexport const invoke = () => client.functions.invoke("wrappedFunction");\n`);
  await fs.writeFile(path.join(repo, "base44/functions/sendReceipt/entry.ts"), `Deno.serve(async () => {
  const token = Deno.env.get("PROVIDER_TOKEN");
  return new Response(token ? "configured" : "missing");
});
`);
  await fs.writeFile(path.join(repo, "base44/functions/sendReceipt/server.ts"), `import axios from "axios";\nimport { createClientFromRequest } from "npm:@base44/sdk@0.8.39";\nconst serviceClient = createClientFromRequest(new Request("https://example.invalid"));\nexport const load = async () => { await axios.post("https://mail.example.invalid/send", {}); return serviceClient.entities.Order.list(); };\n`);
  await fs.writeFile(path.join(repo, "base44/migrations/001.sql"), "-- drop table retained;\n/* alter table ignored; */\ncreate table orders (id text primary key);\ncreate policy orders_rls on orders;\n");
  await fs.writeFile(path.join(repo, "reports/query.sql"), "select * from orders;\n");
  execFileSync("git", ["init", "-q"], { cwd: repo });
  execFileSync("git", ["config", "user.email", "tracemap@example.invalid"], { cwd: repo });
  execFileSync("git", ["config", "user.name", "TraceMap Test"], { cwd: repo });
  execFileSync("git", ["add", "."], { cwd: repo });
  execFileSync("git", ["commit", "-qm", "fixture"], { cwd: repo });
  return repo;
}

async function nonBase44MigrationRepo(): Promise<string> {
  const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-non-base44-fixture-"));
  await fs.mkdir(path.join(repo, "src"), { recursive: true });
  await fs.mkdir(path.join(repo, "db/migrations"), { recursive: true });
  await fs.writeFile(path.join(repo, "src/app.ts"), 'const token = Deno.env.get("GENERIC_TOKEN");\nexport const load = () => fetch("https://ordinary.example.invalid/path");\n');
  await fs.writeFile(path.join(repo, "db/migrations/001.sql"), "create table orders (id text primary key);\n");
  execFileSync("git", ["init", "-q"], { cwd: repo });
  execFileSync("git", ["config", "user.email", "tracemap@example.invalid"], { cwd: repo });
  execFileSync("git", ["config", "user.name", "TraceMap Test"], { cwd: repo });
  execFileSync("git", ["add", "."], { cwd: repo });
  execFileSync("git", ["commit", "-qm", "fixture"], { cwd: repo });
  return repo;
}

async function injectedClientFixtureRepo(): Promise<string> {
  const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-injected-client-fixture-"));
  await fs.mkdir(path.join(repo, "src"), { recursive: true });
  await fs.writeFile(path.join(repo, "package.json"), JSON.stringify({ dependencies: { "@base44/sdk": "0.8.5" } }));
  await fs.writeFile(path.join(repo, "src/helper.ts"), `
export async function useClient(materialId, client, organizationId) {
  const vendors = await client.entities.Vendor.list("name", 1000);
  const existing = await client.entities.MaterialTypeVendor.filter({ material_id: materialId, vendor_id: vendors[0].id });
  if (!existing.length) await client.entities.MaterialTypeVendor.create({ material_id: materialId, vendor_id: vendors[0].id, organization_id: organizationId });
  function shadow(client) { return client.entities.ShadowedItem.list(); }
}
export function forward(client) { return forwardedAgain(client); }
function forwardedAgain(client) { return client.entities.ForwardedItem.list(); }
export function unproven(base44) { return base44.entities.UnprovenItem.list(); }
export function conflicted(client) { return client.entities.ConflictedItem.list(); }
export function lexicalShadow(client) { return client.entities.LexicalShadowItem.list(); }
export function outerShadow(client) { { const client = { entities: {} }; return argumentSink(client); } }
function argumentSink(client) { return client.entities.ArgumentShadowItem.list(); }
export function reassignedAliasSink(client) { return client.entities.ReassignedAliasItem.list(); }
export function reassignedParameter(client) { client = { entities: {} }; return client.entities.ReassignedParameterItem.list(); }
export function recursive(client, count) { return count > 0 ? recursive(client, count - 1) : client.entities.RecursiveItem.list(); }
export function conflictingRecursive(client, count) {
  return count > 0 ? conflictingRecursive(client.entities, count - 1) : client.entities.ConflictingRecursiveItem.list();
}
export function nestedForward(client) {
  const nested = (nestedClient) => nestedClient.entities.NestedItem.list();
  return nested(client);
}
export function localAliasSink(client) { return client.entities.LocalAliasItem.list(); }
export function overloaded(client: unknown): unknown;
export function overloaded(client) { return client.entities.OverloadedItem.list(); }
export function helperContext(client) {
  Deno.env.get("HELPER_TOKEN");
  fetch("https://helper.example.invalid/path");
  return client;
}
export function blockShadows(client) {
  try { throw new Error(); } catch (client) { client.entities.CatchShadowItem.list(); }
  for (const client of []) client.entities.LoopShadowItem.list();
  { class client {} client.entities.ClassShadowItem.list(); }
  { function client() {} client.entities.FunctionShadowItem.list(); }
}
`);
  await fs.writeFile(path.join(repo, "src/app.ts"), `
import { base44 } from "@base44/sdk";
import { useClient as link, forward, conflicted, lexicalShadow, outerShadow, reassignedAliasSink,
  reassignedParameter, recursive, conflictingRecursive, nestedForward, localAliasSink, overloaded, helperContext, blockShadows } from "./helper";
export async function run(id, org) {
  await link(id, base44, org);
  await forward(base44);
  await conflicted(base44);
  await conflicted({ entities: {} });
  { const base44 = { entities: {} }; await lexicalShadow(base44); }
  await outerShadow(base44);
  let mutableClient = base44;
  mutableClient = { entities: {} };
  await reassignedAliasSink(mutableClient);
  await reassignedParameter(base44);
  await recursive(base44, 2);
  await conflictingRecursive(base44, 2);
  await nestedForward(base44);
  const localClient = base44;
  await localAliasSink(localClient);
  await overloaded(base44);
  await helperContext(base44);
  await blockShadows(base44);
  let staleHelper = (client) => client.entities.StaleHelperItem.list();
  staleHelper = () => undefined;
  staleHelper(base44);
  function staleDeclared(client) { return client.entities.StaleDeclaredHelperItem.list(); }
  staleDeclared = () => undefined;
  staleDeclared(base44);
}
`);
  execFileSync("git", ["init", "-q"], { cwd: repo });
  execFileSync("git", ["config", "user.email", "tracemap@example.invalid"], { cwd: repo });
  execFileSync("git", ["config", "user.name", "TraceMap Test"], { cwd: repo });
  execFileSync("git", ["add", "."], { cwd: repo });
  execFileSync("git", ["commit", "-qm", "fixture"], { cwd: repo });
  return repo;
}

async function payloadFixtureRepo(): Promise<string> {
  const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-payload-fixture-"));
  await fs.mkdir(path.join(repo, "src"), { recursive: true });
  await fs.writeFile(path.join(repo, "package.json"), JSON.stringify({ dependencies: { "@base44/sdk": "0.8.5" } }));
  await fs.writeFile(path.join(repo, "src/app.ts"), `import { base44 } from "@base44/sdk";
const modulePayload = { module_initial: 1 };
modulePayload.module_assigned = 2;
export async function run(name, includeCost, hours, rate, dynamicKey, criteria, nextStatus, records, rows, maybeUpdate) {
  const defaults = { quantity: 1 };
  const payload = {
    ...defaults,
    name,
    decimal_cost: 500.00,
    ...(includeCost ? { job_cost: hours * rate } : {}),
    [dynamicKey]: "private-dynamic-field",
    lookup: records["private-customer-id"],
    deleted_before_call: 1,
  };
  delete payload.deleted_before_call;
  payload.assigned_after_init = 3;
  payload.compound_assignment ??= nextStatus;
  includeCost && (payload.short_circuit_assignment = nextStatus);
  if (includeCost) payload.conditional_assignment = nextStatus;
  if (includeCost) Object.assign(payload, { conditional_object_assign: 1 });
  function neverCalled() {
    payload.phantom_nested_mutation = "phantom-private-value";
  }
  function unrelatedScope() {
    const payload = { shadow_only: "secret-tool-name" };
    payload.shadow_only = "secret-tool-name";
    return payload;
  }
  await base44.entities.ToolingItem.create(payload);
  await base44.entities.Organization.update("private-id", maybeUpdate ?? { status: nextStatus });
  let partRows = [{ old_sku: "OLD-PRIVATE" }];
  partRows = [...rows, { sku: "SKU-PRIVATE" }, { sku: "SKU-SECOND", price: 2 }];
  await base44.entities.PurchasedPart.bulkCreate(partRows);
  let original = { captured_before_reassignment: 1 };
  const capturedPayload = original;
  original.captured_after_alias = 3;
  original = { after_capture: 2 };
  await base44.entities.CapturedItem.create(capturedPayload);
  await base44.entities.ConditionalRows.bulkCreate(includeCost ? [{ conditional_a: 1 }] : [{ conditional_b: 2 }]);
  const firstRow = { duplicate_a: 1 };
  firstRow.duplicate_a = 2;
  const secondRow = { second_only: 1 };
  await base44.entities.DuplicateRows.bulkCreate([firstRow, secondRow]);
  const conditionalPayload = { conditional_base: 1 };
  conditionalPayload.late_conditional = 2;
  await base44.entities.ConditionalPayload.create(includeCost ? conditionalPayload : { conditional_other: 3 });
  await base44.entities.ModuleScopedItem.create(modulePayload);
  const unarySource = { unary_initial: 1 };
  const unaryPayload = unarySource;
  unarySource.unary_after_capture++;
  await base44.entities.UnaryAliasItem.create(unaryPayload);
  return base44.entities.ToolingItem.filter({ status: "secret-status", ...criteria }, "-created_date", 100, 0, "id,status");
}
`);
  execFileSync("git", ["init", "-q"], { cwd: repo });
  execFileSync("git", ["config", "user.email", "tracemap@example.invalid"], { cwd: repo });
  execFileSync("git", ["config", "user.name", "TraceMap Test"], { cwd: repo });
  execFileSync("git", ["add", "."], { cwd: repo });
  execFileSync("git", ["commit", "-qm", "fixture"], { cwd: repo });
  return repo;
}

async function reviewFixture(body: string) {
  const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-review-fixture-"));
  const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-review-out-"));
  try {
    await fs.writeFile(path.join(repo, "app.ts"), `import { base44 } from "@base44/sdk";
export function run(flag) { ${body} }
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    return await buildBase44Evidence(options(repo, out));
  } finally {
    await fs.rm(repo, { recursive: true, force: true });
    await fs.rm(out, { recursive: true, force: true });
  }
}

async function mutationHookFixture(body: string, replay = false) {
  const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-mutation-hook-fixture-"));
  const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-mutation-hook-out-"));
  const replayOut = replay ? await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-mutation-hook-replay-")) : null;
  try {
    await fs.writeFile(path.join(repo, "app.ts"), `import { base44 } from "@base44/sdk";
import { useMutation as useWrite } from "@tanstack/react-query";
import * as ReactQuery from "@tanstack/react-query";
${body}
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    const first = await buildBase44Evidence(options(repo, out));
    const replayPacket = replayOut ? (await buildBase44Evidence(options(repo, replayOut))).packet : undefined;
    return { ...first, replayPacket };
  } finally {
    await fs.rm(repo, { recursive: true, force: true });
    await fs.rm(out, { recursive: true, force: true });
    if (replayOut) await fs.rm(replayOut, { recursive: true, force: true });
  }
}
