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
    expect(packet.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.Base44FunctionInvocation,
      targetSymbol: "serviceFunction"
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

  it("fails closed on invalid source identities and marks reduced-coverage diffs", async () => {
    const repo = await fixtureRepo();
    const out = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-out-"));
    await expect(buildBase44Evidence({ ...options(repo, out), acceptedSourceSha256: "not-a-sha" })).rejects.toThrow("64-character SHA-256");

    const beforeOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-before-"));
    const afterOut = await fs.mkdtemp(path.join(os.tmpdir(), "tracemap-base44-after-"));
    await buildBase44Evidence(options(repo, beforeOut));
    const after = await buildBase44Evidence(options(repo, afterOut));
    after.packet.coverage.knownGaps.push("seeded coverage loss");
    await fs.writeFile(path.join(afterOut, "base44-evidence.json"), `${JSON.stringify(after.packet, null, 2)}\n`);
    const diff = await diffBase44Evidence(path.join(beforeOut, "base44-evidence.json"), path.join(afterOut, "base44-evidence.json"), path.join(afterOut, "diff.json"));
    expect(diff.coverageReduced).toBe(true);
    expect(diff.coverageEvidence).toEqual(expect.objectContaining({
      beforeAnalysisLevel: "Level3SyntaxAnalysis",
      afterAnalysisLevel: "Level3SyntaxAnalysis",
      addedKnownGaps: ["seeded coverage loss"]
    }));
    expect(diff.limitations.join(" ")).toContain("must not be interpreted as clean absence");

    const extensionlessOutput = path.join(afterOut, "extensionless-diff");
    await diffBase44Evidence(path.join(beforeOut, "base44-evidence.json"), path.join(afterOut, "base44-evidence.json"), extensionlessOutput);
    expect(JSON.parse(await fs.readFile(extensionlessOutput, "utf8")).schemaVersion).toBe("tracemap.base44.static-diff.v1");
    expect(await fs.readFile(`${extensionlessOutput}.md`, "utf8")).toContain("# TraceMap Base44 Static Diff");
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
  base44.asServiceRole.functions.invoke("serviceFunction");
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
