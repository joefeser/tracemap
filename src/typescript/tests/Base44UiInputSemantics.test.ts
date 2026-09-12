import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { describe, expect, it } from "vitest";
import { buildBase44Evidence, diffBase44Evidence } from "../src/base44/Base44EvidencePacket";
import { FactTypes } from "../src/facts/Models";

describe("Base44 React UI input semantics", () => {
  it("emits widening-only value classes and proven payload correlation from executable JSX", async () => {
    const repo = await fixtureRepo();
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-ui-semantics-out-"));
    const { packet } = await buildBase44Evidence(options(repo, out));
    const semantics = packet.facts
      .filter((fact) => fact.factType === FactTypes.Base44UiInputSemantics)
      .map((fact) => ({ fact, value: JSON.parse(fact.properties.uiSemanticsJson) }));

    const parsedPrice = semantics.find(({ value }) => value.controlKind === "submitted-value"
      && value.submittedEntity === "MaterialItemPriceBreak" && value.submittedField === "price");
    expect(parsedPrice?.value).toMatchObject({
      valueBinding: "row.cost",
      valueClass: "decimal",
      correlationStatus: "proven",
      storageAuthority: "widening-only-never-narrowing"
    });
    expect(parsedPrice?.value.reasons.some((reason: { kind: string; detail: string }) =>
      reason.kind === "parse-cast-function" && reason.detail === "parseFloat")).toBe(true);

    const decimalInput = semantics.find(({ value }) => value.controlKind === "input"
      && value.submittedEntity === "MaterialItemPriceBreak" && value.submittedField === "price");
    expect(decimalInput?.value).toMatchObject({
      componentName: "input",
      fieldBinding: "cost",
      valueBinding: "row.cost",
      valueClass: "decimal",
      confidence: "high",
      correlationStatus: "proven"
    });
    expect(decimalInput?.value.reasons.some((reason: { detail: string }) => reason.detail.includes("step=0.01"))).toBe(true);

    const checkbox = semantics.find(({ value }) => value.controlKind === "input"
      && value.submittedEntity === "FeatureFlag" && value.submittedField === "enabled");
    expect(checkbox?.value).toMatchObject({ valueClass: "boolean", valueBinding: "form.enabled" });
    const wrappedCheckbox = semantics.find(({ value }) => value.componentName === "Checkbox");
    expect(wrappedCheckbox?.value).toMatchObject({ valueClass: "boolean", submittedEntity: "FeatureFlag", submittedField: "enabled" });

    const date = semantics.find(({ value }) => value.controlKind === "input"
      && value.submittedEntity === "Appointment" && value.submittedField === "service_date");
    expect(date?.value).toMatchObject({
      valueClass: "date-string",
      storageAuthority: "widening-only-never-narrowing",
      validationAuthority: "test-validation-only"
    });
    expect(date?.value.valueClass).not.toBe("datetime-string");
    const wrappedDate = semantics.find(({ value }) => value.componentName === "DatePicker");
    expect(wrappedDate?.value).toMatchObject({ valueClass: "date-string", submittedEntity: "Appointment", submittedField: "service_date" });

    const select = semantics.find(({ value }) => value.controlKind === "select");
    expect(select?.value).toMatchObject({
      submittedEntity: "WorkOrder",
      submittedField: "status",
      representativeValues: ["closed", "open"],
      optionAuthority: "representative-only",
      validationAuthority: "test-validation-only"
    });
    expect(select?.value).not.toHaveProperty("enumAuthority");

    const wrapped = semantics.find(({ value }) => value.componentName === "CurrencyInput");
    expect(wrapped?.value).toMatchObject({
      controlKind: "component",
      valueClass: "decimal",
      submittedEntity: "Invoice",
      submittedField: "rate"
    });
    const textDecimal = semantics.find(({ value }) => value.componentName === "Input" && value.fieldBinding === "cost_text");
    expect(textDecimal?.value).toMatchObject({
      valueClass: "decimal",
      submittedEntity: "MaterialItemPriceBreak",
      submittedField: "price"
    });

    const ambiguous = semantics.find(({ value }) => value.componentName === "Input" && value.valueBinding === "shared.value");
    expect(ambiguous?.value).toMatchObject({
      submittedEntity: "",
      submittedField: "",
      confidence: "low",
      correlationStatus: "partial",
      unresolvedCorrelationReason: "multiple-submitted-payload-targets"
    });
    expect(ambiguous?.fact.targetSymbol).toBeNull();

    expect(semantics.every(({ fact }) => fact.evidence.filePath !== "packet.jsonc")).toBe(true);
    expect(semantics.some(({ value }) => value.valueBinding === "fake.commentValue")).toBe(false);
    expect(packet.facts.some((fact) => fact.evidence.filePath === "fixtures/ignored.tsx"
      && fact.factType === FactTypes.Base44UiInputSemantics)).toBe(false);

    const forged = structuredClone(packet);
    const forgedFact = forged.facts.find((fact) => fact.factType === FactTypes.Base44UiInputSemantics)!;
    const forgedContract = JSON.parse(forgedFact.properties.uiSemanticsJson);
    forgedContract.storageAuthority = "narrowing-allowed";
    forgedFact.properties.uiSemanticsJson = JSON.stringify(forgedContract);
    const forgedPath = path.join(out, "forged-ui-authority.json");
    await fs.writeFile(forgedPath, `${JSON.stringify(forged, null, 2)}\n`);
    await expect(diffBase44Evidence(path.join(out, "base44-evidence.json"), forgedPath,
      path.join(out, "forged-ui-authority-diff.json"))).rejects.toThrow("invalid contract values");
  });
});

async function fixtureRepo(): Promise<string> {
  const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-ui-semantics-fixture-"));
  await fs.mkdir(path.join(repo, "src"), { recursive: true });
  await fs.mkdir(path.join(repo, "fixtures"), { recursive: true });
  await writeFrontendSdkAuthority(repo);
  await fs.writeFile(path.join(repo, "src", "Screen.tsx"), `import { base44 } from "@base44/sdk";
export function Screen({ row, form, shared }) {
  async function save() {
    await base44.entities.MaterialItemPriceBreak.create({ price: parseFloat(row.cost) });
    await base44.entities.FeatureFlag.create({ enabled: form.enabled });
    await base44.entities.Appointment.create({ service_date: form.serviceDate });
    await base44.entities.WorkOrder.update("1", { status: form.status });
    await base44.entities.Invoice.create({ rate: form.rate });
    await base44.entities.First.create({ amount: shared.value });
    await base44.entities.Second.create({ amount: shared.value });
  }
  return <form onSubmit={save}>
    {/* <input name="comment_only" type="number" value={fake.commentValue} /> */}
    <input name="cost" type="number" step="0.01" min="0" required value={row.cost} onChange={() => {}} />
    <Input name="cost_text" type="text" inputMode="decimal" value={row.cost} onChange={() => {}} />
    <input name="enabled" type="checkbox" value={form.enabled} onChange={() => {}} />
    <Checkbox name="enabled" checked={form.enabled} onChange={() => {}} />
    <input name="service_date" type="date" value={form.serviceDate} onChange={() => {}} />
    <DatePicker name="service_date" value={form.serviceDate} onChange={() => {}} />
    <select name="status" value={form.status} required onChange={() => {}}>
      <option value="open">Open</option>
      <option value="closed">Closed</option>
    </select>
    <CurrencyInput name="rate" type="number" inputMode="decimal" step="any" value={form.rate} onChange={() => {}} />
    <Input name="amount" type="number" value={shared.value} onChange={() => {}} />
  </form>;
}
`);
  await fs.writeFile(path.join(repo, "packet.jsonc"), `// not executable authority\n{ "ui": "<input type=\\"number\\" step=\\"0.01\\">" }\n`);
  await fs.writeFile(path.join(repo, "fixtures", "ignored.tsx"), `export const ignored = <input type="number" step="any" />;\n`);
  execFileSync("git", ["init", "-q"], { cwd: repo });
  execFileSync("git", ["add", "."], { cwd: repo });
  execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
  return repo;
}

async function writeFrontendSdkAuthority(repo: string): Promise<void> {
  const requested = "^0.8.3";
  await fs.writeFile(path.join(repo, "package.json"), `${JSON.stringify({ dependencies: { "@base44/sdk": requested } }, null, 2)}\n`);
  await fs.writeFile(path.join(repo, "package-lock.json"), `${JSON.stringify({
    name: "base44-ui-fixture",
    lockfileVersion: 3,
    requires: true,
    packages: {
      "": { dependencies: { "@base44/sdk": requested } },
      "node_modules/@base44/sdk": { version: "0.8.5" }
    }
  }, null, 2)}\n`);
}

function options(repoPath: string, outputPath: string) {
  return {
    repoPath,
    outputPath,
    projectPaths: [],
    includeGlobs: [],
    excludeGlobs: [],
    maxFileByteSize: 10_000_000,
    semantic: false,
    acceptedSourceSha256: "a".repeat(64),
    acceptedTreeSha256: "b".repeat(64),
    coverageLabel: "fixture"
  };
}
