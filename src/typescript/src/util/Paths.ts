import path from "node:path";

export function normalizePath(value: string): string {
  return value.replaceAll(path.sep, "/");
}

export function repoRelative(repoPath: string, absolutePath: string): string {
  const relative = path.relative(repoPath, absolutePath);
  return normalizePath(relative.length === 0 ? "." : relative);
}

export function isUnderPath(candidate: string, parent: string): boolean {
  const relative = path.relative(parent, candidate);
  return relative.length === 0 || (!relative.startsWith("..") && !path.isAbsolute(relative));
}

export function parseHumanByteSize(value: string): number {
  const match = /^(\d+(?:\.\d+)?)(b|kb|mb|gb)?$/i.exec(value.trim());
  if (!match) {
    throw new Error(`Invalid byte size: ${value}`);
  }

  const amount = Number(match[1]);
  const unit = (match[2] ?? "b").toLowerCase();
  const multiplier = unit === "gb" ? 1024 ** 3 : unit === "mb" ? 1024 ** 2 : unit === "kb" ? 1024 : 1;
  return Math.floor(amount * multiplier);
}

export function matchesSimpleGlob(relativePath: string, glob: string): boolean {
  const normalized = normalizePath(relativePath);
  const pattern = normalizePath(glob);
  if (pattern === normalized) {
    return true;
  }
  if (pattern.endsWith("/**")) {
    return normalized.startsWith(pattern.slice(0, -3));
  }
  if (pattern.startsWith("**/*.")) {
    return normalized.endsWith(pattern.slice(4));
  }
  if (pattern.startsWith("*.")) {
    return normalized.endsWith(pattern.slice(1));
  }
  return normalized.includes(pattern);
}
