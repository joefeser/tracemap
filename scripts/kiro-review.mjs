#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import process from "node:process";

const cwd = process.cwd();
const validKinds = new Set(["spec", "re-review", "implementation"]);
const defaultTimeoutMs = 30 * 60 * 1000;

function usage(exitCode = 0) {
  const stream = exitCode === 0 ? process.stdout : process.stderr;
  stream.write(`Usage: node scripts/kiro-review.mjs --phase <spec-slug> [options]

Options:
  --kind <spec|re-review|implementation>  Review kind. Default: spec
  --model <model|auto>                    Kiro model. Default: auto
  --fresh                                 Start a fresh session. Default behavior
  --resume                                Resume most recent Kiro session for cwd
  --resume-id <session-id>                Resume a specific Kiro session
  --trust-tools <tools>                   Comma-separated tool allowlist. Default: fs_read,grep
  --timeout-ms <milliseconds>             Wrapper timeout. Default: 1800000
  --prompt-file <path>                    Use an explicit prompt file
  --dry-run                               Write prompt/meta only; do not invoke Kiro
  --help                                  Show this help

Examples:
  node scripts/kiro-review.mjs --phase sql-dependency-surfaces --kind spec --model auto --dry-run
  node scripts/kiro-review.mjs --phase sql-dependency-surfaces --kind spec --model claude-opus-4.8 --fresh
`);
  process.exit(exitCode);
}

function parseArgs(argv) {
  const options = {
    kind: "spec",
    model: "auto",
    fresh: false,
    resume: false,
    resumeId: null,
    trustTools: "fs_read,grep",
    timeoutMs: defaultTimeoutMs,
    promptFile: null,
    dryRun: false,
  };

  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    const nextValue = () => {
      const value = argv[index + 1];
      if (!value || value.startsWith("--")) {
        throw new Error(`Missing value for ${arg}`);
      }
      index += 1;
      return value;
    };

    switch (arg) {
      case "--help":
      case "-h":
        usage(0);
        break;
      case "--phase":
        options.phase = nextValue();
        break;
      case "--kind":
        options.kind = nextValue();
        break;
      case "--model":
        options.model = nextValue();
        break;
      case "--fresh":
        options.fresh = true;
        break;
      case "--resume":
        options.resume = true;
        break;
      case "--resume-id":
        options.resumeId = nextValue();
        break;
      case "--trust-tools":
        options.trustTools = nextValue();
        break;
      case "--timeout-ms":
        options.timeoutMs = Number.parseInt(nextValue(), 10);
        break;
      case "--prompt-file":
        options.promptFile = nextValue();
        break;
      case "--dry-run":
        options.dryRun = true;
        break;
      default:
        throw new Error(`Unknown argument: ${arg}`);
    }
  }

  if (!options.phase) {
    throw new Error("Missing required --phase <spec-slug>");
  }

  if (!validKinds.has(options.kind)) {
    throw new Error(`Invalid --kind '${options.kind}'. Expected one of: ${[...validKinds].join(", ")}`);
  }

  if (!Number.isFinite(options.timeoutMs) || options.timeoutMs <= 0) {
    throw new Error("--timeout-ms must be a positive integer");
  }

  const sessionModes = [options.fresh, options.resume, Boolean(options.resumeId)].filter(Boolean).length;
  if (sessionModes > 1) {
    throw new Error("Use only one of --fresh, --resume, or --resume-id");
  }

  return options;
}

function loadEnvFile(envPath) {
  if (!existsSync(envPath)) {
    return {};
  }

  const values = {};
  const text = readFileSync(envPath, "utf8");
  for (const rawLine of text.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith("#")) {
      continue;
    }

    const equals = line.indexOf("=");
    if (equals <= 0) {
      continue;
    }

    const key = line.slice(0, equals).trim();
    let value = line.slice(equals + 1).trim();
    if ((value.startsWith("\"") && value.endsWith("\"")) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }

    values[key] = value;
  }

  return values;
}

function nowStamp() {
  return new Date().toISOString().replace(/[:]/g, "").replace(/\.\d{3}Z$/, "Z");
}

function safeSegment(value) {
  return value.replace(/[^A-Za-z0-9_.-]+/g, "-");
}

function stripAnsi(text) {
  return text
    .replace(/\u001b\[[0-9;?]*[ -/]*[@-~]/g, "")
    .replace(/\u001b\][^\u0007]*(?:\u0007|\u001b\\)/g, "");
}

function readOptional(filePath) {
  return existsSync(filePath) ? readFileSync(filePath, "utf8") : "";
}

function gitValue(args) {
  const result = spawnSync("git", args, { cwd, encoding: "utf8" });
  return result.status === 0 ? result.stdout.trim() : "";
}

function buildPrompt(options) {
  if (options.promptFile) {
    const fullPath = path.resolve(cwd, options.promptFile);
    if (!existsSync(fullPath)) {
      throw new Error(`Prompt file not found: ${options.promptFile}`);
    }

    return readFileSync(fullPath, "utf8");
  }

  const specDir = path.join(cwd, ".kiro", "specs", options.phase);
  const reviewPacketPath = path.join(specDir, "review-packet.md");
  if (existsSync(reviewPacketPath)) {
    return readFileSync(reviewPacketPath, "utf8");
  }

  const requirementsPath = path.join(specDir, "requirements.md");
  const designPath = path.join(specDir, "design.md");
  const tasksPath = path.join(specDir, "tasks.md");
  const reviewPromptsPath = path.join(specDir, "review-prompts.md");
  const missing = [requirementsPath, designPath, tasksPath].filter((filePath) => !existsSync(filePath));
  if (missing.length > 0) {
    throw new Error(`Missing expected spec files:\n${missing.map((filePath) => `- ${path.relative(cwd, filePath)}`).join("\n")}`);
  }

  const branch = gitValue(["branch", "--show-current"]) || "unknown";
  const commit = gitValue(["rev-parse", "HEAD"]) || "unknown";
  const kindGuidance = {
    spec: "Review this Kiro spec for merge readiness. Focus on correctness, implementability, evidence boundaries, safety, missing tests, and whether the tasks are reviewable.",
    "re-review": "Re-review this Kiro spec after changes. Focus on whether previously identified blockers are resolved and whether any new blockers were introduced.",
    implementation: "Review this implementation against the approved spec. Focus on correctness, merge readiness, tests, safety, deterministic output, and evidence boundaries.",
  }[options.kind];

  return `# TraceMap Kiro Review Request

Expected mode: Review mode. Findings first, severity ordered; do not edit files.

Repository: joefeser/tracemap
Branch: ${branch}
Commit: ${commit}
Phase: ${options.phase}
Kind: ${options.kind}

TraceMap principles:
- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Do not add LLM calls, embeddings, vector DBs, or prompt-based classification.
- Do not display raw SQL, snippets, literal values, connection strings, raw URLs, or local absolute paths in public reports.

${kindGuidance}

Return:
- Blocking issues with exact file/section references.
- Important non-blocking issues.
- Suggested fixes.
- Missing tests.
- Whether this is ready to merge after fixes.

## requirements.md

${readFileSync(requirementsPath, "utf8")}

## design.md

${readFileSync(designPath, "utf8")}

## tasks.md

${readFileSync(tasksPath, "utf8")}

## review-prompts.md

${readOptional(reviewPromptsPath) || "_No review-prompts.md found._"}
`;
}

function writeJson(filePath, value) {
  writeFileSync(filePath, `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

function extractSessionId(text) {
  const match = /Chat SessionId:\s*([a-f0-9-]+)/i.exec(text);
  return match ? match[1] : null;
}

function looksLikeCompleteReview(text) {
  const lower = text.toLowerCase();
  return (
    lower.includes("verdict") ||
    lower.includes("blocking issue") ||
    lower.includes("blocking issues") ||
    lower.includes("important non-blocking") ||
    lower.includes("ready to merge") ||
    lower.includes("not ready to merge")
  );
}

function latestSessionId(env) {
  const result = spawnSync("kiro-cli", ["chat", "--list-sessions", "--format", "json"], {
    cwd,
    env,
    encoding: "utf8",
    timeout: 30_000,
  });

  if (result.status !== 0 || !result.stdout.trim()) {
    return null;
  }

  try {
    const groups = JSON.parse(result.stdout);
    const current = groups.find((group) => path.resolve(group.cwd ?? "") === cwd) ?? groups[0];
    const sessions = Array.isArray(current?.sessions) ? current.sessions : [];
    return sessions[0]?.sessionId ?? null;
  } catch {
    return null;
  }
}

function main() {
  let options;
  try {
    options = parseArgs(process.argv.slice(2));
  } catch (error) {
    console.error(error.message);
    usage(1);
  }

  const startedAt = new Date();
  const timestamp = nowStamp();
  const modelSegment = safeSegment(options.model);
  const outputDir = path.join(cwd, ".tmp", "kiro-reviews", options.phase);
  mkdirSync(outputDir, { recursive: true });

  const baseName = `${timestamp}-${options.kind}-${modelSegment}`;
  const promptPath = path.join(outputDir, `${baseName}.prompt.md`);
  const rawPath = path.join(outputDir, `${baseName}.raw.md`);
  const cleanPath = path.join(outputDir, `${baseName}.clean.md`);
  const metaPath = path.join(outputDir, `${baseName}.meta.json`);

  const envFileValues = loadEnvFile(path.join(cwd, ".env.kiro.local"));
  const env = { ...process.env, ...envFileValues };
  const prompt = buildPrompt(options);
  writeFileSync(promptPath, prompt, "utf8");

  const meta = {
    phase: options.phase,
    kind: options.kind,
    model: options.model,
    trustTools: options.trustTools,
    mode: options.resumeId ? "resume-id" : options.resume ? "resume" : "fresh",
    resumeId: options.resumeId,
    timeoutMs: options.timeoutMs,
    timedOut: false,
    terminatedBySignal: null,
    reviewComplete: false,
    status: null,
    startedAt: startedAt.toISOString(),
    finishedAt: null,
    promptPath: path.relative(cwd, promptPath),
    rawPath: path.relative(cwd, rawPath),
    cleanPath: path.relative(cwd, cleanPath),
    metaPath: path.relative(cwd, metaPath),
    sessionId: null,
    toolDenied: false,
    gitCommit: gitValue(["rev-parse", "HEAD"]) || null,
    gitBranch: gitValue(["branch", "--show-current"]) || null,
    dryRun: options.dryRun,
    auth: {
      kiroApiKeyPresent: Boolean(env.KIRO_API_KEY),
    },
  };

  if (options.dryRun) {
    meta.status = 0;
    meta.reviewComplete = false;
    meta.finishedAt = new Date().toISOString();
    writeFileSync(rawPath, "", "utf8");
    writeFileSync(cleanPath, "", "utf8");
    writeJson(metaPath, meta);
    printArtifacts({ promptPath, rawPath, cleanPath, metaPath, sessionId: null });
    return;
  }

  if (!env.KIRO_API_KEY) {
    console.error("KIRO_API_KEY is missing. Set it in .env.kiro.local or the process environment.");
    meta.status = 1;
    meta.finishedAt = new Date().toISOString();
    writeFileSync(rawPath, "", "utf8");
    writeFileSync(cleanPath, "", "utf8");
    writeJson(metaPath, meta);
    process.exit(1);
  }

  const args = ["chat", "--no-interactive", "--model", options.model, `--trust-tools=${options.trustTools}`];
  if (options.resumeId) {
    args.push("--resume-id", options.resumeId);
  } else if (options.resume) {
    args.push("--resume");
  }

  const result = spawnSync("kiro-cli", args, {
    cwd,
    env,
    input: prompt,
    encoding: "utf8",
    timeout: options.timeoutMs,
    maxBuffer: 50 * 1024 * 1024,
  });

  const raw = [result.stdout ?? "", result.stderr ?? ""].filter(Boolean).join("\n");
  const clean = stripAnsi(raw);
  writeFileSync(rawPath, raw, "utf8");
  writeFileSync(cleanPath, clean, "utf8");

  meta.timedOut = Boolean(result.error && result.error.code === "ETIMEDOUT");
  meta.terminatedBySignal = result.signal ?? null;
  meta.status = meta.timedOut ? 124 : result.status;
  meta.sessionId = extractSessionId(clean) ?? latestSessionId(env);
  meta.toolDenied = clean.includes("is rejected because it matches one or more rules on the denied list");
  meta.reviewComplete = meta.status === 0 && clean.trim().length > 0 && looksLikeCompleteReview(clean);
  meta.finishedAt = new Date().toISOString();
  writeJson(metaPath, meta);

  printArtifacts({ promptPath, rawPath, cleanPath, metaPath, sessionId: meta.sessionId });

  if (!meta.reviewComplete || meta.status !== 0) {
    process.exit(meta.status || 1);
  }
}

function printArtifacts({ promptPath, rawPath, cleanPath, metaPath, sessionId }) {
  console.log(`Prompt: ${path.relative(cwd, promptPath)}`);
  console.log(`Raw:    ${path.relative(cwd, rawPath)}`);
  console.log(`Clean:  ${path.relative(cwd, cleanPath)}`);
  console.log(`Meta:   ${path.relative(cwd, metaPath)}`);
  console.log(`Session: ${sessionId ?? ""}`);
}

main();
