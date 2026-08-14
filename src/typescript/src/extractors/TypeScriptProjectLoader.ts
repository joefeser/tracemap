import fs from "node:fs";
import path from "node:path";
import ts from "typescript";
import { FileInventoryItem, ScanOptions } from "../facts/Models";
import { createCompilerHostWithCache, CompilerHostCache } from "../util/CompilerHost";
import { hashBytes } from "../util/Hash";
import { isUnderPath, repoRelative } from "../util/Paths";

export interface LoadedProject {
  projectPath: string;
  parsed: ts.ParsedCommandLine;
  program: ts.Program;
  checker: ts.TypeChecker;
  sourceFiles: ts.SourceFile[];
  diagnostics: ts.Diagnostic[];
  skippedFiles: Set<string>;
}

export interface LoadedProjectSet {
  projects: LoadedProject[];
  compilerInputTokens: string[];
}

export async function loadTypeScriptProjects(repoPath: string, options: ScanOptions, inventory: readonly FileInventoryItem[]): Promise<LoadedProjectSet> {
  const projectPaths = discoverProjectPaths(repoPath, options, inventory);
  const visited = new Set<string>();
  const loaded: LoadedProject[] = [];
  const cache: CompilerHostCache = { parsedCommandLines: new Map(), sourceFiles: new Map(), configFiles: new Map() };
  const selectedPaths = new Set(inventory.filter((item) => !item.skipped).map((item) => path.resolve(item.absolutePath)));
  for (const projectPath of projectPaths) {
    loadProjectRecursive(repoPath, projectPath, options, selectedPaths, cache, visited, loaded);
  }
  return {
    projects: loaded,
    compilerInputTokens: [
      ...[...cache.sourceFiles.entries()]
        .filter(([fileName]) => !selectedPaths.has(path.resolve(fileName)))
        .map(([, [sourceFile]]) => {
          const analyzedText = Buffer.from(sourceFile.text, "utf8");
          return `source\0${analyzedText.byteLength}\0${hashBytes(analyzedText)}\0`;
        }),
      ...[...cache.configFiles.entries()]
        .filter(([fileName]) => !selectedPaths.has(path.resolve(fileName)))
        .map(([, configText]) => {
          const analyzedText = Buffer.from(configText, "utf8");
          return `config\0${analyzedText.byteLength}\0${hashBytes(analyzedText)}\0`;
        })
    ].sort()
  };
}

export function discoverProjectPaths(repoPath: string, options: ScanOptions, inventory: readonly FileInventoryItem[]): string[] {
  if (options.projectPaths.length > 0) {
    const selectedPaths = new Set(inventory.filter((item) => !item.skipped).map((item) => path.resolve(item.absolutePath)));
    return options.projectPaths
      .map((projectPath) => {
        const absolute = path.resolve(repoPath, projectPath);
        return fs.existsSync(absolute) && fs.statSync(absolute).isDirectory() ? path.join(absolute, "tsconfig.json") : absolute;
      })
      .filter((projectPath) => selectedPaths.has(path.resolve(projectPath)));
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
  selectedPaths: ReadonlySet<string>,
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
  const parseConfigHost: ts.ParseConfigHost = {
    useCaseSensitiveFileNames: ts.sys.useCaseSensitiveFileNames,
    readDirectory: ts.sys.readDirectory,
    fileExists: ts.sys.fileExists,
    readFile: (fileName) => {
      const text = ts.sys.readFile(fileName);
      if (text !== undefined) {
        cache.configFiles.set(path.resolve(fileName), text);
      }
      return text;
    }
  };
  const parsed = ts.parseJsonConfigFileContent(config.config, parseConfigHost, path.dirname(normalizedProjectPath), undefined, normalizedProjectPath);
  cache.parsedCommandLines.set(normalizedProjectPath, parsed);
  for (const reference of parsed.projectReferences ?? []) {
    const referencePath = path.resolve(path.dirname(normalizedProjectPath), reference.path);
    const configPath = fs.existsSync(referencePath) && fs.statSync(referencePath).isDirectory() ? path.join(referencePath, "tsconfig.json") : referencePath;
    if (selectedPaths.has(path.resolve(configPath))) {
      loadProjectRecursive(repoPath, configPath, options, selectedPaths, cache, visited, loaded);
    }
  }
  parsed.fileNames = parsed.fileNames.filter((fileName) => selectedPaths.has(path.resolve(fileName)));
  const skippedFiles = new Set<string>();
  const host = createCompilerHostWithCache(
    parsed.options,
    cache,
    options.maxFileByteSize,
    skippedFiles,
    (fileName) => {
      const absoluteFileName = path.resolve(fileName);
      return !isUnderPath(absoluteFileName, repoPath)
        || selectedPaths.has(absoluteFileName)
        || isCompilerDependencyInput(repoPath, absoluteFileName);
    });
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

function isCompilerDependencyInput(repoPath: string, fileName: string): boolean {
  const relative = path.relative(repoPath, fileName);
  return relative.split(path.sep).some((segment) => segment === "node_modules" || segment === ".pnpm-store");
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
