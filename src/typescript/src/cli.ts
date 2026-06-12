#!/usr/bin/env node
import path from "node:path";
import { scan } from "./scan/ScanEngine";
import { exportIndex } from "./export/IndexExporter";
import { parseHumanByteSize } from "./util/Paths";
import { ScanOptions } from "./facts/Models";

async function main(): Promise<void> {
  const args = process.argv.slice(2);
  if (args.length === 0 || args.includes("--help") || args.includes("-h")) {
    printHelp();
    process.exit(0);
  }
  const command = args.shift();
  if (command === "export") {
    const result = await exportIndex(parseExportOptions(args));
    process.stdout.write(`TraceMap TypeScript export wrote ${result.format} to ${result.outputPath}\n`);
    process.stdout.write(`Facts exported: ${result.factCount}\n`);
    return;
  }
  if (command !== "scan") {
    throw new Error(`Unknown command: ${command ?? ""}`);
  }
  const options = parseScanOptions(args);
  const result = await scan(options);
  process.stdout.write(`TraceMap TypeScript scan wrote ${result.facts.length} facts to ${path.resolve(options.outputPath)}\n`);
}

function parseExportOptions(args: string[]) {
  let indexPath = "";
  let outputPath = "";
  let format: "json" | "mermaid" = "json";
  for (let index = 0; index < args.length; index++) {
    const arg = args[index];
    switch (arg) {
      case "--index":
        indexPath = path.resolve(requireValue(args, ++index, arg));
        break;
      case "--out":
        outputPath = path.resolve(requireValue(args, ++index, arg));
        break;
      case "--format": {
        const value = requireValue(args, ++index, arg).toLowerCase();
        format = value === "mermaid" || value === "mmd" ? "mermaid" : "json";
        if (value !== "json" && value !== "mermaid" && value !== "mmd") {
          throw new Error("export --format must be json or mermaid");
        }
        break;
      }
      default:
        throw new Error(`Unknown option: ${arg}`);
    }
  }
  if (!indexPath || !outputPath) {
    throw new Error("export requires --index <path> and --out <path>");
  }
  return { indexPath, outputPath, format };
}

function parseScanOptions(args: string[]): ScanOptions {
  let repoPath = "";
  let outputPath = "";
  const projectPaths: string[] = [];
  const includeGlobs: string[] = [];
  const excludeGlobs: string[] = [];
  let maxFileByteSize = parseHumanByteSize("1mb");
  let semantic = true;

  for (let index = 0; index < args.length; index++) {
    const arg = args[index];
    switch (arg) {
      case "--repo":
        repoPath = requireValue(args, ++index, arg);
        break;
      case "--out":
        outputPath = requireValue(args, ++index, arg);
        break;
      case "--project":
        projectPaths.push(requireValue(args, ++index, arg));
        break;
      case "--include":
        includeGlobs.push(requireValue(args, ++index, arg));
        break;
      case "--exclude":
        excludeGlobs.push(requireValue(args, ++index, arg));
        break;
      case "--max-file-byte-size":
        maxFileByteSize = parseHumanByteSize(requireValue(args, ++index, arg));
        break;
      case "--no-semantic":
        semantic = false;
        break;
      default:
        throw new Error(`Unknown option: ${arg}`);
    }
  }
  if (!repoPath || !outputPath) {
    throw new Error("scan requires --repo <path> and --out <path>");
  }
  return {
    repoPath: path.resolve(repoPath),
    outputPath: path.resolve(outputPath),
    projectPaths,
    includeGlobs,
    excludeGlobs,
    maxFileByteSize,
    semantic
  };
}

function requireValue(args: string[], index: number, option: string): string {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    throw new Error(`${option} requires a value`);
  }
  return value;
}

function printHelp(): void {
  process.stdout.write(`TraceMap TypeScript scanner

Usage:
  tracemap-ts scan --repo <path> --out <path> [options]
  tracemap-ts export --index <path> --out <path> [--format <json|mermaid>]

Options:
  --project <path>              Explicit tsconfig.json or project directory. Repeatable.
  --include <glob>              Include simple glob/path pattern. Repeatable.
  --exclude <glob>              Exclude simple glob/path pattern. Repeatable.
  --max-file-byte-size <size>   Max extraction file size. Default: 1mb.
  --no-semantic                 Force syntax-only scan.
  -h, --help                    Show help.
`);
}

main().catch((error: unknown) => {
  const message = error instanceof Error ? error.message : String(error);
  process.stderr.write(`${message}\n`);
  process.exit(1);
});
