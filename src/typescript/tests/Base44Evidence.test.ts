import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { describe, expect, it } from "vitest";
import { buildBase44Evidence, diffBase44Evidence } from "../src/base44/Base44EvidencePacket";
import { FactTypes } from "../src/facts/Models";

const shaA = "a".repeat(64);
const shaB = "b".repeat(64);

describe("Base44 source-bound static evidence", () => {
  it("extracts SDK, function, entity, env, provider and migration surfaces without raw secrets or URLs", async () => {
    const repo = await fixtureRepo();
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-out-"));
    const { packet } = await buildBase44Evidence(options(repo, out));

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
