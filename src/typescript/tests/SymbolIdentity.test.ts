import { describe, expect, it } from "vitest";
import ts from "typescript";
import { localSymbolKey } from "../src/symbols/TypeScriptSymbolIdentityProvider";

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
});
