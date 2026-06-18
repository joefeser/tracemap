import fs from "node:fs";
import fsp from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { describe, expect, it } from "vitest";
import { scan } from "../src/scan/ScanEngine";
import { FactTypes } from "../src/facts/Models";
import { RuleIds } from "../src/facts/RuleIds";

describe("Angular template binding extraction", () => {
  it("emits safe UI field, control, event, template variable, and gap facts", async () => {
    const repo = await tempDir();
    await fsp.mkdir(path.join(repo, "src", "app"), { recursive: true });
    await fsp.writeFile(path.join(repo, "tsconfig.json"), JSON.stringify({
      compilerOptions: { target: "ES2022", module: "CommonJS", strict: true },
      include: ["src/**/*.ts"]
    }, null, 2));
    await fsp.writeFile(path.join(repo, "src", "angular-core.d.ts"), `
      declare module "@angular/core" {
        export function Component(value: unknown): ClassDecorator;
      }
    `);
    await fsp.writeFile(path.join(repo, "src", "app", "profile.component.ts"), `
      import { Component } from "@angular/core";

      @Component({
        selector: "app-profile",
        templateUrl: "./profile.component.html"
      })
      export class ProfileComponent {
        user = { email: "" };
        save() {}
      }
    `);
    await fsp.writeFile(path.join(repo, "src", "app", "profile.component.html"), `
      <input [value]="user.email" (change)="save()" [(ngModel)]="user.email" name="email" ngModel #emailModel="ngModel">
      <input formControlName="email" />
      <input [formControlName]="'secondaryEmail'" />
      <input [formControlName]="selectedField" />
      <section [formGroup]="'profileForm'"></section>
      <section>{{ user.email }}</section>
      <section>{{ format(user.email) }}</section>
    `);
    await fsp.writeFile(path.join(repo, "src", "app", "docs.html"), `
      <article>{{ ordinary.docs }}</article>
      <button (click)="notAngular()">Save</button>
    `);
    initGitRepo(repo);

    const out = await tempDir();
    const result = await scan({
      repoPath: repo,
      outputPath: out,
      projectPaths: [],
      includeGlobs: [],
      excludeGlobs: [],
      maxFileByteSize: 1024 * 1024,
      semantic: true
    });

    const templateFacts = result.facts.filter((fact) => fact.ruleId === RuleIds.TypeScriptAngularTemplateBinding);
    expect(templateFacts).toEqual(expect.arrayContaining([
      expect.objectContaining({
        factType: FactTypes.UiTemplateBinding,
        targetSymbol: "user.email",
        properties: expect.objectContaining({
          uiFramework: "angular",
          componentClass: "ProfileComponent",
          propertyPath: "user.email",
          propertyName: "email",
          templateOrigin: "templateUrl"
        })
      })
    ]));
    expect(templateFacts.filter((fact) => fact.properties.bindingKind === "two-way")).toHaveLength(2);
    expect(result.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.UiEventBinding,
      ruleId: RuleIds.TypeScriptAngularEventBinding,
      targetSymbol: "save"
    }));
    expect(result.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.UiFormControlBinding,
      ruleId: RuleIds.TypeScriptAngularFormBinding,
      targetSymbol: "email"
    }));
    expect(result.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.UiFormControlBinding,
      ruleId: RuleIds.TypeScriptAngularFormBinding,
      targetSymbol: "secondaryEmail"
    }));
    expect(result.facts).not.toContainEqual(expect.objectContaining({
      factType: FactTypes.UiFormControlBinding,
      ruleId: RuleIds.TypeScriptAngularFormBinding,
      targetSymbol: "selectedField"
    }));
    expect(result.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.UiFormControlBinding,
      ruleId: RuleIds.TypeScriptAngularFormBinding,
      targetSymbol: "profileForm"
    }));
    expect(result.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.UiTemplateVariable,
      ruleId: RuleIds.TypeScriptAngularTemplateVariable,
      targetSymbol: "emailModel"
    }));
    expect(result.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.UiBindingGap,
      ruleId: RuleIds.TypeScriptAngularBindingGap,
      evidenceTier: "Tier4Unknown",
      properties: expect.objectContaining({ gapKind: "dynamic-template-expression" })
    }));
    expect(result.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.UiBindingGap,
      ruleId: RuleIds.TypeScriptAngularBindingGap,
      evidenceTier: "Tier4Unknown",
      properties: expect.objectContaining({ gapKind: "dynamic-form-control-name" })
    }));
    expect(JSON.stringify(result.facts)).not.toContain("format(user.email)");
    const angularUiFacts = result.facts.filter((fact) =>
      fact.ruleId.startsWith("typescript.angular.")
      || fact.factType.startsWith("Ui")
    );
    expect(angularUiFacts.some((fact) => fact.evidence.filePath.endsWith("docs.html"))).toBe(false);
    expect(fs.existsSync(path.join(out, "index.sqlite"))).toBe(true);
  });
});

async function tempDir(): Promise<string> {
  return fsp.mkdtemp(path.join(os.tmpdir(), "tracemap-angular-template-"));
}

function initGitRepo(repo: string): void {
  expect(spawnSync("git", ["init"], { cwd: repo, encoding: "utf8" }).status).toBe(0);
  expect(spawnSync("git", ["add", "."], { cwd: repo, encoding: "utf8" }).status).toBe(0);
  const env = {
    ...process.env,
    GIT_AUTHOR_DATE: "2026-01-01T00:00:00Z",
    GIT_COMMITTER_DATE: "2026-01-01T00:00:00Z"
  };
  expect(spawnSync("git", ["-c", "user.email=test@example.com", "-c", "user.name=TraceMap Test", "commit", "-m", "initial"], { cwd: repo, env, encoding: "utf8" }).status).toBe(0);
}
