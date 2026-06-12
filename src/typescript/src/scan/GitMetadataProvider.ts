import path from "node:path";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { GitMetadata } from "../facts/Models";

const execFileAsync = promisify(execFile);

export async function getGitMetadata(repoPath: string): Promise<GitMetadata> {
  const repoName = path.basename(repoPath);
  try {
    const [commitSha, branch, remoteUrl] = await Promise.all([
      git(repoPath, ["rev-parse", "HEAD"]),
      git(repoPath, ["rev-parse", "--abbrev-ref", "HEAD"]),
      git(repoPath, ["config", "--get", "remote.origin.url"]).catch(() => null)
    ]);
    return {
      repoName,
      remoteUrl,
      branch,
      commitSha: commitSha ?? "unknown",
      knownGaps: []
    };
  } catch {
    return {
      repoName,
      remoteUrl: null,
      branch: null,
      commitSha: "unknown",
      knownGaps: ["Git metadata unavailable."]
    };
  }
}

async function git(cwd: string, args: string[]): Promise<string | null> {
  const { stdout } = await execFileAsync("git", args, { cwd, timeout: 5000, maxBuffer: 1024 * 1024 });
  const value = stdout.trim();
  return value.length > 0 ? value : null;
}
