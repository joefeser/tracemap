import fs from "node:fs";
import ts from "typescript";

export interface CompilerHostCache {
  parsedCommandLines: Map<string, ts.ParsedCommandLine>;
  sourceFiles: Map<string, [ts.SourceFile, ts.ScriptTarget]>;
}

export function createCompilerHostWithCache(
  options: ts.CompilerOptions,
  cache: CompilerHostCache,
  maxFileByteSize: number,
  skippedFiles: Set<string>
): ts.CompilerHost {
  const host = ts.createCompilerHost(options);
  const originalGetSourceFile = host.getSourceFile.bind(host);
  host.getParsedCommandLine = (fileName: string) => {
    const cached = cache.parsedCommandLines.get(fileName);
    if (cached) {
      return cached;
    }
    return undefined;
  };
  host.getSourceFile = (fileName, languageVersion, onError, shouldCreateNewSourceFile) => {
    try {
      if (!fileName.includes("node_modules/typescript/lib/") && fs.existsSync(fileName) && fs.statSync(fileName).size > maxFileByteSize) {
        skippedFiles.add(fileName);
        return undefined;
      }
    } catch {
      // Let TypeScript report the inaccessible file.
    }
    const target = typeof languageVersion === "number" ? languageVersion : languageVersion.languageVersion;
    const cached = cache.sourceFiles.get(fileName);
    if (cached && cached[1] === target) {
      return cached[0];
    }
    const sourceFile = originalGetSourceFile(fileName, languageVersion, onError, shouldCreateNewSourceFile);
    if (sourceFile) {
      cache.sourceFiles.set(fileName, [sourceFile, target]);
    }
    return sourceFile;
  };
  return host;
}
