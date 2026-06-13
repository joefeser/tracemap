import { describe, expect, it } from "vitest";
import fsp from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import ts from "typescript";
import { localSymbolKey } from "../src/symbols/TypeScriptSymbolIdentityProvider";
import { findNearestPackageIdentity } from "../src/extractors/PackageJsonExtractor";
import { matchesSimpleGlob } from "../src/util/Paths";

describe("TypeScriptSymbolIdentityProvider", () => {
  it("creates stable local symbol keys from file path and source position", () => {
    const source = ts.createSourceFile("/repo/src/file.ts", "function run() { const value = 1; return value; }", ts.ScriptTarget.Latest, true);
    let identifier: ts.Identifier | undefined;
    const visit = (node: ts.Node): void => {
      if (ts.isIdentifier(node) && node.text === "value" && !identifier) {
        identifier = node;
      }
      ts.forEachChild(node, visit);
    };
    visit(source);
    expect(identifier).toBeDefined();
    expect(localSymbolKey("/repo", source, identifier!, "value")).toBe("typescript local src/file.ts:1:24:value");
  });

  it("finds nearest package identity from resolved paths", async () => {
    const repo = await fsp.mkdtemp(path.join(os.tmpdir(), "tracemap-package-"));
    await fsp.mkdir(path.join(repo, "src"), { recursive: true });
    await fsp.writeFile(path.join(repo, "package.json"), JSON.stringify({ name: "@sample/app", version: "1.2.3" }));

    const identity = await findNearestPackageIdentity(repo, path.join(repo, "src", "file.ts"));

    expect(identity).toMatchObject({ name: "@sample/app", version: "1.2.3", rootPath: repo });
  });

  it("does not match /** globs against sibling path prefixes", () => {
    expect(matchesSimpleGlob("src/app/file.ts", "src/**")).toBe(true);
    expect(matchesSimpleGlob("src", "src/**")).toBe(true);
    expect(matchesSimpleGlob("src-other/file.ts", "src/**")).toBe(false);
  });
});
