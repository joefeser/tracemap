import fs from "node:fs";
import path from "node:path";
import ts from "typescript";
import { FileInventoryItem, ScanOptions } from "../facts/Models";
import { createCompilerHostWithCache, CompilerHostCache } from "../util/CompilerHost";
import { repoRelative } from "../util/Paths";

export interface LoadedProject {
  projectPath: string;
  parsed: ts.ParsedCommandLine;
  program: ts.Program;
  checker: ts.TypeChecker;
  sourceFiles: ts.SourceFile[];
  diagnostics: ts.Diagnostic[];
  skippedFiles: Set<string>;
}

export async function loadTypeScriptProjects(repoPath: string, options: ScanOptions, inventory: readonly FileInventoryItem[]): Promise<LoadedProject[]> {
  const projectPaths = discoverProjectPaths(repoPath, options, inventory);
  const visited = new Set<string>();
  const loaded: LoadedProject[] = [];
  const cache: CompilerHostCache = { parsedCommandLines: new Map(), sourceFiles: new Map() };
  for (const projectPath of projectPaths) {
    loadProjectRecursive(repoPath, projectPath, options, cache, visited, loaded);
  }
  return loaded;
}

export function discoverProjectPaths(repoPath: string, options: ScanOptions, inventory: readonly FileInventoryItem[]): string[] {
  if (options.projectPaths.length > 0) {
    return options.projectPaths.map((projectPath) => {
      const absolute = path.resolve(repoPath, projectPath);
      return fs.existsSync(absolute) && fs.statSync(absolute).isDirectory() ? path.join(absolute, "tsconfig.json") : absolute;
    });
  }
  return inventory
    .filter((item) => path.basename(item.relativePath) === "tsconfig.json")
    .map((item) => item.absolutePath)
    .sort();
}

function loadProjectRecursive(
  repoPath: string,
  projectPath: string,
  options: ScanOptions,
  cache: CompilerHostCache,
  visited: Set<string>,
  loaded: LoadedProject[]
): void {
  const normalizedProjectPath = path.resolve(projectPath);
  if (visited.has(normalizedProjectPath) || !fs.existsSync(normalizedProjectPath)) {
    return;
  }
  visited.add(normalizedProjectPath);
  const config = ts.readConfigFile(normalizedProjectPath, ts.sys.readFile);
  if (config.error) {
    const parsed = emptyParsed(normalizedProjectPath);
    const program = ts.createProgram([], parsed.options);
    loaded.push({
      projectPath: repoRelative(repoPath, normalizedProjectPath),
      parsed,
      program,
      checker: program.getTypeChecker(),
      sourceFiles: [],
      diagnostics: [config.error],
      skippedFiles: new Set()
    });
    return;
  }
  const parsed = ts.parseJsonConfigFileContent(config.config, ts.sys, path.dirname(normalizedProjectPath), undefined, normalizedProjectPath);
  cache.parsedCommandLines.set(normalizedProjectPath, parsed);
  for (const reference of parsed.projectReferences ?? []) {
    const referencePath = path.resolve(path.dirname(normalizedProjectPath), reference.path);
    const configPath = fs.existsSync(referencePath) && fs.statSync(referencePath).isDirectory() ? path.join(referencePath, "tsconfig.json") : referencePath;
    loadProjectRecursive(repoPath, configPath, options, cache, visited, loaded);
  }
  const skippedFiles = new Set<string>();
  const host = createCompilerHostWithCache(parsed.options, cache, options.maxFileByteSize, skippedFiles);
  const program = ts.createProgram(parsed.fileNames, parsed.options, host);
  const diagnostics = [
    ...parsed.errors,
    ...program.getConfigFileParsingDiagnostics(),
    ...program.getOptionsDiagnostics(),
    ...program.getSyntacticDiagnostics(),
    ...program.getSemanticDiagnostics()
  ];
  const sourceFiles = program
    .getSourceFiles()
    .filter((sourceFile) => parsed.fileNames.includes(sourceFile.fileName))
    .filter((sourceFile) => sourceFile.fileName.startsWith(repoPath));
  loaded.push({
    projectPath: repoRelative(repoPath, normalizedProjectPath),
    parsed,
    program,
    checker: program.getTypeChecker(),
    sourceFiles,
    diagnostics,
    skippedFiles
  });
}

function emptyParsed(configPath: string): ts.ParsedCommandLine {
  return {
    options: {},
    fileNames: [],
    errors: [],
    projectReferences: undefined,
    typeAcquisition: { enable: false },
    raw: {},
    wildcardDirectories: {},
    compileOnSave: false
  } satisfies ts.ParsedCommandLine;
}
