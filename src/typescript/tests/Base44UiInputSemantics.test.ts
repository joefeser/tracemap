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

    const time = semantics.find(({ value }) => value.controlKind === "input" && value.fieldBinding === "start_time");
    expect(time?.value).toMatchObject({
      valueClass: "string",
      validationAuthority: "test-validation-only"
    });

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
    expect(packet.facts.some((fact) => fact.evidence.filePath === "src/generated/Widget.generated.tsx"
      && fact.factType === FactTypes.Base44UiInputSemantics)).toBe(false);
    expect(packet.facts.some((fact) => fact.evidence.filePath === "src/StoryPanel.tsx"
      && fact.factType === FactTypes.Base44UiInputSemantics)).toBe(false);
    expect(semantics.every(({ value }) => value.reasons.every((item: { ruleId: string }) =>
      item.ruleId === "base44.ui-input-semantics.v1"))).toBe(true);

    const analyticsOnly = semantics.find(({ value }) => value.fieldBinding === "checkout_started");
    expect(analyticsOnly).toBeUndefined();

    const formatted = semantics.find(({ value }) => value.valueBinding === "formatCurrency(row.cost)");
    expect(formatted).toBeUndefined();

    expect(semantics.some(({ value }) => value.submittedEntity === "ReassignedPayload")).toBe(false);
    const reassignedControl = semantics.find(({ value }) => value.valueBinding === "row.reassignedCost");
    expect(reassignedControl?.value).toMatchObject({
      correlationStatus: "partial",
      submittedField: ""
    });

    const lexical = semantics.find(({ value }) => value.submittedEntity === "LexicalPayload" && value.submittedField === "amount");
    expect(lexical?.value).toMatchObject({
      correlationStatus: "proven",
      valueClass: "decimal"
    });

    const shadowedInput = semantics.find(({ value }) => value.valueBinding === "form.price"
      && value.controlKind === "input");
    expect(shadowedInput?.value).toMatchObject({
      correlationStatus: "partial",
      submittedEntity: "",
      submittedField: ""
    });
    expect(semantics.some(({ value }) => value.submittedEntity === "ShadowedInput"
      && value.controlKind !== "submitted-value")).toBe(false);

    const shadowedCast = semantics.find(({ value }) => value.valueBinding === "row.code"
      && value.controlKind === "input");
    expect(shadowedCast?.value).toMatchObject({
      correlationStatus: "partial",
      submittedEntity: "",
      submittedField: ""
    });
    expect(semantics.some(({ value }) => value.submittedEntity === "ShadowedCast"
      && value.controlKind !== "submitted-value")).toBe(false);

	    const dynamicType = semantics.find(({ value }) => value.valueBinding === "form.dynamicQuantity"
	      && value.controlKind === "input");
    expect(dynamicType?.value).toMatchObject({
      correlationStatus: "partial",
      valueClass: "unknown",
      submittedEntity: "",
      submittedField: ""
    });
    expect(dynamicType?.value.reasons).toContainEqual(expect.objectContaining({
      kind: "native-input-type",
      detail: "type=dynamic-unresolved"
	    }));

	    const noValueBinding = semantics.find(({ value }) => value.fieldBinding === "price"
	      && value.valueBinding === "" && value.controlKind === "input");
	    expect(noValueBinding?.value).toMatchObject({
	      correlationStatus: "partial",
	      submittedEntity: "",
	      submittedField: ""
	    });
	    expect(semantics.some(({ value }) => value.submittedEntity === "NoValueControl"
	      && value.controlKind !== "submitted-value")).toBe(false);

	    const trailingJsxSpread = semantics.find(({ value }) => value.valueBinding === "form.spreadPrice"
	      && value.controlKind === "input");
	    expect(trailingJsxSpread?.value).toMatchObject({
	      correlationStatus: "partial",
	      valueClass: "unknown",
	      submittedEntity: "",
	      submittedField: ""
	    });
	    expect(trailingJsxSpread?.value.reasons).toContainEqual(expect.objectContaining({
	      detail: "trailing-jsx-spread-unresolved"
	    }));
	    expect(semantics.some(({ value }) => value.submittedEntity === "TrailingJsxSpread"
	      && value.controlKind !== "submitted-value")).toBe(false);

    const trailingPayloadSpread = semantics.find(({ value }) => value.valueBinding === "row.spreadPayloadPrice"
      && value.controlKind === "input");
    expect(trailingPayloadSpread?.value).toMatchObject({
      correlationStatus: "partial",
      submittedEntity: "",
	      submittedField: ""
	    });
    expect(semantics.some(({ value }) => value.submittedEntity === "TrailingPayloadSpread"
      && value.controlKind !== "submitted-value")).toBe(false);

    const overriddenSpread = semantics.find(({ value }) => value.valueBinding === "row.overriddenSpreadPrice"
      && value.controlKind === "input");
    expect(overriddenSpread?.value).toMatchObject({
      correlationStatus: "partial",
      submittedEntity: "",
      submittedField: ""
    });
    expect(semantics.some(({ value }) => value.submittedEntity === "OverriddenSpread"
      && value.submittedField === "price"
      && value.valueBinding === "row.overriddenSpreadPrice"
      && value.controlKind !== "submitted-value")).toBe(false);
    const explicitOverride = semantics.find(({ value }) => value.valueBinding === "form.overridePrice"
      && value.controlKind === "input");
    expect(explicitOverride?.value).toMatchObject({
      correlationStatus: "proven",
      submittedEntity: "OverriddenSpread",
      submittedField: "price"
    });

    const escapedPayload = semantics.find(({ value }) => value.valueBinding === "row.escapedPrice"
      && value.controlKind === "input");
    expect(escapedPayload?.value).toMatchObject({
      correlationStatus: "partial",
      submittedEntity: "",
	      submittedField: ""
	    });
    expect(semantics.some(({ value }) => value.submittedEntity === "EscapedPayload"
      && value.controlKind !== "submitted-value")).toBe(false);

    const aliasedPayload = semantics.find(({ value }) => value.valueBinding === "row.aliasedPrice"
      && value.controlKind === "input");
    expect(aliasedPayload?.value).toMatchObject({
      correlationStatus: "partial",
      submittedEntity: "",
      submittedField: ""
    });
    expect(semantics.some(({ value }) => value.submittedEntity === "AliasedPayload"
      && value.controlKind !== "submitted-value")).toBe(false);

    const parameterShadowed = semantics.find(({ value }) => value.valueBinding === "row.shadowedPayloadPrice"
      && value.controlKind === "input");
    expect(parameterShadowed?.value).toMatchObject({
      correlationStatus: "partial",
      submittedEntity: "",
      submittedField: ""
    });
    expect(semantics.some(({ value }) => value.submittedEntity === "ParameterShadow"
      && value.controlKind !== "submitted-value")).toBe(false);

    const hoistedCast = semantics.find(({ value }) => value.valueBinding === "row.hoistedCode"
      && value.controlKind === "input");
    expect(hoistedCast?.value).toMatchObject({
      correlationStatus: "partial",
      submittedEntity: "",
	      submittedField: ""
	    });
    expect(semantics.some(({ value }) => value.submittedEntity === "HoistedCast"
      && value.controlKind !== "submitted-value")).toBe(false);

    const laterLexicalCast = semantics.find(({ value }) => value.valueBinding === "row.laterLexicalCode"
      && value.controlKind === "input");
    expect(laterLexicalCast?.value).toMatchObject({
      correlationStatus: "partial",
      submittedEntity: "",
      submittedField: ""
    });
    expect(semantics.some(({ value }) => value.submittedEntity === "LaterLexicalCast"
      && value.controlKind !== "submitted-value")).toBe(false);

    const truthyRequired = semantics.find(({ value }) => value.fieldBinding === "truthy_required"
      && value.controlKind === "input");
    expect(truthyRequired?.value.validation).toMatchObject({ required: true });

	    expect(semantics.some(({ value }) => value.submittedEntity === "UnknownSdk")).toBe(false);
	    expect(packet.facts.some((fact) => fact.evidence.filePath === "src/__mocks__/Mocked.tsx"
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

	    const tier4Forged = structuredClone(packet);
	    const tier4Fact = tier4Forged.facts.find((fact) => fact.factType === FactTypes.Base44UiInputSemantics)!;
	    const tier4Contract = JSON.parse(tier4Fact.properties.uiSemanticsJson);
	    const tier4Operation = structuredClone(tier4Forged.facts.find((fact) => fact.factType === FactTypes.Base44EntityOperation
	      && fact.properties.operationEvidenceId === tier4Contract.operationEvidenceId)!);
	    tier4Operation.factId = "fact-forged-tier4-operation";
	    tier4Operation.evidenceTier = "Tier4Unknown";
	    tier4Operation.properties.operationEvidenceId = "operation-ffffffffffffffffffff";
	    tier4Operation.properties.sdkIdentityGap = "sdk-identity-source-root-missing";
	    delete tier4Operation.properties.sdkIdentityJson;
	    tier4Forged.facts.push(tier4Operation);
	    tier4Contract.operationEvidenceId = tier4Operation.properties.operationEvidenceId;
	    tier4Fact.properties.uiSemanticsJson = JSON.stringify(tier4Contract);
	    const tier4Path = path.join(out, "forged-tier4-operation.json");
    await fs.writeFile(tier4Path, `${JSON.stringify(tier4Forged, null, 2)}\n`);
    await expect(diffBase44Evidence(path.join(out, "base44-evidence.json"), tier4Path,
      path.join(out, "forged-tier4-operation-diff.json"))).rejects.toThrow(/unsupported entity operation|invalid exact operation identity/u);

    const readForged = structuredClone(packet);
    const readFact = readForged.facts.find((fact) => fact.factType === FactTypes.Base44UiInputSemantics)!;
    const readContract = JSON.parse(readFact.properties.uiSemanticsJson);
    const readOperation = structuredClone(readForged.facts.find((fact) => fact.factType === FactTypes.Base44EntityOperation
      && fact.properties.entityName === readContract.submittedEntity)!);
    readOperation.factId = "fact-forged-read-operation";
    readOperation.properties.operationName = "filter";
    readOperation.properties.operationEvidenceId = "operation-eeeeeeeeeeeeeeeeeeee";
    readForged.facts.push(readOperation);
    readContract.operationName = "filter";
    readContract.operationEvidenceId = readOperation.properties.operationEvidenceId;
    readFact.properties.uiSemanticsJson = JSON.stringify(readContract);
    const readPath = path.join(out, "forged-read-operation.json");
    await fs.writeFile(readPath, `${JSON.stringify(readForged, null, 2)}\n`);
    await expect(diffBase44Evidence(path.join(out, "base44-evidence.json"), readPath,
      path.join(out, "forged-read-operation-diff.json"))).rejects.toThrow(/unsupported entity operation|invalid exact operation identity/u);
  });
});

async function fixtureRepo(): Promise<string> {
  const repo = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-ui-semantics-fixture-"));
  await fs.mkdir(path.join(repo, "src"), { recursive: true });
  await fs.mkdir(path.join(repo, "fixtures"), { recursive: true });
  await writeFrontendSdkAuthority(repo);
  await fs.writeFile(path.join(repo, "src", "Screen.tsx"), `import { base44 } from "@base44/sdk";
export function Screen({ row, form, shared }) {
  const payload = { amount: parseFloat(row.lexicalCost) };
  function Number(value) {
    return "sku-" + value;
  }
  function saveShadowed(form) {
    return base44.entities.ShadowedInput.create({ amount: form.price });
  }
  const shadowedPayload = { price: row.shadowedPayloadPrice };
  function saveParameterShadow(shadowedPayload) {
    return base44.entities.ParameterShadow.create(shadowedPayload);
  }
  async function saveHoistedCast() {
    await base44.entities.HoistedCast.create({ code: Number(row.hoistedCode) });
    function Number(value) {
      return "hoisted-" + value;
    }
  }
  async function saveLaterLexicalCast() {
    await base44.entities.LaterLexicalCast.create({ code: Number(row.laterLexicalCode) });
    const Number = (value) => "local-" + value;
  }
  async function save() {
	    await base44.entities.MaterialItemPriceBreak.create({ price: parseFloat(row.cost) });
	    await base44.entities.FeatureFlag.create({ enabled: form.enabled });
    await base44.entities.Appointment.create({ service_date: form.serviceDate });
    await base44.entities.WorkOrder.update("1", { status: form.status });
    await base44.entities.Invoice.create({ rate: form.rate });
    await base44.entities.First.create({ amount: shared.value });
    await base44.entities.Second.create({ amount: shared.value });
    await base44.entities.LexicalPayload.create(payload);
	    await saveShadowed({ price: form.price });
	    await base44.entities.ShadowedCast.create({ code: Number(row.code) });
	    await base44.entities.DynamicInput.create({ quantity: form.dynamicQuantity });
	    await base44.entities.NoValueControl.create({ price: unrelated.value });
    await base44.entities.TrailingJsxSpread.create({ price: form.spreadPrice });
    await base44.entities.TrailingPayloadSpread.create({ price: row.spreadPayloadPrice, ...overrides });
    const spreadBase = { price: row.overriddenSpreadPrice, note: row.overriddenSpreadNote };
    await base44.entities.OverriddenSpread.create({ ...spreadBase, price: form.overridePrice });
    const escaped = { price: row.escapedPrice };
    mutate(escaped);
    await base44.entities.EscapedPayload.create(escaped);
    const aliased = { price: row.aliasedPrice };
    const alias = aliased;
    alias.price = form.aliasOverridePrice;
    await base44.entities.AliasedPayload.create(aliased);
    await saveParameterShadow({ price: form.parameterShadowPrice });
    await saveHoistedCast();
    await saveLaterLexicalCast();
    const reassigned = { amount: row.reassignedCost };
	    reassigned.amount = row.otherCost;
	    await base44.entities.ReassignedPayload.create(reassigned);
  }
  function analytics() {
    window.analytics?.track("checkout_started");
  }
  return <form onSubmit={save}>
    {/* <input name="comment_only" type="number" value={fake.commentValue} /> */}
    <input name="cost" type="number" step="0.01" min="0" required value={row.cost} onChange={() => {}} />
    <input name="formatted_cost" type="text" value={formatCurrency(row.cost)} onChange={() => {}} />
    <input name="start_time" type="time" value={form.startTime} onChange={() => {}} />
    <input name="reassigned_amount" type="number" value={row.reassignedCost} onChange={() => {}} />
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
	    <input name="amount" type="number" value={form.price} onChange={() => {}} />
	    <input name="code" type="number" value={row.code} onChange={() => {}} />
	    <input name="quantity" type={form.dynamicKind} value={form.dynamicQuantity} onChange={() => {}} />
	    <input name="price" type="number" onChange={() => {}} />
	    <input name="price" type="number" value={form.spreadPrice} {...props} />
    <input name="price" type="number" value={row.spreadPayloadPrice} onChange={() => {}} />
    <input name="price" type="number" value={row.overriddenSpreadPrice} onChange={() => {}} />
    <input name="price" type="number" value={form.overridePrice} onChange={() => {}} />
    <input name="price" type="number" value={row.escapedPrice} onChange={() => {}} />
    <input name="price" type="number" value={row.aliasedPrice} onChange={() => {}} />
    <input name="price" type="number" value={row.shadowedPayloadPrice} onChange={() => {}} />
    <input name="code" type="number" value={row.hoistedCode} onChange={() => {}} />
    <input name="code" type="number" value={row.laterLexicalCode} onChange={() => {}} />
    <input name="truthy_required" type="text" required="required" value={form.truthyRequired} onChange={() => {}} />
	  </form>;
	}
	`);
  await fs.writeFile(path.join(repo, "src", "UnresolvedSdk.tsx"), `export function UnresolvedSdk({ row }) {
  base44.entities.UnknownSdk.create({ price: parseFloat(row.cost) });
  return <input name="price" type="number" value={row.cost} onChange={() => {}} />;
}
`);
	  await fs.mkdir(path.join(repo, "src", "generated"), { recursive: true });
	  await fs.mkdir(path.join(repo, "src", "__mocks__"), { recursive: true });
	  await fs.writeFile(path.join(repo, "src", "generated", "Widget.generated.tsx"), `export const generated = <input name="price" type="number" value={row.cost} onChange={() => {}} />;\n`);
	  await fs.writeFile(path.join(repo, "src", "__mocks__", "Mocked.tsx"), `export const mocked = <input name="price" type="number" value={row.cost} onChange={() => {}} />;\n`);
  await fs.writeFile(path.join(repo, "src", "StoryPanel.tsx"), `export const story = <input name="price" type="number" value={row.cost} onChange={() => {}} />;\n`);
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
