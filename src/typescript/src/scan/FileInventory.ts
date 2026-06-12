import fs from "node:fs/promises";
import path from "node:path";
import { FileInventoryItem, ScanOptions } from "../facts/Models";
import { matchesSimpleGlob, normalizePath, repoRelative } from "../util/Paths";

const defaultExcludedNames = new Set([".git", "node_modules", ".pnpm-store", "dist", "build", "coverage", ".next", ".nuxt", ".turbo"]);
const sourceExtensions = new Set([".ts", ".tsx", ".d.ts", ".json"]);

export async function collectFileInventory(options: ScanOptions): Promise<FileInventoryItem[]> {
  const repoPath = path.resolve(options.repoPath);
  const outputPath = path.resolve(options.outputPath);
  const files: FileInventoryItem[] = [];
  await visit(repoPath, repoPath, outputPath, options, files);
  return files.sort((left, right) => left.relativePath.localeCompare(right.relativePath));
}

async function visit(root: string, current: string, outputPath: string, options: ScanOptions, files: FileInventoryItem[]): Promise<void> {
  const entries = await fs.readdir(current, { withFileTypes: true });
  for (const entry of entries) {
    const absolutePath = path.join(current, entry.name);
    if (entry.isDirectory()) {
      if (defaultExcludedNames.has(entry.name) || isOutputDirectory(absolutePath, outputPath)) {
        continue;
      }
      await visit(root, absolutePath, outputPath, options, files);
      continue;
    }
    if (!entry.isFile()) {
      continue;
    }
    const relativePath = repoRelative(root, absolutePath);
    if (isExcluded(relativePath, options) || !isIncluded(relativePath, options)) {
      continue;
    }
    if (!isSupported(relativePath)) {
      continue;
    }
    const stat = await fs.stat(absolutePath);
    files.push({
      relativePath: normalizePath(relativePath),
      absolutePath,
      kind: kindFor(relativePath),
      sizeBytes: stat.size,
      skipped: stat.size > options.maxFileByteSize
    });
  }
}

function isSupported(relativePath: string): boolean {
  if (path.basename(relativePath) === "package.json") {
    return true;
  }
  if (/^tsconfig.*\.json$/.test(path.basename(relativePath))) {
    return true;
  }
  return sourceExtensions.has(path.extname(relativePath)) && !relativePath.endsWith(".js");
}

function kindFor(relativePath: string): string {
  if (relativePath.endsWith(".d.ts")) {
    return "typescript-declaration";
  }
  if (relativePath.endsWith(".tsx")) {
    return "typescript-tsx";
  }
  if (relativePath.endsWith(".ts")) {
    return "typescript";
  }
  if (path.basename(relativePath) === "package.json") {
    return "package-json";
  }
  return "json-config";
}

function isOutputDirectory(candidate: string, outputPath: string): boolean {
  const relative = path.relative(outputPath, candidate);
  return relative.length === 0 || (!relative.startsWith("..") && !path.isAbsolute(relative));
}

function isExcluded(relativePath: string, options: ScanOptions): boolean {
  return options.excludeGlobs.some((glob) => matchesSimpleGlob(relativePath, glob));
}

function isIncluded(relativePath: string, options: ScanOptions): boolean {
  return options.includeGlobs.length === 0 || options.includeGlobs.some((glob) => matchesSimpleGlob(relativePath, glob));
}
