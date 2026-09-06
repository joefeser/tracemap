import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { describe, expect, it } from "vitest";
import { buildBase44Evidence } from "../src/base44/Base44EvidencePacket";
import { FactTypes } from "../src/facts/Models";

const sink = "function sink(client) { client.entities.ReviewItem.list(); }";
const sdk = 'import { base44, createClient } from "@base44/sdk";';

describe("Base44 injected client review regressions", () => {
  it.each([
    ["distinct property paths with the same dotted text", `${sdk} function sink(client) { client.ReviewItem.list(); } sink(base44["asServiceRole.entities"]); sink(base44.asServiceRole.entities);`],
    ["unknown upstream call", `${sdk} ${sink} function forward(client) { sink(client); } forward(base44); forward({});`],
    ["unknown across a recursive cycle", `${sdk} ${sink} function forward(client) { sink(client); again(client); } function again(client) { forward(client); } forward(base44); again({});`],
    ["missing upstream argument", `${sdk} ${sink} function forward(client) { sink(client); } forward(base44); forward();`],
    ["shadowed factory argument", `${sdk} ${sink} function run(createClient) { sink(createClient()); } run(() => ({}));`],
    ["shadowed factory initializer", `${sdk} ${sink} function run(createClient) { const client = createClient(); sink(client); } run(() => ({}));`],
    ["shadowed namespace factory", `import * as sdk from "@base44/sdk"; ${sink} function run(sdk) { sink(sdk.createClient()); } run(fake);`],
    ["destructured alias", `${sdk} ${sink} const { unrelated: client } = base44; sink(client);`],
    ["destructuring-default parameter write", `${sdk} function sink(client) { ([client = {}] = values); client.entities.ReviewItem.list(); } sink(base44);`],
    ["for-of parameter write", `${sdk} function sink(client) { for (client of values) { client.entities.ReviewItem.list(); } } sink(base44);`],
    ["for-in parameter write", `${sdk} function sink(client) { for (client in values) { client.entities.ReviewItem.list(); } } sink(base44);`],
    ["rest parameter", `${sdk} function sink(...client) { client.entities.ReviewItem.list(); } sink(base44);`],
    ["spread argument positions", `${sdk} function sink(first, client) { client.entities.ReviewItem.list(); } sink(...values, base44);`],
    ["named function expression", `${sdk} const run = function base44() { base44.entities.ReviewItem.list(); }; run();`],
    ["named class expression", `${sdk} const Run = class base44 { run() { base44.entities.ReviewItem.list(); } };`]
  ])("rejects false SDK authority: %s", async (_name, app) => {
    const packet = await scanFixture({ "app.ts": app });
    expect(packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation)).toEqual([]);
  });

  it("retains injected provenance on function invocation facts", async () => {
    const packet = await scanFixture({ "app.ts": `${sdk} function invoke(client) { client.functions.invoke("reviewHelper"); } invoke(base44);` });
    expect(packet.facts.find((fact) => fact.factType === FactTypes.Base44FunctionInvocation)?.properties.clientBindingKind).toBe("callsite-proven-parameter");
  });

  it.each([
    ["seeded mutual recursion", `${sdk} ${sink} function forward(client) { sink(client); again(client); } function again(client) { forward(client); } forward(base44);`],
    ["direct factory", `${sdk} ${sink} sink(createClient());`],
    ["immutable factory alias", `${sdk} ${sink} const make = createClient; sink(make());`],
    ["namespace factory alias", `import * as sdk from "@base44/sdk"; ${sink} const ns = sdk; sink(ns.createClient());`],
    ["spread after client", `${sdk} ${sink} sink(base44, ...values);`]
  ])("preserves proven callsites: %s", async (_name, app) => {
    const packet = await scanFixture({ "app.ts": app });
    expect(packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation)).toEqual([
      expect.objectContaining({ targetSymbol: "ReviewItem", properties: expect.objectContaining({ clientBindingKind: "callsite-proven-parameter" }) })
    ]);
  });

  it.each([
    "export const helper = (client) => client.entities.ReviewItem.list();",
    "const helper = (client) => client.entities.ReviewItem.list(); export { helper };",
    "const helper = (client) => client.entities.ReviewItem.list(); export default helper;"
  ])("recognizes immutable exported helper expressions: %s", async (helper) => {
    const packet = await scanFixture({
      "helper.ts": helper,
      "app.ts": `${sdk} import ${helper.includes("export default") ? "helper" : "{ helper }"} from "./helper"; helper(base44);`
    });
    expect(packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation)).toEqual([
      expect.objectContaining({ targetSymbol: "ReviewItem", properties: expect.objectContaining({ clientBindingKind: "callsite-proven-parameter" }) })
    ]);
  });

  it.each([
    `import { createClient } from "@base44/sdk"; export let client = createClient(); client = {};`,
    `import { base44 } from "@base44/sdk"; export const client = {}; function unrelated() { const client = base44; }`,
    `import { createClient } from "@base44/sdk"; const client = {}; function unrelated(createClient) { const client = createClient(); } export { client };`
  ])("does not import client authority from mutable or unrelated export bindings: %s", async (wrapper) => {
    const packet = await scanFixture({
      "wrapper.ts": wrapper,
      "app.ts": `import { client } from "./wrapper"; ${sink} sink(client);`
    });
    expect(packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation)).toEqual([]);
  });
});

async function scanFixture(files: Record<string, string>) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-injected-review-"));
  const repo = path.join(root, "repo");
  try {
    await fs.mkdir(repo);
    for (const [name, text] of Object.entries(files)) await fs.writeFile(path.join(repo, name), text);
    execFileSync("git", ["init", "-q"], { cwd: repo });
    execFileSync("git", ["add", "."], { cwd: repo });
    execFileSync("git", ["-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-qm", "fixture"], { cwd: repo });
    const { packet } = await buildBase44Evidence({
      repoPath: repo, outputPath: path.join(root, "out"), projectPaths: [], includeGlobs: [], excludeGlobs: [],
      maxFileByteSize: 1024 * 1024, semantic: false,
      acceptedSourceSha256: "a".repeat(64), acceptedTreeSha256: "b".repeat(64), coverageLabel: "controlled-review-fixture"
    });
    return packet;
  } finally {
    await fs.rm(root, { recursive: true, force: true });
  }
}
