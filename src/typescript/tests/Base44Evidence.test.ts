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
  it("expands finite source-derived entity selectors without erasing the source call identity", async () => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/dynamic-entities.ts"), `import { base44 } from "@base44/sdk";
const ENTITY_LIST = ["Customer", "Order"];
const RELATIONSHIPS = {
  Customer: [{ entity: "Buyer", text: "buyer", flag: true }, { entity: "ShippingAddress" }],
  Order: [{ entity: "OrderItem" }]
};
export async function run() {
  for (const entity of ENTITY_LIST) await base44.entities[entity].list();
  for (const relationship of RELATIONSHIPS.Customer) await base44.entities[relationship.entity].filter({ id: "1" });
  const remove = async (entityName) => base44.entities[entityName].delete("1");
  await remove("Quote");
  await remove(true ? "Print" : "Shipment");
  const scan = async (kind) => {
    const relationships = RELATIONSHIPS[kind];
    for (const relationship of relationships) await base44.entities[relationship.entity].get("1");
  };
  await scan("Customer");
  await scan("Order");
}
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-selector-domain-"));
    const { packet } = await buildBase44Evidence(options(repo, out));
    const dynamicOperations = packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.evidence.filePath === "src/dynamic-entities.ts");
    expect(dynamicOperations.map((fact) => `${fact.targetSymbol}.${fact.properties.operationName}`).sort()).toEqual([
      "Buyer.filter", "Buyer.get", "Customer.list", "Order.list", "OrderItem.get", "Print.delete", "Quote.delete",
      "Shipment.delete", "ShippingAddress.filter", "ShippingAddress.get"
    ]);
    expect(new Set(dynamicOperations.map((fact) => fact.properties.operationEvidenceId)).size).toBe(10);
    expect(dynamicOperations.every((fact) => fact.evidenceTier === "Tier3SyntaxOrTextual"
      && fact.properties.entitySelectorGap === "")).toBe(true);
    expect(dynamicOperations.every((fact) => {
      const selector = JSON.parse(fact.properties.entitySelectorJson);
      return selector.schemaVersion === "88mph.base44-entity-selector.v1"
        && selector.kind === "finite-source-domain" && selector.candidates.length > 0;
    })).toBe(true);

    const duplicateCandidate = structuredClone(packet);
    const finite = duplicateCandidate.facts.find((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.evidence.filePath === "src/dynamic-entities.ts")!;
    const duplicateSelector = JSON.parse(finite.properties.entitySelectorJson);
    duplicateSelector.candidates.push(duplicateSelector.candidates[0]);
    finite.properties.entitySelectorJson = JSON.stringify(duplicateSelector);
    const duplicatePath = path.join(out, "duplicate-selector-candidate.json");
    await fs.writeFile(duplicatePath, `${JSON.stringify(duplicateCandidate, null, 2)}\n`);
    await expect(diffBase44Evidence(path.join(out, "base44-evidence.json"), duplicatePath,
      path.join(out, "duplicate-selector-diff.json"))).rejects.toThrow("invalid entity selector candidates");

    const forgedAuthority = structuredClone(packet);
    const forged = forgedAuthority.facts.find((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.evidence.filePath === "src/dynamic-entities.ts")!;
    const forgedSelector = JSON.parse(forged.properties.entitySelectorJson);
    forgedSelector.evidence[0].sourceFileSha256 = "f".repeat(64);
    forged.properties.entitySelectorJson = JSON.stringify(forgedSelector);
    const forgedPath = path.join(out, "forged-selector-authority.json");
    await fs.writeFile(forgedPath, `${JSON.stringify(forgedAuthority, null, 2)}\n`);
    await expect(diffBase44Evidence(path.join(out, "base44-evidence.json"), forgedPath,
      path.join(out, "forged-selector-diff.json"))).rejects.toThrow("outside packet source authority");
  });

  it("retains a typed entity gap for mutated, escaped, or runtime-open selectors", async () => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/dynamic-entity-gaps.ts"), `import { base44 } from "@base44/sdk";
export async function run(runtimeEntity) {
  const mutated = ["Order"];
  mutated.push(runtimeEntity);
  for (const entity of mutated) await base44.entities[entity].list();
  const escaped = ["Customer"];
  consume(escaped);
  for (const entity of escaped) await base44.entities[entity].get("1");
  await base44.entities[runtimeEntity].filter({ id: "1" });
  const helper = async (entityName) => base44.entities[entityName].subscribe(() => undefined);
  await helper("Order");
  await helper(runtimeEntity);
  const escapedHelper = async (entityName) => base44.entities[entityName].deleteMany({ id: "1" });
  consume(escapedHelper);
  await escapedHelper("Customer");
}
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-selector-gaps-"));
    const { packet } = await buildBase44Evidence(options(repo, out));
    const operations = packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.evidence.filePath === "src/dynamic-entity-gaps.ts");
    expect(operations).toHaveLength(5);
    expect(operations.every((fact) => fact.targetSymbol === "dynamic"
      && fact.evidenceTier === "Tier4Unknown"
      && fact.properties.entitySelectorGap === "entity-selector-dynamic-unresolved")).toBe(true);
    const entityGaps = packet.coverage.gaps.filter((gap) => operations.some((operation) => operation.factId === gap.factId));
    expect(entityGaps).toHaveLength(5);
    expect(entityGaps.every((gap) => gap.category === "entity" && gap.surface.includes("entities.dynamic."))).toBe(true);
  });

  it("classifies only closed exported helpers outside the rooted module graph as dormant", async () => {
    const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-selector-dormant-"));
    await fs.mkdir(path.join(repo, "src"), { recursive: true });
    await writeFrontendSdkAuthority(repo);
    await fs.writeFile(path.join(repo, "src/main.ts"), `import { base44 } from "@base44/sdk";
import { run } from "./active";
base44.auth.me();
run("Order");
`);
    await fs.writeFile(path.join(repo, "src/active.ts"), `import { base44 } from "@base44/sdk";
export async function run(entityName) { return base44.entities[entityName].filter({ id: "1" }); }
`);
    await fs.writeFile(path.join(repo, "src/dormant.ts"), `import { base44 } from "@base44/sdk";
export async function unused(entityName) { return base44.entities[entityName].create({ name: "x" }); }
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });

    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-selector-dormant-out-"));
    const { packet } = await buildBase44Evidence(options(repo, out));
    expect(packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.evidence.filePath === "src/active.ts").map((fact) => fact.targetSymbol)).toEqual(["Order"]);
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.evidence.filePath === "src/dormant.ts")).toBe(false);
    const disposition = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityCallsiteDisposition
      && fact.evidence.filePath === "src/dormant.ts")!;
    expect(JSON.parse(disposition.properties.callsiteDispositionJson)).toEqual(expect.objectContaining({
      schemaVersion: "88mph.base44-entity-callsite-disposition.v2",
      disposition: "dormant-unreachable",
      callableName: "unused",
      externalModuleReferences: 0,
      ambiguousDynamicModuleReferences: 0,
      operationEvidenceIds: [expect.stringMatching(/^operation-[0-9a-f]{20}$/)],
      primitiveCapabilities: ["entities.dynamic.create"],
      sdkIdentity: expect.objectContaining({ version: "0.8.5" }),
      entitySelector: expect.objectContaining({ kind: "unresolved" })
    }));

    const tampered = structuredClone(packet);
    const tamperedDisposition = tampered.facts.find((fact) => fact.factId === disposition.factId)!;
    const invalid = JSON.parse(tamperedDisposition.properties.callsiteDispositionJson);
    invalid.externalModuleReferences = 1;
    tamperedDisposition.properties.callsiteDispositionJson = JSON.stringify(invalid);
    const tamperedPath = path.join(out, "tampered-dormant.json");
    await fs.writeFile(tamperedPath, `${JSON.stringify(tampered, null, 2)}\n`);
    await expect(diffBase44Evidence(path.join(out, "base44-evidence.json"), tamperedPath,
      path.join(out, "tampered-dormant-diff.json"))).rejects.toThrow("invalid entity callsite disposition");

    const orphanedPrimitive = structuredClone(packet);
    orphanedPrimitive.facts = orphanedPrimitive.facts.filter((fact) => fact.factId !== disposition.factId);
    orphanedPrimitive.coverage.gaps = orphanedPrimitive.coverage.gaps.filter((gap) => gap.factId !== disposition.factId);
    const orphanedPath = path.join(out, "orphaned-dormant-primitive.json");
    await fs.writeFile(orphanedPath, `${JSON.stringify(orphanedPrimitive, null, 2)}\n`);
    await expect(diffBase44Evidence(path.join(out, "base44-evidence.json"), orphanedPath,
      path.join(out, "orphaned-dormant-primitive-diff.json"))).rejects.toThrow("not accounted by exactly one active operation or dormant disposition");

    const retainedPrimitive = packet.facts.find((fact) => fact.factType === FactTypes.Base44SdkPrimitive
      && fact.evidence.filePath === disposition.evidence.filePath
      && fact.evidence.startLine === disposition.evidence.startLine
      && fact.properties.capability === "entities.dynamic.create")!;
    const removedDisposition = structuredClone(packet);
    removedDisposition.facts = removedDisposition.facts.filter((fact) => fact.factId !== disposition.factId
      && fact.factId !== retainedPrimitive.factId);
    removedDisposition.coverage.gaps = removedDisposition.coverage.gaps.filter((gap) => gap.factId !== disposition.factId
      && gap.factId !== retainedPrimitive.factId);
    const removedPath = path.join(out, "removed-dormant.json");
    await fs.writeFile(removedPath, `${JSON.stringify(removedDisposition, null, 2)}\n`);
    const removedDiff = await diffBase44Evidence(path.join(out, "base44-evidence.json"), removedPath,
      path.join(out, "removed-dormant-diff.json"));
    expect(removedDiff.coverageReduced).toBe(true);
    expect(removedDiff.removed).toContainEqual(expect.objectContaining({ factId: disposition.factId }));

    const activeOperation = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.evidence.filePath === "src/active.ts")!;
    const activePrimitive = packet.facts.find((fact) => fact.factType === FactTypes.Base44SdkPrimitive
      && fact.evidence.filePath === activeOperation.evidence.filePath
      && fact.properties.capability === "entities.Order.filter")!;
    const missingActivePrimitive = structuredClone(packet);
    missingActivePrimitive.facts = missingActivePrimitive.facts.filter((fact) => fact.factId !== activePrimitive.factId);
    const missingActivePrimitivePath = path.join(out, "missing-active-primitive.json");
    await fs.writeFile(missingActivePrimitivePath, `${JSON.stringify(missingActivePrimitive, null, 2)}\n`);
    await expect(diffBase44Evidence(path.join(out, "base44-evidence.json"), missingActivePrimitivePath,
      path.join(out, "missing-active-primitive-diff.json"))).rejects.toThrow("does not have exactly one retained SDK primitive");

    const duplicateOperation = structuredClone(packet);
    duplicateOperation.facts.push({ ...structuredClone(activeOperation), factId: `fact-${"d".repeat(20)}` });
    const duplicateOperationPath = path.join(out, "duplicate-operation-identity.json");
    await fs.writeFile(duplicateOperationPath, `${JSON.stringify(duplicateOperation, null, 2)}\n`);
    await expect(diffBase44Evidence(path.join(out, "base44-evidence.json"), duplicateOperationPath,
      path.join(out, "duplicate-operation-identity-diff.json"))).rejects.toThrow("duplicate operation identities");

    await fs.writeFile(path.join(repo, "src/main.ts"), `import { base44 } from "@base44/sdk";
import { run } from "./active";
base44.auth.me();
run("Order");
export const load = (runtimePath) => import(runtimePath);
`);
    const ambiguousOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-selector-dormant-ambiguous-"));
    const ambiguous = (await buildBase44Evidence(options(repo, ambiguousOut))).packet;
    expect(ambiguous.facts.some((fact) => fact.factType === FactTypes.Base44EntityCallsiteDisposition)).toBe(false);
    expect(ambiguous.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.Base44EntityOperation,
      evidenceTier: "Tier4Unknown",
      targetSymbol: "dynamic",
      evidence: expect.objectContaining({ filePath: "src/dormant.ts" })
    }));
  });

  it("never suppresses an exported React Query mutation handle or a module graph with a missing local edge", async () => {
    const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-exported-mutation-handle-"));
    await fs.mkdir(path.join(repo, "src"), { recursive: true });
    await writeFrontendSdkAuthority(repo);
    await fs.writeFile(path.join(repo, "src/main.ts"), `import { save } from "./hook"; save.mutate({ name: "active" }); import "./missing-local";\n`);
    await fs.writeFile(path.join(repo, "src/hook.ts"), `import { base44 } from "@base44/sdk";
import { useMutation } from "@tanstack/react-query";
export const save = useMutation({ mutationFn: (data) => base44.entities.ExportedHandle.create(data) });
export async function otherwiseDormant(data) { return base44.entities.MissingEdge.create(data); }
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-exported-mutation-handle-out-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    expect(packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation)
      .map((fact) => fact.targetSymbol)).toEqual(expect.arrayContaining(["ExportedHandle", "MissingEdge"]));
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityCallsiteDisposition
      && fact.evidence.filePath === "src/hook.ts")).toBe(false);
  });

  it("keeps explicit static-style imports outside the executable reachability graph", async () => {
    const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-style-import-reachability-"));
    await fs.mkdir(path.join(repo, "src"), { recursive: true });
    await writeFrontendSdkAuthority(repo);
    await fs.writeFile(path.join(repo, "src/main.ts"), `import "./app.css";\n`);
    await fs.writeFile(path.join(repo, "src/app.css"), `.app { display: block; }\n`);
    await fs.writeFile(path.join(repo, "src/unused.ts"), `import { base44 } from "@base44/sdk";
export async function unused(data) { return base44.entities.StyleGraph.create(data); }
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-style-import-reachability-out-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.targetSymbol === "StyleGraph")).toBe(false);
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityCallsiteDisposition
      && fact.evidence.filePath === "src/unused.ts")).toBe(true);
  });

  it("fails dormant reachability closed on an unresolved configured local alias", async () => {
    const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-local-alias-reachability-"));
    await fs.mkdir(path.join(repo, "src"), { recursive: true });
    await writeFrontendSdkAuthority(repo);
    await fs.writeFile(path.join(repo, "jsconfig.json"), `${JSON.stringify({
      compilerOptions: { baseUrl: ".", paths: { "~/*": ["src/*"] } }
    }, null, 2)}\n`);
    await fs.writeFile(path.join(repo, "src/main.ts"), `import "~/missing-local-module";\n`);
    await fs.writeFile(path.join(repo, "src/unused.ts"), `import { base44 } from "@base44/sdk";
export async function unused(data) { return base44.entities.AliasGraph.create(data); }
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-local-alias-reachability-out-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.targetSymbol === "AliasGraph")).toBe(true);
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityCallsiteDisposition
      && fact.evidence.filePath === "src/unused.ts")).toBe(false);
  });

  it("resolves a local alias inherited from a source-bound config", async () => {
    const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-extended-alias-reachability-"));
    await fs.mkdir(path.join(repo, "src"), { recursive: true });
    await writeFrontendSdkAuthority(repo);
    await fs.writeFile(path.join(repo, "config.base.json"), `${JSON.stringify({
      compilerOptions: { baseUrl: ".", paths: { "~/*": ["src/*"] } }
    }, null, 2)}\n`);
    await fs.writeFile(path.join(repo, "jsconfig.json"), `${JSON.stringify({ extends: "./config.base.json" }, null, 2)}\n`);
    await fs.writeFile(path.join(repo, "src/main.ts"), `import { used } from "~/used"; used({ name: "active" });\n`);
    await fs.writeFile(path.join(repo, "src/used.ts"), `import { base44 } from "@base44/sdk";
export const used = (data) => base44.entities.ExtendedAlias.create(data);
`);
    await fs.writeFile(path.join(repo, "src/dormant.ts"), `import { base44 } from "@base44/sdk";
export async function dormant(data) { return base44.entities.ExtendedAliasDormant.create(data); }
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-extended-alias-reachability-out-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.targetSymbol === "ExtendedAlias")).toBe(true);
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityCallsiteDisposition
      && fact.evidence.filePath === "src/dormant.ts")).toBe(true);
  });

  it("resolves source-rooted baseUrl and package imports aliases without opening the graph", async () => {
    for (const kind of ["baseUrl", "package-imports"] as const) {
      const repo = await fs.mkdtemp(path.join(os.tmpdir(), `tracemap-${kind}-reachability-`));
      await fs.mkdir(path.join(repo, "src"), { recursive: true });
      await writeFrontendSdkAuthority(repo);
      const specifier = kind === "baseUrl" ? "used" : "#used";
      if (kind === "baseUrl") {
        await fs.writeFile(path.join(repo, "jsconfig.json"), `${JSON.stringify({ compilerOptions: { baseUrl: "src" } }, null, 2)}\n`);
      } else {
        const packageJson = JSON.parse(await fs.readFile(path.join(repo, "package.json"), "utf8"));
        packageJson.imports = { "#*": "./src/*" };
        await fs.writeFile(path.join(repo, "package.json"), `${JSON.stringify(packageJson, null, 2)}\n`);
      }
      await fs.writeFile(path.join(repo, "src/main.ts"), `import { used } from "${specifier}"; used({ name: "active" });\n`);
      await fs.writeFile(path.join(repo, "src/used.ts"), `import { base44 } from "@base44/sdk";
export const used = (data) => base44.entities.RootedAlias.create(data);
`);
      await fs.writeFile(path.join(repo, "src/dormant.ts"), `import { base44 } from "@base44/sdk";
export async function dormant(data) { return base44.entities.RootedAliasDormant.create(data); }
`);
      execFileSync("git", ["init", "-q"], { cwd: repo });
      execFileSync("git", ["add", "."], { cwd: repo });
      execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
      const out = await fs.mkdtemp(path.join(os.tmpdir(), `tracemap-${kind}-reachability-out-`));
      const packet = (await buildBase44Evidence(options(repo, out))).packet;
      expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityOperation
        && fact.targetSymbol === "RootedAlias")).toBe(true);
      expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityCallsiteDisposition
        && fact.evidence.filePath === "src/dormant.ts")).toBe(true);
    }
  });

  it.each([
    ["malformed", `{ "compilerOptions": {`],
    ["missing-extends", `${JSON.stringify({ extends: "./missing-config.json" })}\n`],
    ["multi-target", `${JSON.stringify({ compilerOptions: { paths: { "~/*": ["src/*", "fallback/*"] } } })}\n`],
    ["invalid-wildcards", `${JSON.stringify({ compilerOptions: { paths: { "~/*/*": ["src/*/*"] } } })}\n`]
  ])("fails dormant reachability closed on %s module config authority", async (_caseName, config) => {
    const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-invalid-module-config-"));
    await fs.mkdir(path.join(repo, "src"), { recursive: true });
    await writeFrontendSdkAuthority(repo);
    await fs.writeFile(path.join(repo, "jsconfig.json"), config);
    await fs.writeFile(path.join(repo, "src/main.ts"), `export const ready = true;\n`);
    await fs.writeFile(path.join(repo, "src/unused.ts"), `import { base44 } from "@base44/sdk";
export async function unused(data) { return base44.entities.InvalidConfig.create(data); }
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-invalid-module-config-out-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.targetSymbol === "InvalidConfig")).toBe(true);
  });

  it("fails dormant reachability closed on an undeclared bare module that may be a local bundler alias", async () => {
    const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-undeclared-bare-alias-"));
    await fs.mkdir(path.join(repo, "src"), { recursive: true });
    await writeFrontendSdkAuthority(repo);
    await fs.writeFile(path.join(repo, "src/main.ts"), `import "application/missing";\n`);
    await fs.writeFile(path.join(repo, "src/unused.ts"), `import { base44 } from "@base44/sdk";
export async function unused(data) { return base44.entities.UndeclaredAlias.create(data); }
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-undeclared-bare-alias-out-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.targetSymbol === "UndeclaredAlias")).toBe(true);
  });

  it("marks an uninvoked real mutation callback dormant but blocks spoofed, invoked, or escaped handles", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen(runtimeEntity) {
  const dormant = useWrite({ mutationFn: () => base44.entities[runtimeEntity].filter({ id: "1" }) });
  void dormant.isPending;
  const staticDormant = useWrite({ mutationFn: () => base44.entities.Order.create({ status: "draft" }) });
  void staticDormant.isIdle;
  const invoked = useWrite({ mutationFn: () => base44.entities[runtimeEntity].delete("1") });
  invoked.mutate();
  const escaped = useWrite({ mutationFn: () => base44.entities[runtimeEntity].get("1") });
  consume(escaped.mutate);
}
export function Spoofed(runtimeEntity, useWrite) {
  const spoofed = useWrite({ mutationFn: () => base44.entities[runtimeEntity].list() });
  void spoofed.isPending;
}
`);
    expect(packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityCallsiteDisposition)
      .map((fact) => fact.properties.operationName).sort()).toEqual(["create", "filter"]);
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.targetSymbol === "Order" && fact.properties.operationName === "create")).toBe(false);
    expect(packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.targetSymbol === "dynamic").map((fact) => fact.properties.operationName).sort()).toEqual([
      "delete", "get", "list"
    ]);
  });

  it("follows controlled selector state and mutable SDK-client aliases only through closed source flow", async () => {
    const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-selector-state-alias-"));
    await fs.mkdir(path.join(repo, "src"), { recursive: true });
    await writeFrontendSdkAuthority(repo);
    await fs.writeFile(path.join(repo, "src/main.tsx"), `import VerifyIds from "./verify";
export const App = () => <VerifyIds />;
`);
    await fs.writeFile(path.join(repo, "src/verify.tsx"), `import { useState } from "react";
import { base44 } from "@base44/sdk";
export default function VerifyIds({ defaultEntity = "Order" }) {
  const [entity, setEntity] = useState(defaultEntity);
  const getEnt = (name) => base44.entities[name];
  let ent = getEnt(entity);
  if (!ent) return null;
  const run = () => ent.filter({ id: "1" });
  return <><select value={entity} onChange={event => setEntity(event.target.value)}>
    <option value="Order">Order</option><option value="Quote">Quote</option><option value="Customer">Customer</option>
  </select><button onClick={run}>Run</button></>;
}
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-selector-state-alias-out-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    expect(packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.evidence.filePath === "src/verify.tsx").map((fact) => fact.targetSymbol).sort()).toEqual([
      "Customer", "Order", "Quote"
    ]);

    await fs.writeFile(path.join(repo, "src/verify.tsx"), `import { useState } from "react";
import { base44 } from "@base44/sdk";
export default function VerifyIds({ ...runtimeProps }) {
  const [entity, setEntity] = useState(runtimeProps.defaultEntity);
  let ent = base44.entities[entity];
  ent = runtimeProps.replacement;
  return <button onClick={() => ent.filter({ id: "1" })}>Run</button>;
}
`);
    const unsafeOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-selector-state-alias-unsafe-"));
    const unsafe = (await buildBase44Evidence(options(repo, unsafeOut))).packet;
    expect(unsafe.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.Base44EntityOperation,
      evidenceTier: "Tier4Unknown",
      targetSymbol: "dynamic"
    }));
  });

  it("preserves correlated relationship selectors through append-only state and terminating guards", async () => {
    const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-selector-correlated-"));
    await fs.mkdir(path.join(repo, "src"), { recursive: true });
    await writeFrontendSdkAuthority(repo);
    await fs.writeFile(path.join(repo, "src/main.ts"), `import { run } from "./screen"; run();\n`);
    await fs.writeFile(path.join(repo, "src/screen.ts"), `import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { base44 } from "@base44/sdk";
  const RELATIONSHIPS = { Parent: [
  { entity: "StringChild", field: "parent_name", isStringMatch: true },
  { entity: "IdChild", field: "parent_id" },
  { entity: "IdChild", field: "alternate_parent_id" }
] };
async function check(parent) {
  const blockers = [];
  for (const rel of RELATIONSHIPS[parent]) blockers.push({ entity: rel.entity, field: rel.field, isStringMatch: rel.isStringMatch });
  return { blockers };
}
export function run() {
  const [validation, setValidation] = useState(null);
  const load = async () => { const result = await check("Parent"); setValidation(result); };
  const remove = useMutation({ mutationFn: async ({ blockers }) => {
    for (const blocker of blockers) {
      if (blocker.isStringMatch) await base44.entities[blocker.entity].list();
      else await base44.entities[blocker.entity].filter({ [blocker.field]: "id" });
      await base44.entities[blocker.entity].delete("id");
    }
  }});
  if (!validation) return load;
  remove.mutate({ blockers: validation.blockers });
  return load;
}
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-selector-correlated-out-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    const operations = packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.evidence.filePath === "src/screen.ts");
    expect(operations.map((fact) => `${fact.targetSymbol}.${fact.properties.operationName}`).sort()).toEqual([
      "IdChild.delete", "IdChild.filter", "StringChild.delete", "StringChild.list"
    ]);
    const idQuery = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityQuery
      && fact.targetSymbol === "IdChild" && fact.properties.operationName === "filter")!;
    expect(JSON.parse(idQuery.properties.fieldsJson)).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "alternate_parent_id", presence: "conditional" }),
      expect.objectContaining({ name: "parent_id", presence: "conditional" })
    ]));
    expect(JSON.parse(idQuery.properties.fieldsJson)).not.toContainEqual(expect.objectContaining({ name: "parent_name" }));
    expect(idQuery.properties.completeness).toBe("complete");
    const querySemantics = JSON.parse(idQuery.properties.querySemanticsJson);
    expect(querySemantics).toMatchObject({
      schemaVersion: "88mph.entity-query.v3",
      completeness: "complete"
    });
    expect(querySemantics.arguments[0]).toMatchObject({
      role: "filter",
      value: expect.objectContaining({
        entries: expect.arrayContaining([
          expect.objectContaining({ field: "alternate_parent_id", presence: "conditional" }),
          expect.objectContaining({ field: "parent_id", presence: "conditional" })
        ])
      })
    });
  });

  it("does not narrow a selector branch through an opaque predicate", async () => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/opaque-predicate.ts"), `import { base44 } from "@base44/sdk";
export async function run(runtimeFlag) {
  const relationships = [
    { entity: "KnownChild", isStringMatch: true },
    { entity: "RuntimeChild", isStringMatch: runtimeFlag }
  ];
  for (const relationship of relationships) {
    if (relationship.isStringMatch) await base44.entities[relationship.entity].list();
    else await base44.entities[relationship.entity].filter({ id: "1" });
  }
}
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-selector-opaque-predicate-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    const operations = packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.evidence.filePath === "src/opaque-predicate.ts");
    expect(operations.map((fact) => [fact.targetSymbol, fact.properties.operationName])).toEqual([
      ["dynamic", "list"], ["dynamic", "filter"]
    ]);
    expect(operations.every((fact) => fact.evidenceTier === "Tier4Unknown"
      && fact.properties.entitySelectorGap === "entity-selector-dynamic-unresolved")).toBe(true);
  });

  it("derives cross-component callback payload fields only through a closed repo-wide caller graph", async () => {
    const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-computed-field-component-"));
    await fs.mkdir(path.join(repo, "src"), { recursive: true });
    await writeFrontendSdkAuthority(repo);
    await fs.writeFile(path.join(repo, "src/main.tsx"), `import Screen from "./screen";
export const App = () => <Screen />;
`);
    await fs.writeFile(path.join(repo, "src/screen.tsx"), `import { useEffect, useRef } from "react";
import { debounce } from "lodash";
import Child from "./child";
import { base44 } from "@base44/sdk";
export default function Screen() {
  const save = (field) => base44.entities.Order.create({ [field]: 1 });
  const debouncedRef = useRef(debounce((...args) => saveRef.current?.(...args), 5));
  const saveRef = useRef(save);
  useEffect(() => { saveRef.current = save; }, [save]);
  const debounced = debouncedRef.current;
  const handle = (field) => debounced(field);
  return <Child onSave={handle} />;
}
`);
    await fs.writeFile(path.join(repo, "src/child.tsx"), `export default function Child({ onSave }) {
  return <><button onClick={() => onSave("actual_cost")}>Cost</button>
    <button onClick={() => onSave("actual_quantity")}>Qty</button></>;
}
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-computed-field-component-out-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "Order")!;
    expect(payload.properties).toMatchObject({ completeness: "complete", outerKind: "object", referenceAccounting: "source-bounded" });
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "actual_cost", presence: "conditional" }),
      expect.objectContaining({ name: "actual_quantity", presence: "conditional" })
    ]));
  });

  it("does not promote local payload semantics when an exported callable has cross-file callers", async () => {
    const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-cross-file-payload-semantics-"));
    await fs.mkdir(path.join(repo, "src"), { recursive: true });
    await writeFrontendSdkAuthority(repo);
    await fs.writeFile(path.join(repo, "src/save.ts"), `import { base44 } from "@base44/sdk";
export const direct = (payload) => base44.entities.Direct.create(payload);
direct({ value: "local" });
const named = (payload) => base44.entities.Named.create(payload);
named({ value: "local" });
export { named };
const defaultSave = (payload) => base44.entities.Defaulted.create(payload);
defaultSave({ value: "local" });
export default defaultSave;
const commonSave = (payload) => base44.entities.CommonJs.create(payload);
commonSave({ value: "local" });
module.exports.commonSave = commonSave;
`);
    await fs.writeFile(path.join(repo, "src/main.ts"), `import defaultSave, { direct, named } from "./save";
direct({ value: 1 }); named({ value: 2 }); defaultSave({ value: 3 });
`);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-cross-file-payload-semantics-out-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    for (const entity of ["Direct", "Named", "Defaulted", "CommonJs"]) {
      const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
        && fact.targetSymbol === entity)!;
      expect(payload.properties.completeness).not.toBe("complete");
      expect(["partial", "unresolved"]).toContain(payload.properties.completeness);
      expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
      expect(JSON.parse(payload.properties.semanticFieldsJson)).toEqual([]);
      expect(JSON.parse(payload.properties.analysisGapsJson).length).toBeGreaterThan(0);
    }
  });

  it("treats an arbitrary current assignment as a callable escape", async () => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/arbitrary-ref-escape.ts"), `import { base44 } from "@base44/sdk";
const external = globalThis.runtimeRef;
const save = (payload) => base44.entities.EscapedRef.create(payload);
save({ value: "local" });
external.current = save;
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-arbitrary-ref-escape-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "EscapedRef")!;
    expect(payload.properties).toMatchObject({ completeness: "unresolved", outerKind: "unknown", referenceAccounting: "unresolved" });
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("binding-initializer-unresolved");
  });

  it("keeps non-JSON-safe coercions unknown and preserves identical source occurrences", async () => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/json-semantics.ts"), `import { base44 } from "@base44/sdk";
base44.entities.JsonSafe.create({ boxed: Object(1), bigint: 1n * 2n, numeric: 1 * 2 });
const save = (payload) => base44.entities.Occurrences.create(payload); save({ value: "same" }); save({ value: "same" });
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-json-semantics-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    const jsonSafe = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "JsonSafe")!;
    expect(JSON.parse(jsonSafe.properties.semanticFieldsJson)).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "boxed", valueType: "unknown" }),
      expect.objectContaining({ name: "bigint", valueType: "unknown" }),
      expect.objectContaining({ name: "numeric", valueType: "number" })
    ]));
    const occurrences = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "Occurrences")!;
    const fields = JSON.parse(occurrences.properties.fieldsJson);
    const semantic = JSON.parse(occurrences.properties.semanticFieldsJson);
    expect(fields).toHaveLength(2);
    expect(new Set(fields.map((field: {evidenceStartOffset: number}) => field.evidenceStartOffset)).size).toBe(2);
    expect(semantic[0].provenance).toHaveLength(2);
  });

  it("does not trust Array map or push inference when the project realm mutates array intrinsics", async () => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/array-patch.ts"), `Array.prototype.map = function(callback) { return [{ forged: true }]; };\n`);
    await fs.writeFile(path.join(repo, "src/array-realm.ts"), `import "./array-patch";
import { base44 } from "@base44/sdk";
const names = ["safe"];
const rows = names.map((name) => ({ [name]: 1 }));
base44.entities.ArrayRealm.bulkCreate(rows);
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-array-realm-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "ArrayRealm")!;
    expect(payload.properties.completeness).not.toBe("complete");
    expect(payload.evidenceTier).toBe("Tier4Unknown");
  });

  it.each([
    ["aliased getPrototypeOf/new Array", `const getPrototype = Object.getPrototypeOf;
Reflect.set(getPrototype(new Array()), "push", function(value) { return 1; });`],
    ["constructor prototype", `Reflect.set([].constructor.prototype, "map", function(callback) { return []; });`]
  ])("does not trust Array inference through an indirectly obtained intrinsic prototype: %s", async (_caseName, patch) => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/array-reflect-patch.ts"), `${patch}\n`);
    await fs.writeFile(path.join(repo, "src/array-reflect-realm.ts"), `import "./array-reflect-patch";
import { base44 } from "@base44/sdk";
const rows = [];
rows.push({ forged: 1 });
base44.entities.ArrayReflectRealm.bulkCreate(rows);
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-array-reflect-realm-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "ArrayReflectRealm")!;
    expect(payload.properties.completeness).not.toBe("complete");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
  });

  it("does not confuse unrelated map properties with Array intrinsic mutation", async () => {
    const { packet } = await mutationHookFixture(`
const adapter = {};
adapter.map = () => "not-an-array-intrinsic";
export function Screen(enabled) {
  const save = useWrite({
    mutationFn: async (rows) => Promise.all(rows.map((row) => base44.entities.UnrelatedMapProperty.create(row)))
  });
  const rows = [];
  if (enabled) rows.push({ name: "safe" });
  save.mutate(rows);
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "UnrelatedMapProperty")!;
    expect(payload.properties.completeness).toBe("complete");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "name" })
    ]));
  });

  it.each([
    ["bounded arithmetic", `function evaluate(expression) {
  const cleaned = expression.toString().trim();
  if (!/^[\\d+\\-*/(). ]+$/.test(cleaned)) return expression;
  return new Function(\`return \${cleaned}\`)();
}` , true],
    ["unbounded source", `function evaluate(expression) {
  return new Function(expression)();
}` , false]
  ])("admits Array inference only around %s dynamic evaluation", async (_caseName, evaluator, expectedComplete) => {
    const { packet } = await mutationHookFixture(`
${evaluator}
export function Screen(enabled) {
  evaluate("1 + 1");
  const save = useWrite({
    mutationFn: async (rows) => Promise.all(rows.map((row) => base44.entities.DynamicEvaluationArray.create(row)))
  });
  const rows = [];
  if (enabled) rows.push({ name: "safe" });
  save.mutate(rows);
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "DynamicEvaluationArray")!;
    expect(payload.properties.completeness === "complete").toBe(expectedComplete);
  });

  it.each([
    ["aliased eval", `const execute = eval; execute("Array.prototype.push = () => 0");`],
    ["global member eval", `globalThis.eval("Array.prototype.push = () => 0");`],
    ["global alias member eval", `const realm = globalThis; realm.eval("Array.prototype.push = () => 0");`],
    ["conditional global alias member eval", `const realm = Math.random() ? {} : globalThis; realm.eval("Array.prototype.push = () => 0");`],
    ["comma eval", `(0, eval)("Array.prototype.push = () => 0");`],
    ["aliased Function", `const Build = Function; new Build("Array.prototype.push = () => 0")();`],
    ["function constructor", `(() => {}).constructor("Array.prototype.push = () => 0")();`],
    ["aliased function constructor", `const Build = (() => {}).constructor; new Build("Array.prototype.push = () => 0")();`],
    ["escaped global alias", `const realm = globalThis; inspect(realm);`]
  ])("blocks Array inference around indirect dynamic evaluation: %s", async (_caseName, evaluator) => {
    const { packet } = await mutationHookFixture(`
${evaluator}
export function Screen(enabled) {
  const save = useWrite({
    mutationFn: async (rows) => Promise.all(rows.map((row) => base44.entities.IndirectDynamicEvaluation.create(row)))
  });
  const rows = [];
  if (enabled) rows.push({ name: "safe" });
  save.mutate(rows);
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "IndirectDynamicEvaluation")!;
    expect(payload.properties.completeness).not.toBe("complete");
  });

  it("does not confuse an ordinary object eval member with dynamic evaluation", async () => {
    const { packet } = await mutationHookFixture(`
interface AdapterContract { eval(): string; Function: string; }
const adapter = { eval: () => "ordinary-member" };
adapter.eval();
const { eval: ordinaryEval } = adapter;
ordinaryEval();
const windowObj = typeof window === "undefined" ? { localStorage: new Map() } : window;
windowObj.localStorage;
export function Screen(enabled) {
  const save = useWrite({
    mutationFn: async (rows) => Promise.all(rows.map((row) => base44.entities.OrdinaryEvalMember.create(row)))
  });
  const rows = [];
  if (enabled) rows.push({ name: "safe" });
  save.mutate(rows);
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "OrdinaryEvalMember")!;
    expect(payload.properties.completeness).toBe("complete");
  });

  it.each([
    ["stateful coercion", `const cleaned = {
  count: 0,
  toString() { return this.count++ ? "Array.prototype.push = () => 0" : "1"; }
};`],
    ["mutable RegExp guard", `RegExp.prototype.test = () => true;
const cleaned = "Array.prototype.push = () => 0";`],
    ["aliased RegExp guard", `const RuntimeRegExp = RegExp;
RuntimeRegExp.prototype.test = () => true;
const cleaned = "Array.prototype.push = () => 0";`],
    ["global-member RegExp guard", `globalThis.RegExp.prototype.test = () => true;
const cleaned = "Array.prototype.push = () => 0";`],
    ["escaped RegExp authority", `const RuntimeRegExp = RegExp;
inspect(RuntimeRegExp);
const cleaned = "Array.prototype.push = () => 0";`]
  ])("rejects an unsound bounded arithmetic guard: %s", async (_caseName, setup) => {
    const { packet } = await mutationHookFixture(`
function evaluate() {
  ${setup}
  if (!/^[\\d+\\-*/(). ]+$/.test(cleaned)) return null;
  return new Function(\`return \${cleaned}\`)();
}
export function Screen(enabled) {
  evaluate();
  const save = useWrite({
    mutationFn: async (rows) => Promise.all(rows.map((row) => base44.entities.UnsoundArithmeticGuard.create(row)))
  });
  const rows = [];
  if (enabled) rows.push({ name: "safe" });
  save.mutate(rows);
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "UnsoundArithmeticGuard")!;
    expect(payload.properties.completeness).not.toBe("complete");
  });

  it("excludes array mutations after a statically terminating statement", async () => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/unreachable-array-mutation.ts"), `import { base44 } from "@base44/sdk";
const rows = [{ retained: 1 }];
if (globalThis.stop) { throw new Error("stop"); rows.push({ forged: 1 }); }
base44.entities.UnreachableMutation.bulkCreate(rows);
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-unreachable-array-mutation-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "UnreachableMutation")!;
    const names = JSON.parse(payload.properties.fieldsJson).map((field: {name: string}) => field.name);
    expect(names).toContain("retained");
    expect(names).not.toContain("forged");
  });

  it("excludes array mutations after a statically terminating try/finally", async () => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/unreachable-try-array-mutation.ts"), `import { base44 } from "@base44/sdk";
const rows = [{ retained: 1 }];
if (globalThis.stop) { try { throw new Error("stop"); } finally {} rows.push({ forged: 1 }); }
base44.entities.UnreachableTryMutation.bulkCreate(rows);
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-unreachable-try-array-mutation-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "UnreachableTryMutation")!;
    const names = JSON.parse(payload.properties.fieldsJson).map((field: {name: string}) => field.name);
    expect(names).toContain("retained");
    expect(names).not.toContain("forged");
  });

  it("follows a closed React ref and debounce caller chain without trusting arbitrary current assignments", async () => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/closed-react-ref.tsx"), `import { base44 } from "@base44/sdk";
import { useCallback, useEffect, useRef } from "react";
import { useMutation } from "@tanstack/react-query";
import { debounce } from "lodash";
const mutation = useMutation({ mutationFn: (data) => base44.entities.ClosedRef.create(data) });
const save = useCallback((field, value) => mutation.mutateAsync({ [field]: value }), [mutation]);
const debouncedRef = useRef(debounce((...args) => saveRef.current?.(...args), 500));
const saveRef = useRef(save);
useEffect(() => { saveRef.current = save; }, [save]);
useEffect(() => () => { debouncedRef.current.flush(); }, []);
const debounced = debouncedRef.current;
const handle = (field, value) => debounced(field, value);
handle("price", 1);
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-closed-react-ref-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "ClosedRef")!;
    expect(payload.properties.completeness).toBe("complete");
    expect(JSON.parse(payload.properties.semanticFieldsJson)).toEqual([
      expect.objectContaining({ name: "price", valueType: "unknown" })
    ]);
  });

  it("derives a finite local query-parameter domain but blocks an escaped parameter", async () => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/query-parameter.ts"), `import { base44 } from "@base44/sdk";
const findOne = async (entityName, filter) => base44.entities[entityName].filter(filter, undefined, 50);
findOne("Order", { status: "open" });
findOne("Order", { status: runtimeStatus });
findOne("Order", { customer_id: "1" });
findOne("Other", { other_only: "x" });
findOne("Other", { other_only: { $eq: "x" } });
const unsafe = (filter) => { consume(filter); return base44.entities.Quote.filter(filter); };
unsafe({ status: "draft" });
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-query-parameter-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    const order = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityQuery
      && fact.targetSymbol === "Order" && fact.evidence.filePath === "src/query-parameter.ts")!;
    expect(order.properties.completeness).toBe("complete");
    const descriptor = JSON.parse(order.properties.querySemanticsJson);
    expect(descriptor).toMatchObject({ schemaVersion: "88mph.entity-query.v3", completeness: "complete" });
    expect(descriptor.arguments[0].value.entries).toEqual(expect.arrayContaining([
      expect.objectContaining({ field: "status", presence: "conditional" }),
      expect.objectContaining({ field: "customer_id", presence: "conditional" })
    ]));
    expect(descriptor.arguments[0].value.entries.some((entry: { field: string }) => entry.field === "other_only")).toBe(false);
    const other = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityQuery
      && fact.targetSymbol === "Other" && fact.evidence.filePath === "src/query-parameter.ts")!;
    expect(JSON.parse(other.properties.querySemanticsJson).gaps).toContain("query:parameter-callsite-value-conflict");
    const quote = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityQuery
      && fact.targetSymbol === "Quote" && fact.evidence.filePath === "src/query-parameter.ts")!;
    expect(quote.properties.completeness).toBe("unresolved");
    expect(JSON.parse(quote.properties.analysisGapsJson)).toContain("binding-initializer-unresolved");
  });

  it("admits only a source-bounded outer object as runtime-deferred and blocks an escaped caller graph", async () => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/main.tsx"), `import { Screen } from "./runtime-deferred";
export const App = () => <Screen />;
`);
    await fs.writeFile(path.join(repo, "src/runtime-deferred.tsx"), `import { base44 } from "@base44/sdk";
import { useMutation as useWrite } from "@tanstack/react-query";
import { useCallback } from "react";
import Child from "./runtime-deferred-child";
export function Screen(runtimeInput) {
  const save = (data) => base44.entities.SafeOrder.update("1", data);
  const saveSpread = (data) => base44.entities.SafeSpread.create({ ...data, fixed: true });
  saveSpread(runtimeInput);
  const unsafe = (data) => base44.entities.UnsafeOrder.update("1", data);
  consume(unsafe);
  unsafe(runtimeInput);
  const hook = useWrite({ mutationFn: ({ data }) => base44.entities.HookOrder.update("1", data) });
  const invoke = useCallback(() => hook.mutate({ data: { status: "ready" } }), [hook]);
  invoke();
  return <Child onSubmit={save} onUnsafe={(data) => base44.entities.UnsafeInline.create(data)}
    onUnused={(data) => hook.mutate({ data })} />;
}
`);
    await fs.writeFile(path.join(repo, "src/runtime-deferred-child.tsx"), `export default function Child({ onSubmit, onUnsafe, onUnused }) {
  const handleSubmit = (event) => { event.preventDefault(); onSubmit({ [field]: value }); };
  consume(onUnsafe);
  return <form onSubmit={handleSubmit}><button type="submit">Save</button></form>;
}
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-runtime-deferred-outer-"));
    const packet = (await buildBase44Evidence(options(repo, out))).packet;
    const safe = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "SafeOrder")!;
    expect(safe.properties).toMatchObject({
      completeness: "partial",
      outerKind: "object",
      referenceAccounting: "source-bounded",
      runtimeObligationsJson: '["entity-open-object-fields:docker-write-readback-cleanup"]'
    });
    const safeSpread = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "SafeSpread")!;
    expect(safeSpread.properties).toMatchObject({
      completeness: "partial",
      outerKind: "object",
      referenceAccounting: "source-bounded",
      runtimeObligationsJson: '["entity-open-object-fields:docker-write-readback-cleanup"]'
    });
    const unsafe = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "UnsafeOrder")!;
    expect(unsafe.properties).toMatchObject({ completeness: "unresolved", outerKind: "unknown", referenceAccounting: "unresolved" });
    const unsafeInline = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "UnsafeInline")!;
    expect(unsafeInline.properties).toMatchObject({ completeness: "unresolved", outerKind: "unknown", referenceAccounting: "unresolved" });
    const hook = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "HookOrder")!;
    expect(hook.properties.outerKind).toBe("object");
    expect(JSON.parse(hook.properties.analysisGapsJson)).not.toContain("binding-initializer-unresolved");
  });

  it("accepts only terminating immutable Set guards as finite selector authority", async () => {
    const repo = await fixtureRepo();
    await fs.writeFile(path.join(repo, "src/guarded-entities.ts"), `import { base44 } from "@base44/sdk";
export async function run(acceptedEntity, mutatedEntity, nonTerminatingEntity) {
  const accepted = new Set(["Order", "Quote"]);
  if (!accepted.has(acceptedEntity)) return;
  await base44.entities[acceptedEntity].filter({ id: "1" });

  const mutated = new Set(["Customer"]);
  mutated.add(mutatedEntity);
  if (!mutated.has(mutatedEntity)) return;
  await base44.entities[mutatedEntity].filter({ id: "1" });

  const nonTerminating = new Set(["Print"]);
  if (!nonTerminating.has(nonTerminatingEntity)) consume(nonTerminatingEntity);
  await base44.entities[nonTerminatingEntity].filter({ id: "1" });
}
`);
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-selector-set-guard-"));
    const { packet } = await buildBase44Evidence(options(repo, out));
    const operations = packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.evidence.filePath === "src/guarded-entities.ts");
    expect(operations.filter((fact) => fact.properties.operationName === "filter" && fact.targetSymbol !== "dynamic")
      .map((fact) => fact.targetSymbol).sort()).toEqual(["Order", "Quote"]);
    expect(operations.filter((fact) => fact.targetSymbol === "dynamic")).toHaveLength(2);
  });

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
        outerKind: "object",
        operationName: "create",
        referenceAccounting: "source-bounded",
        runtimeObligationsJson: "[]",
        shapeVersion: "3"
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
    expect(JSON.parse(createPayload?.properties.semanticFieldsJson ?? "[]")).toEqual([
      expect.objectContaining({
        name: "status", semanticPresence: "always", valueType: "string", explicitNull: false,
        provenance: [expect.objectContaining({ origin: "literal", expressionType: "string-literal" })]
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
    const frontendOperation = operations.find((fact) => fact.targetSymbol === "Order" && fact.properties.operationName === "create")!;
    expect(JSON.parse(frontendOperation.properties.sdkIdentityJson)).toEqual({
      schemaVersion: "88mph.base44-sdk-callsite-identity.v1",
      packageName: "@base44/sdk",
      version: "0.8.5",
      scope: "frontend-package",
      rawSpecifier: "@base44/sdk",
      evidence: [
        expect.objectContaining({ authorityPath: "package-lock.json", kind: "package-lock-resolution" }),
        expect.objectContaining({ authorityPath: "package.json", kind: "package-manifest" }),
        expect.objectContaining({ authorityPath: "src/app.ts", kind: "source-import" })
      ]
    });
    const functionOperation = operations.find((fact) => fact.evidence.filePath === "base44/functions/sendReceipt/server.ts")!;
    expect(JSON.parse(functionOperation.properties.sdkIdentityJson)).toEqual({
      schemaVersion: "88mph.base44-sdk-callsite-identity.v1",
      packageName: "@base44/sdk",
      version: "0.8.4",
      scope: "function-runtime",
      rawSpecifier: "npm:@base44/sdk@0.8.4",
      evidence: [expect.objectContaining({
        authorityPath: "base44/functions/sendReceipt/server.ts",
        authoritySha256: functionOperation.properties.sourceFileSha256,
        kind: "source-import"
      })]
    });
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
    const indirectIdentity = JSON.parse(helperOperations.find((fact) => fact.targetSymbol === "Vendor")!.properties.sdkIdentityJson);
    expect(indirectIdentity.evidence.map((item: {authorityPath: string}) => item.authorityPath)).toEqual([
      "package-lock.json",
      "package.json",
      "src/app.ts"
    ]);
    expect(indirectIdentity.evidence.some((item: {authorityPath: string}) => item.authorityPath === "src/helper.ts")).toBe(false);
  });

  it("binds SDK identity to source roots and fails closed for ambiguous or tampered authority", async () => {
    const repo = await mixedSdkIdentityFixtureRepo();
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-sdk-identity-"));
    try {
      const {packet} = await buildBase44Evidence(options(repo, out));
      const operations = packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation);
      const frontend = operations.find((fact) => fact.targetSymbol === "FrontendItem")!;
      const functionRuntime = operations.find((fact) => fact.targetSymbol === "FunctionItem")!;
      const ambiguous = operations.find((fact) => fact.targetSymbol === "AmbiguousItem")!;
      expect(JSON.parse(frontend.properties.sdkIdentityJson)).toMatchObject({version: "0.8.5", scope: "frontend-package"});
      expect(JSON.parse(functionRuntime.properties.sdkIdentityJson)).toMatchObject({version: "0.8.4", scope: "function-runtime"});
      expect(ambiguous.properties).toMatchObject({
        sdkIdentityGap: "sdk-identity-source-root-ambiguous",
        sdkIdentityJson: ""
      });
      expect(ambiguous.evidenceTier).toBe("Tier4Unknown");

      const tampered = structuredClone(packet);
      const tamperedOperation = tampered.facts.find((fact) => fact.factId === frontend.factId)!;
      const tamperedIdentity = JSON.parse(tamperedOperation.properties.sdkIdentityJson);
      tamperedIdentity.evidence.find((item: {kind: string}) => item.kind === "source-import").authoritySha256 = "f".repeat(64);
      tamperedOperation.properties.sdkIdentityJson = JSON.stringify(tamperedIdentity);
      const tamperedPath = path.join(out, "tampered-sdk-identity.json");
      await fs.writeFile(tamperedPath, `${JSON.stringify(tampered, null, 2)}\n`);
      await expect(diffBase44Evidence(
        path.join(out, "base44-evidence.json"),
        tamperedPath,
        path.join(out, "tampered-sdk-identity-diff.json")
      )).rejects.toThrow("not bound to an extracted runtime import");

      const branchLoss = structuredClone(packet);
      const ambiguousOperation = branchLoss.facts.find((fact) => fact.factId === ambiguous.factId)!;
      ambiguousOperation.properties.sdkIdentityGap = "";
      ambiguousOperation.properties.sdkIdentityJson = frontend.properties.sdkIdentityJson;
      const branchLossPath = path.join(out, "sdk-identity-branch-loss.json");
      await fs.writeFile(branchLossPath, `${JSON.stringify(branchLoss, null, 2)}\n`);
      await expect(diffBase44Evidence(
        path.join(out, "base44-evidence.json"),
        branchLossPath,
        path.join(out, "sdk-identity-branch-loss-diff.json")
      )).rejects.toThrow("with an exact SDK identity must retain Tier3SyntaxOrTextual");

      await fs.rm(path.join(repo, "package-lock.json"));
      const missingOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-sdk-identity-missing-"));
      try {
        const missing = await buildBase44Evidence(options(repo, missingOut));
        expect(missing.packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityOperation
          && fact.targetSymbol === "FrontendItem")).toEqual(expect.objectContaining({
          evidenceTier: "Tier4Unknown",
          properties: expect.objectContaining({
            sdkIdentityGap: "sdk-identity-package-authority-missing",
            sdkIdentityJson: ""
          })
        }));
      } finally {
        await fs.rm(missingOut, {recursive: true, force: true});
      }
    } finally {
      await fs.rm(repo, {recursive: true, force: true});
      await fs.rm(out, {recursive: true, force: true});
    }
  });

  it("emits deterministic payload presence, type, binding, spread and ambiguity evidence without values", async () => {
    const repo = await payloadFixtureRepo();
    const firstOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-payload-first-"));
    const secondOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-payload-second-"));
    const first = await buildBase44Evidence(options(repo, firstOut));
    const second = await buildBase44Evidence(options(repo, secondOut));
    expect(await fs.readFile(path.join(firstOut, "base44-evidence.json"), "utf8")).toBe(
      await fs.readFile(path.join(secondOut, "base44-evidence.json"), "utf8")
    );
    expect(Object.keys(first.packet.artifacts).sort()).toEqual([
      "facts.ndjson", "logs/analyzer.log", "report.md"
    ]);
    expect(first.packet.artifacts).toEqual(second.packet.artifacts);
    await expect(fs.stat(path.join(firstOut, "scan-manifest.json"))).resolves.toBeTruthy();
    await expect(fs.stat(path.join(firstOut, "index.sqlite"))).resolves.toBeTruthy();
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
  const stableSemantic = useWrite({ mutationFn: (payload) => base44.entities.StableSemantic.create(payload) });
  stableSemantic.mutate({ state: "one" });
  stableSemantic.mutate({ state: "two" });
  const conflictingSemantic = useWrite({ mutationFn: (payload) => base44.entities.ConflictingSemantic.create(payload) });
  conflictingSemantic.mutate({ state: "one" });
  conflictingSemantic.mutate({ state: 2 });
}
`, true);

    const direct = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "DirectItem")!;
    expect(direct.properties.completeness).toBe("complete");
    expect(direct.properties.constructionKind).toBe("react-query-mutation-callsites");
    expect(direct.properties.outerKind).toBe("object");
    expect(JSON.parse(direct.properties.fieldsJson)).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "always_present", presence: "unconditional", expressionType: "integer-number-literal" }),
      expect.objectContaining({ name: "sometimes_present", presence: "conditional", expressionType: "identifier-reference" })
    ]));
    expect(JSON.parse(direct.properties.semanticFieldsJson)).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "always_present", semanticPresence: "always", valueType: "integer", explicitNull: false }),
      expect.objectContaining({ name: "sometimes_present", semanticPresence: "conditional", valueType: "unknown", explicitNull: false })
    ]));
    expect(JSON.parse(direct.properties.candidateBindingsJson)).toEqual(expect.arrayContaining([
      "binding:payload", "mutation-hook:createItem"
    ]));

    const nested = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === "NestedItem")!;
    expect(nested.properties.completeness).toBe("complete");
    expect(nested.properties.outerKind).toBe("object");
    expect(JSON.parse(nested.properties.semanticFieldsJson)).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "status", semanticPresence: "always", valueType: "string", explicitNull: false }),
      expect.objectContaining({ name: "total", semanticPresence: "always", valueType: "number", explicitNull: false })
    ]));
    const stableSemantic = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "StableSemantic")!;
    expect(JSON.parse(stableSemantic.properties.semanticFieldsJson)).toEqual([
      expect.objectContaining({ name: "state", semanticPresence: "always", valueType: "string", explicitNull: false })
    ]);
    const conflictingSemantic = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "ConflictingSemantic")!;
    expect(JSON.parse(conflictingSemantic.properties.semanticFieldsJson)).toEqual([
      expect.objectContaining({ name: "state", semanticPresence: "always", valueType: "unknown", explicitNull: false })
    ]);
    expect(replayPacket?.facts.filter((fact) => fact.factType === FactTypes.Base44EntityPayload).map((fact) => fact.factId)).toEqual(
      packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityPayload).map((fact) => fact.factId)
    );
    expect(JSON.stringify(packet)).not.toContain("redacted");

    const tampered = structuredClone(packet);
    tampered.facts.find((fact) => fact.factId === direct.factId)!.properties.outerKind = "unknown";
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-complete-outer-kind-tamper-"));
    const baselinePath = path.join(out, "baseline.json");
    const tamperedPath = path.join(out, "tampered.json");
    await fs.writeFile(baselinePath, `${JSON.stringify(packet)}\n`);
    await fs.writeFile(tamperedPath, `${JSON.stringify(tampered)}\n`);
    await expect(diffBase44Evidence(baselinePath, tamperedPath, path.join(out, "diff.json")))
      .rejects.toThrow("cannot be complete with an unknown outer kind");
  });

  it("downgrades conflicting mutation callsite outer kinds instead of claiming completeness", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen() {
  const save = useWrite({ mutationFn: (payload) => base44.entities.ConflictingOuterItem.create(payload) });
  save.mutate({ known: 1 });
  save.mutate([]);
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "ConflictingOuterItem")!;
    expect(payload).toMatchObject({
      evidenceTier: "Tier4Unknown",
      properties: expect.objectContaining({
        completeness: "partial",
        outerKind: "unknown",
        referenceAccounting: "unresolved"
      })
    });
    expect(JSON.parse(payload.properties.analysisGapsJson)).toEqual(["payload-outer-kind-unresolved"]);
  });

  it("projects a source-proven object-rest payload without the removed fields", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen() {
  const save = useWrite({ mutationFn: (variables) => {
    const { metadata, ...payload } = variables;
    return base44.entities.RestProjectedItem.create(payload);
  } });
  save.mutate({ metadata: "redacted", name: "redacted", quantity: 2 });
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "RestProjectedItem")!;
    expect(payload.properties.completeness).toBe("complete");
    expect(payload.properties.constructionKind).toBe("destructured:object-rest:react-query-mutation-callsites");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([
      expect.objectContaining({ name: "name" }),
      expect.objectContaining({ name: "quantity" })
    ]);
    expect(payload.properties.fieldsJson).not.toContain("metadata");
    expect(JSON.parse(payload.properties.analysisGapsJson)).toEqual([]);
  });

  it("classifies a finite open-object spread as runtime-deferred without claiming its fields", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen(runtimeInput) {
  const save = useWrite({ mutationFn: (variables) =>
    base44.entities.DeferredItem.create({ ...variables, organization_id: "redacted" })
  });
  save.mutate(runtimeInput);
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "DeferredItem")!;
    expect(payload).toMatchObject({
      evidenceTier: "Tier4Unknown",
      properties: {
        ...payload.properties,
        completeness: "partial",
        outerKind: "object",
        referenceAccounting: "source-bounded",
        runtimeObligationsJson: '["entity-open-object-fields:docker-write-readback-cleanup"]',
        shapeVersion: "3"
      }
    });
    expect(JSON.parse(payload.properties.analysisGapsJson)).toEqual(["runtime-deferred-object-fields"]);
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([
      expect.objectContaining({ name: "organization_id", presence: "unconditional" })
    ]);
    expect(JSON.parse(payload.properties.semanticFieldsJson)).toEqual([
      expect.objectContaining({ name: "organization_id", semanticPresence: "always", valueType: "string", explicitNull: false })
    ]);

    const tampered = structuredClone(packet);
    const tamperedPayload = tampered.facts.find((fact) => fact.factId === payload.factId)!;
    tamperedPayload.properties.outerKind = "unknown";
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-deferred-tamper-"));
    const baselinePath = path.join(out, "baseline.json");
    const tamperedPath = path.join(out, "tampered.json");
    await fs.writeFile(baselinePath, `${JSON.stringify(packet)}\n`);
    await fs.writeFile(tamperedPath, `${JSON.stringify(tampered)}\n`);
    await expect(diffBase44Evidence(baselinePath, tamperedPath, path.join(out, "diff.json")))
      .rejects.toThrow("invalid deferred-object contract");

    const missingObligation = structuredClone(packet);
    const missingObligationPayload = missingObligation.facts.find((fact) => fact.factId === payload.factId)!;
    missingObligationPayload.properties.runtimeObligationsJson = "[]";
    const missingObligationPath = path.join(out, "missing-obligation.json");
    await fs.writeFile(missingObligationPath, `${JSON.stringify(missingObligation)}\n`);
    await expect(diffBase44Evidence(baselinePath, missingObligationPath, path.join(out, "missing-obligation-diff.json")))
      .rejects.toThrow("invalid deferred-object contract");

    const orphanedObligation = structuredClone(packet);
    const orphanedObligationPayload = orphanedObligation.facts.find((fact) => fact.factId === payload.factId)!;
    orphanedObligationPayload.properties.analysisGapsJson = "[]";
    const orphanedObligationPath = path.join(out, "orphaned-obligation.json");
    await fs.writeFile(orphanedObligationPath, `${JSON.stringify(orphanedObligation)}\n`);
    await expect(diffBase44Evidence(baselinePath, orphanedObligationPath, path.join(out, "orphaned-obligation-diff.json")))
      .rejects.toThrow("orphaned runtime obligation");

    const malformedSemantic = structuredClone(packet);
    const malformedPayload = malformedSemantic.facts.find((fact) => fact.factId === payload.factId)!;
    const malformedFields = JSON.parse(malformedPayload.properties.semanticFieldsJson);
    malformedFields[0].valueType = "guessed";
    malformedPayload.properties.semanticFieldsJson = JSON.stringify(malformedFields);
    const malformedSemanticPath = path.join(out, "malformed-semantic.json");
    await fs.writeFile(malformedSemanticPath, `${JSON.stringify(malformedSemantic)}\n`);
    await expect(diffBase44Evidence(baselinePath, malformedSemanticPath, path.join(out, "malformed-semantic-diff.json")))
      .rejects.toThrow("invalid semantic field");

    const duplicateOccurrence = structuredClone(packet);
    const duplicateOccurrencePayload = duplicateOccurrence.facts.find((fact) => fact.factId === payload.factId)!;
    const duplicatedFields = JSON.parse(duplicateOccurrencePayload.properties.fieldsJson);
    duplicatedFields.push({ ...duplicatedFields[0], origin: `${duplicatedFields[0].origin}:tampered` });
    duplicateOccurrencePayload.properties.fieldsJson = JSON.stringify(duplicatedFields);
    const duplicateOccurrencePath = path.join(out, "duplicate-semantic-occurrence.json");
    await fs.writeFile(duplicateOccurrencePath, `${JSON.stringify(duplicateOccurrence)}\n`);
    await expect(diffBase44Evidence(baselinePath, duplicateOccurrencePath, path.join(out, "duplicate-semantic-occurrence-diff.json")))
      .rejects.toThrow("duplicate field occurrences");

    const contradictorySemantic = structuredClone(packet);
    const contradictoryPayload = contradictorySemantic.facts.find((fact) => fact.factId === payload.factId)!;
    const contradictoryFields = JSON.parse(contradictoryPayload.properties.semanticFieldsJson);
    contradictoryFields[0].valueType = contradictoryFields[0].valueType === "string" ? "integer" : "string";
    contradictoryPayload.properties.semanticFieldsJson = JSON.stringify(contradictoryFields);
    const contradictorySemanticPath = path.join(out, "contradictory-semantic.json");
    await fs.writeFile(contradictorySemanticPath, `${JSON.stringify(contradictorySemantic)}\n`);
    await expect(diffBase44Evidence(baselinePath, contradictorySemanticPath, path.join(out, "contradictory-semantic-diff.json")))
      .rejects.toThrow("contradictory semantic value aggregate");

    const duplicateSemantic = structuredClone(packet);
    const duplicatePayload = duplicateSemantic.facts.find((fact) => fact.factId === payload.factId)!;
    const duplicateFields = JSON.parse(duplicatePayload.properties.semanticFieldsJson);
    duplicateFields.push(duplicateFields[0]);
    duplicatePayload.properties.semanticFieldsJson = JSON.stringify(duplicateFields);
    const duplicateSemanticPath = path.join(out, "duplicate-semantic.json");
    await fs.writeFile(duplicateSemanticPath, `${JSON.stringify(duplicateSemantic)}\n`);
    await expect(diffBase44Evidence(baselinePath, duplicateSemanticPath, path.join(out, "duplicate-semantic-diff.json")))
      .rejects.toThrow("invalid semantic field");
  });

  it("follows a finite mutation-array push and map-element path", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen(enabled) {
  const save = useWrite({
    mutationFn: async (rows) => Promise.all(rows.map((row) => base44.entities.ArrayMappedItem.create(row)))
  });
  const rows = [];
  if (enabled) rows.push({ organization_id: "redacted", quantity: 2 });
  save.mutate(rows);
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "ArrayMappedItem")!;
    expect(payload).toMatchObject({
      evidenceTier: "Tier3SyntaxOrTextual",
      properties: expect.objectContaining({
        completeness: "complete",
        outerKind: "object",
        referenceAccounting: "source-bounded"
      })
    });
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "organization_id", presence: "conditional" }),
      expect.objectContaining({ name: "quantity", presence: "conditional" })
    ]));
    expect(JSON.parse(payload.properties.analysisGapsJson)).toEqual([]);
  });

  it.each([
    "rows.push(runtimeInput);",
    "rows.push(...runtimeInput);",
    "inspect(rows);",
    "rows.map((row) => { row.extra = 2; });",
    "rows.push({ known: 1 }); rows.push([]);"
  ])("keeps an open or conflicting mutation-array source fail closed: %s", async (mutation) => {
    const { packet } = await mutationHookFixture(`
export function Screen(runtimeInput) {
  const save = useWrite({
    mutationFn: async (rows) => Promise.all(rows.map((row) => base44.entities.ArrayMappedRejectedItem.create(row)))
  });
  const rows = [];
  ${mutation}
  save.mutate(rows);
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "ArrayMappedRejectedItem")!;
    expect(payload.evidenceTier).toBe("Tier4Unknown");
    expect(payload.properties.outerKind).toBe("unknown");
    expect(payload.properties.referenceAccounting).toBe("unresolved");
    expect(JSON.parse(payload.properties.analysisGapsJson)).toEqual(expect.arrayContaining([
      expect.stringMatching(/^(?:array-iterator-source-unresolved|payload-outer-kind-unresolved)$/)
    ]));
  });

  it("rejects a map element parameter that escapes beside the SDK call", async () => {
    const { packet } = await mutationHookFixture(`
export function Screen() {
  const save = useWrite({
    mutationFn: async (rows) => Promise.all(rows.map((row) => {
      inspect(row);
      return base44.entities.ArrayMappedEscapedItem.create(row);
    }))
  });
  const rows = [{ known: 1 }];
  save.mutate(rows);
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "ArrayMappedEscapedItem")!;
    expect(payload.evidenceTier).toBe("Tier4Unknown");
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("array-iterator-parameter-state-unresolved");
  });

  it.each([
    "const escaped = save.mutate;",
    "save.mutate(runtimeInput); inspect(save);"
  ])("does not mark an escaped hook graph source-bounded: %s", async (escape) => {
    const { packet } = await mutationHookFixture(`
export function Screen(runtimeInput) {
  const save = useWrite({ mutationFn: (variables) =>
    base44.entities.DeferredRejectedItem.create({ ...variables, organization_id: "redacted" })
  });
  ${escape}
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "DeferredRejectedItem")!;
    expect(payload.properties.referenceAccounting).toBe("unresolved");
    expect(JSON.parse(payload.properties.analysisGapsJson)).not.toContain("runtime-deferred-object-fields");
    expect(JSON.parse(payload.properties.runtimeObligationsJson)).toEqual([]);
  });

  it.each([
    "const { [key]: removed, ...payload } = variables;",
    "const { metadata: removed = fallback, ...payload } = variables;",
    "const { metadata, payload } = variables;"
  ])("keeps unsupported object-rest exclusions fail closed: %s", async (binding) => {
    const { packet } = await mutationHookFixture(`
export function Screen(key, fallback) {
  const save = useWrite({ mutationFn: (variables) => {
    ${binding}
    return base44.entities.RestRejectedItem.create(payload);
  } });
  save.mutate({ metadata: { nested: "redacted" }, name: "must-not-be-claimed" });
}
`);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "RestRejectedItem")!;
    expect(payload.properties.completeness).toBe("unresolved");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual([]);
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("destructured-binding-unresolved");
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

  it("treats only source-proven React dependency-array references as terminating hook uses", async () => {
    const {packet} = await mutationHookFixture(`
import { useCallback as useStable } from "react";
export function Screen() {
  const accepted = useWrite({ mutationFn: (payload) => base44.entities.AcceptedDependencyItem.create(payload) });
  accepted.mutate({ exact_field: 1 });
  useStable(() => accepted.mutate({ exact_field: 2 }), [accepted]);

  const rejected = useWrite({ mutationFn: (payload) => base44.entities.RejectedDependencyItem.create(payload) });
  rejected.mutate({ exact_field: 1 });
  const useCallback = (_callback, dependencies) => dependencies;
  useCallback(() => undefined, [rejected]);
}
`);
    const accepted = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "AcceptedDependencyItem")!;
    expect(accepted.properties.completeness).toBe("complete");
    expect(JSON.parse(accepted.properties.analysisGapsJson)).toEqual([]);
    const rejected = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "RejectedDependencyItem")!;
    expect(rejected.properties.completeness).toBe("partial");
    expect(JSON.parse(rejected.properties.analysisGapsJson)).toContain("mutation-hook-binding-escape-unresolved");
  });

  it("resolves computed payload keys only through finite source-proven local caller domains", async () => {
    const {packet} = await mutationHookFixture(`
import { useCallback } from "react";
import { debounce } from "lodash";
export function Screen(runtimeField) {
  const accepted = useWrite({ mutationFn: (payload) => base44.entities.FiniteComputedItem.update("id", payload) });
  const update = useCallback(debounce((field, value) => accepted.mutate({ [field]: value }), 10), [accepted]);
  update("name", 1);
  update(true ? "phone" : "email", 2);

  const unknown = useWrite({ mutationFn: (payload) => base44.entities.UnknownComputedItem.update("id", payload) });
  const updateUnknown = useCallback(debounce((field, value) => unknown.mutate({ [field]: value }), 10), [unknown]);
  updateUnknown(runtimeField, 1);

  const escaped = useWrite({ mutationFn: (payload) => base44.entities.EscapedComputedItem.update("id", payload) });
  const updateEscaped = useCallback(debounce((field, value) => escaped.mutate({ [field]: value }), 10), [escaped]);
  consume(updateEscaped);
  updateEscaped("known_but_not_closed", 1);
}
`);
    const accepted = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "FiniteComputedItem")!;
    expect(JSON.parse(accepted.properties.analysisGapsJson)).toEqual([]);
    expect(accepted.properties.completeness).toBe("complete");
    expect(JSON.parse(accepted.properties.fieldsJson).map((field: {name: string}) => field.name).sort()).toEqual([
      "email", "name", "phone"
    ]);
    expect(JSON.parse(accepted.properties.fieldsJson).every((field: {presence: string}) => field.presence === "conditional")).toBe(true);
    for (const entity of ["UnknownComputedItem", "EscapedComputedItem"]) {
      const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === entity)!;
      expect(payload.properties.completeness).toBe("partial");
      expect(JSON.parse(payload.properties.analysisGapsJson)).toContain("dynamic-computed-property");
      expect(JSON.parse(payload.properties.fieldsJson)).toContainEqual(expect.objectContaining({name: "<dynamic>"}));
    }
  });

  it("models closed React object state and mutually exclusive mutation branches without erasing unsafe state flow", async () => {
    const {packet} = await mutationHookFixture(`
import { useState } from "react";
export function Screen(flag, value) {
  const [form, setForm] = useState({ name: "", email: "" });
  setForm({ ...form, name: value });
  setForm(previous => ({ ...previous, email: value }));
  const create = useWrite({ mutationFn: (payload) => base44.entities.StateCreateItem.create(payload) });
  const update = useWrite({ mutationFn: ({id, data}) => base44.entities.StateUpdateItem.update(id, data) });
  if (flag) update.mutate({id: "id", data: form});
  else create.mutate({...form, organization_id: "org"});
}

export function Sequential(value) {
  const [form, setForm] = useState({ name: "" });
  setForm(previous => ({ ...previous, name: value }));
  const first = useWrite({ mutationFn: ({id, data}) => base44.entities.SequentialFirstItem.update(id, data) });
  const second = useWrite({ mutationFn: (payload) => base44.entities.SequentialSecondItem.create(payload) });
  first.mutate({id: "id", data: form});
  second.mutate({...form, organization_id: "org"});
}

export function EscapedState(value) {
  const [form, setForm] = useState({ name: "" });
  consume(setForm);
  const save = useWrite({ mutationFn: (payload) => base44.entities.EscapedStateItem.create(payload) });
  save.mutate(form);
}

export function SpoofedState(useState) {
  const [form] = useState({ invented: "" });
  const save = useWrite({ mutationFn: (payload) => base44.entities.SpoofedStateItem.create(payload) });
  save.mutate(form);
}
`);
    for (const entity of ["StateCreateItem", "StateUpdateItem"]) {
      const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload && fact.targetSymbol === entity)!;
      expect(JSON.parse(payload.properties.analysisGapsJson)).toEqual([]);
      expect(payload.properties.completeness).toBe("complete");
      expect(payload.properties.outerKind).toBe("object");
    }
    const sequential = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "SequentialSecondItem")!;
    expect(sequential.properties.completeness).toBe("partial");
    expect(JSON.parse(sequential.properties.analysisGapsJson)).toContain("spread:binding-alias-escape-unresolved");
    const escaped = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "EscapedStateItem")!;
    expect(JSON.parse(escaped.properties.analysisGapsJson)).toContain("react-state-setter-flow-unresolved");
    const spoofed = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload
      && fact.targetSymbol === "SpoofedStateItem")!;
    expect(spoofed.properties.completeness).toBe("unresolved");
    expect(JSON.parse(spoofed.properties.analysisGapsJson)).toContain("destructured-binding-unresolved");
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
    ["const original = [{ initial: 1 }]; const payload = original; original.push({ added: 2 }); base44.entities.ReviewItem.bulkCreate(payload);", "post-capture-alias-mutation-unresolved"]
  ])("does not claim a complete shape after unmodeled reference use: %s", async (body, gap) => {
    const { packet } = await reviewFixture(body);
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload)!;
    expect(payload.properties.completeness).toBe("partial");
    expect(payload.evidenceTier).toBe("Tier4Unknown");
    expect(JSON.parse(payload.properties.analysisGapsJson)).toContain(gap);
  });

  it("models a finite array push without erasing the array payload kind", async () => {
    const { packet } = await reviewFixture("const payload = [{ initial: 1 }]; payload.push({ added: 2 }); base44.entities.ReviewItem.bulkCreate(payload);");
    const payload = packet.facts.find((fact) => fact.factType === FactTypes.Base44EntityPayload)!;
    expect(payload.properties.completeness).toBe("complete");
    expect(payload.properties.outerKind).toBe("array");
    expect(JSON.parse(payload.properties.fieldsJson)).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "initial", presence: "conditional" }),
      expect.objectContaining({ name: "added", presence: "conditional" })
    ]));
    expect(JSON.parse(payload.properties.analysisGapsJson)).toEqual([]);
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

  it("publishes mixed producer-owned coverage gaps once with stable source-bound identities", async () => {
    const repo = await coverageGapFixtureRepo();
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-coverage-gaps-"));
    const replayOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-coverage-gaps-replay-"));
    const first = await buildBase44Evidence(options(repo, out));
    const replay = await buildBase44Evidence(options(repo, replayOut));

    expect(first.packet.coverage.gapSchemaVersion).toBe("tracemap.base44.coverage-gap.v1");
    expect(first.packet.coverage.gaps).toEqual(replay.packet.coverage.gaps);
    const tier4Facts = first.packet.facts.filter((fact) => fact.evidenceTier === "Tier4Unknown");
    expect(tier4Facts).toHaveLength(4);
    expect(first.packet.coverage.gaps).toHaveLength(tier4Facts.length);
    expect(new Set(first.packet.coverage.gaps.map((gap) => gap.gapId)).size).toBe(tier4Facts.length);
    expect(new Set(first.packet.coverage.gaps.map((gap) => gap.factId)).size).toBe(tier4Facts.length);
    expect(first.packet.coverage.gaps.map((gap) => gap.factId).sort()).toEqual(
      tier4Facts.map((fact) => fact.factId).sort()
    );
    expect(first.packet.coverage.gaps.map((gap) => gap.category).sort()).toEqual([
      "entity",
      "function",
      "http-integration",
      "unknown"
    ]);
    expect(first.packet.coverage.gaps).toEqual(expect.arrayContaining([
      expect.objectContaining({ category: "entity", surface: "entities.ImportRecord.importEntities" }),
      expect.objectContaining({ category: "function", surface: "functions.dynamic" }),
      expect.objectContaining({ category: "http-integration", surface: "http-integration.Base44HttpTarget.dynamic" }),
      expect.objectContaining({ category: "unknown", surface: "unknown.Base44EnvironmentAccess.dynamic" })
    ]));
    expect(first.packet.coverage.gaps.every((gap) => gap.factId !== null && gap.evidenceTier === "Tier4Unknown")).toBe(true);

    const duplicate = structuredClone(first.packet);
    duplicate.coverage.gaps.push(structuredClone(duplicate.coverage.gaps[0]));
    const duplicatePath = path.join(out, "duplicate-gap.json");
    await fs.writeFile(duplicatePath, `${JSON.stringify(duplicate, null, 2)}\n`);
    await expect(diffBase44Evidence(
      path.join(out, "base44-evidence.json"),
      duplicatePath,
      path.join(out, "duplicate-diff.json")
    )).rejects.toThrow("duplicate gapId");

    const malformed = structuredClone(first.packet);
    malformed.coverage.gaps[0].gapId = "gap-not-a-sha";
    const malformedPath = path.join(out, "malformed-gap.json");
    await fs.writeFile(malformedPath, `${JSON.stringify(malformed, null, 2)}\n`);
    await expect(diffBase44Evidence(
      path.join(out, "base44-evidence.json"),
      malformedPath,
      path.join(out, "malformed-diff.json")
    )).rejects.toThrow("invalid gapId");

    const reclassifiedUnknown = structuredClone(first.packet);
    const unknownGap = reclassifiedUnknown.coverage.gaps.find((gap) => gap.category === "unknown");
    if (!unknownGap) throw new Error("fixture did not emit the expected unknown coverage gap");
    unknownGap.category = "http-integration";
    const reclassifiedPath = path.join(out, "reclassified-gap.json");
    await fs.writeFile(reclassifiedPath, `${JSON.stringify(reclassifiedUnknown, null, 2)}\n`);
    await expect(diffBase44Evidence(
      path.join(out, "base44-evidence.json"),
      reclassifiedPath,
      path.join(out, "reclassified-diff.json")
    )).rejects.toThrow("do not match the source-bound Tier4 fact set exactly once");
  });

  it("does not classify conventional migrations as Base44 without a Base44 signal", async () => {
    const repo = await nonBase44MigrationRepo();
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-non-base44-out-"));
    const { packet } = await buildBase44Evidence(options(repo, out));

    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44MigrationSurface)).toBe(false);
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44HttpTarget)).toBe(false);
    expect(packet.facts.some((fact) => fact.factType === FactTypes.Base44EnvironmentAccess)).toBe(false);
    expect(packet.coverage.gapSchemaVersion).toBe("tracemap.base44.coverage-gap.v1");
    expect(packet.coverage.gaps).toEqual([]);
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
  await writeFrontendSdkAuthority(repo);
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
  await fs.writeFile(path.join(repo, "base44/functions/sendReceipt/server.ts"), `import axios from "axios";\nimport { createClientFromRequest } from "npm:@base44/sdk@0.8.4";\nconst serviceClient = createClientFromRequest(new Request("https://example.invalid"));\nexport const load = async () => { await axios.post("https://mail.example.invalid/send", {}); return serviceClient.entities.Order.list(); };\n`);
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

async function mixedSdkIdentityFixtureRepo(): Promise<string> {
  const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-mixed-sdk-identity-fixture-"));
  await fs.mkdir(path.join(repo, "src"), {recursive: true});
  await fs.mkdir(path.join(repo, "functions/task"), {recursive: true});
  await writeFrontendSdkAuthority(repo);
  await fs.writeFile(path.join(repo, "src/helper.ts"), `
export function writeAmbiguous(client, payload) {
  return client.entities.AmbiguousItem.create(payload);
}
`);
  await fs.writeFile(path.join(repo, "src/app.ts"), `import {base44} from "@base44/sdk";
import {writeAmbiguous} from "./helper";
export function run(payload) {
  base44.entities.FrontendItem.create(payload);
  return writeAmbiguous(base44, payload);
}
`);
  await fs.writeFile(path.join(repo, "functions/task/index.ts"), `import {createClientFromRequest} from "npm:@base44/sdk@0.8.4";
import {writeAmbiguous} from "../../src/helper";
const base44 = createClientFromRequest(new Request("https://example.invalid"));
export function run(payload) {
  base44.entities.FunctionItem.create(payload);
  return writeAmbiguous(base44, payload);
}
`);
  execFileSync("git", ["init", "-q"], {cwd: repo});
  execFileSync("git", ["add", "."], {cwd: repo});
  execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], {cwd: repo});
  return repo;
}

async function coverageGapFixtureRepo(): Promise<string> {
  const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-coverage-gap-fixture-"));
  await fs.mkdir(path.join(repo, "src"), { recursive: true });
  await writeFrontendSdkAuthority(repo);
  await fs.writeFile(path.join(repo, "src/app.ts"), `import { base44 } from "@base44/sdk";
export async function run(functionName, file, environmentName, url) {
  await base44.functions.invoke(functionName, {});
  await base44.entities.ImportRecord.importEntities(file);
  Deno.env.get(environmentName);
  return fetch(url);
}
`);
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
  await writeFrontendSdkAuthority(repo);
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
  await writeFrontendSdkAuthority(repo);
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
    await writeFrontendSdkAuthority(repo);
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
    await writeFrontendSdkAuthority(repo);
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

async function writeFrontendSdkAuthority(repo: string): Promise<void> {
  const requested = "^0.8.3";
  await fs.writeFile(path.join(repo, "package.json"), `${JSON.stringify({ dependencies: { "@base44/sdk": requested } }, null, 2)}\n`);
  await fs.writeFile(path.join(repo, "package-lock.json"), `${JSON.stringify({
    name: "base44-fixture",
    lockfileVersion: 3,
    requires: true,
    packages: {
      "": { dependencies: { "@base44/sdk": requested } },
      "node_modules/@base44/sdk": { version: "0.8.5" }
    }
  }, null, 2)}\n`);
}
