import fs from "node:fs/promises";
import path from "node:path";
import { CodeFact } from "../facts/Models";

export async function writeFactsJsonl(filePath: string, facts: readonly CodeFact[]): Promise<void> {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  const ordered = [...facts].sort((left, right) => left.factId.localeCompare(right.factId));
  const content = ordered.map((fact) => JSON.stringify(fact)).join("\n") + (ordered.length > 0 ? "\n" : "");
  await fs.writeFile(filePath, content, "utf8");
}
