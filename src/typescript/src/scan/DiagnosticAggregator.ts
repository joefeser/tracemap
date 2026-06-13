import ts from "typescript";

export interface AggregatedDiagnostic {
  category: string;
  code: number;
  count: number;
  filePath: string;
  startLine: number;
  messageHash: string;
}

export function aggregateDiagnostics(diagnostics: readonly ts.Diagnostic[], repoPath: string, maxPerProject = 20): AggregatedDiagnostic[] {
  const groups = new Map<string, AggregatedDiagnostic>();
  for (const diagnostic of diagnostics) {
    const category = diagnosticCategory(diagnostic);
    const filePath = diagnostic.file ? relative(repoPath, diagnostic.file.fileName) : ".";
    const line = diagnostic.file && diagnostic.start !== undefined ? diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start).line + 1 : 1;
    const key = `${category}|${diagnostic.code}|${filePath}`;
    const current = groups.get(key);
    if (current) {
      current.count++;
      continue;
    }
    if (groups.size >= maxPerProject) {
      continue;
    }
    groups.set(key, {
      category,
      code: diagnostic.code,
      count: 1,
      filePath,
      startLine: line,
      messageHash: String(diagnostic.code)
    });
  }
  return [...groups.values()].sort((left, right) => left.filePath.localeCompare(right.filePath) || left.code - right.code);
}

function diagnosticCategory(diagnostic: ts.Diagnostic): string {
  if (diagnostic.code === 2307 || diagnostic.code === 2792) {
    return "missing-module";
  }
  if (diagnostic.code === 2688) {
    return "missing-type-definition";
  }
  if (diagnostic.category === ts.DiagnosticCategory.Error && diagnostic.file === undefined) {
    return "project-load";
  }
  return "ordinary-type-error";
}

function relative(repoPath: string, fileName: string): string {
  return fileName.startsWith(repoPath) ? fileName.slice(repoPath.length + 1).replaceAll("\\", "/") : fileName.replaceAll("\\", "/");
}
