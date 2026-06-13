import fs from "node:fs";
import fsp from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { scan } from "../src/scan/ScanEngine";
import { FactTypes } from "../src/facts/Models";
import { RuleIds } from "../src/facts/RuleIds";

describe("Angular HttpClient extraction", () => {
  it("emits normalized HttpClient endpoint facts without raw URLs", async () => {
    const repo = await tempDir();
    await fsp.mkdir(path.join(repo, "src", "app"), { recursive: true });
    await fsp.mkdir(path.join(repo, "src", "environments"), { recursive: true });
    await fsp.writeFile(path.join(repo, "tsconfig.json"), `{
      // JSONC comments should not create config parse gaps.
      "compilerOptions": { "target": "ES2022", "module": "CommonJS", "strict": true }
    }`);
    await fsp.writeFile(path.join(repo, "src", "angular-http.d.ts"), `
      declare module "@angular/common/http" {
        export class HttpClient {
          get<T>(url: string): T;
          post<T>(url: string, body?: unknown): T;
        }
      }
    `);
    await fsp.writeFile(path.join(repo, "src", "environments", "environment.ts"), `
      export const environment = { apiUri: "https://localhost:44396/api" };
    `);
    await fsp.writeFile(path.join(repo, "src", "app", "runner.service.ts"), `
      import { HttpClient } from "@angular/common/http";
      import { environment } from "../environments/environment";

      export class RunnerService {
        constructor(private http: HttpClient) {}

        getById(runnerId: string) {
          return this.http.get(\`\${environment.apiUri}/admin/runner/get-by-id/\${runnerId}?includeHistory=true#fragment\`);
        }

        dynamic(path: string) {
          return this.http.post(path, {});
        }
      }
    `);

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

    const facts = result.facts.filter((fact) => fact.factType === FactTypes.HttpCallDetected && fact.ruleId === RuleIds.TypeScriptIntegrationAngularHttpClient);
    expect(facts).toHaveLength(2);
    const staticFact = facts.find((fact) => fact.properties.urlKind === "template");
    expect(staticFact?.properties.normalizedPathTemplate).toBe("/api/admin/runner/get-by-id/{runnerId}");
    expect(staticFact?.properties.normalizedPathKey).toBe("/api/admin/runner/get-by-id/{}");
    expect(staticFact?.properties.basePathPrefix).toBe("/api");
    expect(staticFact?.properties.queryParameterNames).toBe("includeHistory");
    expect(JSON.stringify(staticFact)).not.toContain("localhost:44396");

    const dynamicFact = facts.find((fact) => fact.properties.urlKind === "dynamic");
    expect(dynamicFact?.properties.dynamicReason).toBe("ComplexExpression");
    expect(fs.existsSync(path.join(out, "index.sqlite"))).toBe(true);
  });
});

async function tempDir(): Promise<string> {
  return fsp.mkdtemp(path.join(os.tmpdir(), "tracemap-angular-"));
}
