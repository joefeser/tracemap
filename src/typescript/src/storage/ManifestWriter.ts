import fs from "node:fs/promises";
import path from "node:path";
import { ScanManifest } from "../facts/Models";

export async function writeManifest(filePath: string, manifest: ScanManifest): Promise<void> {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, `${JSON.stringify(manifest, null, 2)}\n`, "utf8");
}
