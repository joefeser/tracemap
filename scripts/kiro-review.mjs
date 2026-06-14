#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import process from "node:process";
import assert from "node:assert/strict";

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
  --save-review-text                      Persist prompt/raw/clean text artifacts
  --dry-run                               Build prompt/meta only; do not invoke Kiro
  --self-test                             Run wrapper unit tests and exit
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
    saveReviewText: false,
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
      case "--save-review-text":
        options.saveReviewText = true;
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

  options.phase = options.phase.trim();
  if (!/^[a-z0-9][a-z0-9-]*$/.test(options.phase)) {
    throw new Error(`Invalid --phase '${options.phase}'. Expected a lowercase spec slug such as contract-delta-impact-v2.`);
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

function loadConfiguredEnvFiles(baseEnv = process.env, baseDir = cwd) {
  const loaded = [];
  const values = {};
  const sourceByKey = {};
  const configuredPath = baseEnv.KIRO_ENV_FILE;
  const candidates = [];
  if (configuredPath) {
    candidates.push({ path: configuredPath, source: "KIRO_ENV_FILE" });
  }

  const configuredFullPath = configuredPath ? path.resolve(baseDir, configuredPath) : null;
  if (!configuredFullPath || !existsSync(configuredFullPath)) {
    candidates.push({ path: path.join(baseDir, ".env.kiro.local"), source: ".env.kiro.local" });
  }

  for (const candidate of candidates) {
    const fullPath = path.resolve(baseDir, candidate.path);
    if (!existsSync(fullPath)) {
      continue;
    }

    const loadedValues = loadEnvFile(fullPath);
    Object.assign(values, loadedValues);
    for (const key of Object.keys(loadedValues)) {
      sourceByKey[key] = candidate.source;
    }
    loaded.push(candidate.source);
    break;
  }

  return { values, loaded, sourceByKey };
}

function kiroProfileAvailable(env) {
  const result = spawnSync("kiro-cli", ["chat", "--list-models", "--format", "json"], {
    cwd,
    env,
    encoding: "utf8",
    timeout: 30_000,
    maxBuffer: 5 * 1024 * 1024,
  });

  return result.status === 0 && Boolean(result.stdout?.trim());
}

function resolveAuthMode(env, loadedEnvFiles, sourceByKey = {}, baseEnv = process.env, profileProbe = kiroProfileAvailable) {
  if (env.KIRO_API_KEY) {
    const keySource = baseEnv.KIRO_API_KEY ? "env" : sourceByKey.KIRO_API_KEY;
    const mode = keySource === "KIRO_ENV_FILE"
      ? "configured-env-file"
      : keySource === ".env.kiro.local"
        ? "env-file"
        : "env";

    return {
      mode,
      kiroApiKeyPresent: true,
      profileAvailable: null,
      loadedEnvFiles,
      kiroApiKeySource: keySource,
    };
  }

  const profileAvailable = profileProbe(env);
  return {
    mode: profileAvailable ? "profile" : "missing",
    kiroApiKeyPresent: false,
    profileAvailable,
    loadedEnvFiles,
    kiroApiKeySource: null,
  };
}

function runSelfTests() {
  const tempDir = mkdtempSync(path.join(tmpdir(), "tracemap-kiro-review-"));
  try {
    const configured = path.join(tempDir, "configured.env");
    const fallback = path.join(tempDir, ".env.kiro.local");
    writeFileSync(configured, "KIRO_API_KEY=configured-key\nOTHER=configured\n", "utf8");
    writeFileSync(fallback, "KIRO_API_KEY=fallback-key\nOTHER=fallback\n", "utf8");

    const configuredLoad = loadConfiguredEnvFiles({ KIRO_ENV_FILE: configured }, tempDir);
    assert.deepEqual(configuredLoad.loaded, ["KIRO_ENV_FILE"]);
    assert.equal(configuredLoad.values.KIRO_API_KEY, "configured-key");
    assert.equal(configuredLoad.sourceByKey.KIRO_API_KEY, "KIRO_ENV_FILE");

    const fallbackLoad = loadConfiguredEnvFiles({ KIRO_ENV_FILE: path.join(tempDir, "missing.env") }, tempDir);
    assert.deepEqual(fallbackLoad.loaded, [".env.kiro.local"]);
    assert.equal(fallbackLoad.values.KIRO_API_KEY, "fallback-key");
    assert.equal(fallbackLoad.sourceByKey.KIRO_API_KEY, ".env.kiro.local");

    const noFileDir = path.join(tempDir, "empty");
    mkdirSync(noFileDir);
    const noFileLoad = loadConfiguredEnvFiles({}, noFileDir);
    assert.deepEqual(noFileLoad.loaded, []);
    assert.deepEqual(noFileLoad.values, {});

    const configuredAuth = resolveAuthMode(
      { KIRO_API_KEY: "configured-key" },
      configuredLoad.loaded,
      configuredLoad.sourceByKey,
      {},
      () => false,
    );
    assert.equal(configuredAuth.mode, "configured-env-file");
    assert.equal(configuredAuth.kiroApiKeySource, "KIRO_ENV_FILE");

    const fallbackAuth = resolveAuthMode(
      { KIRO_API_KEY: "fallback-key" },
      fallbackLoad.loaded,
      fallbackLoad.sourceByKey,
      {},
      () => false,
    );
    assert.equal(fallbackAuth.mode, "env-file");
    assert.equal(fallbackAuth.kiroApiKeySource, ".env.kiro.local");

    const processEnvAuth = resolveAuthMode(
      { KIRO_API_KEY: "process-key" },
      configuredLoad.loaded,
      configuredLoad.sourceByKey,
      { KIRO_API_KEY: "process-key" },
      () => false,
    );
    assert.equal(processEnvAuth.mode, "env");
    assert.equal(processEnvAuth.kiroApiKeySource, "env");

    const profileAuth = resolveAuthMode({}, [], {}, {}, () => true);
    assert.equal(profileAuth.mode, "profile");
    assert.equal(profileAuth.profileAvailable, true);

    const missingAuth = resolveAuthMode({}, [], {}, {}, () => false);
    assert.equal(missingAuth.mode, "missing");
    assert.equal(missingAuth.profileAvailable, false);
  } finally {
    rmSync(tempDir, { recursive: true, force: true });
  }

  console.log("kiro-review self-test passed");
}

function nowStamp() {
  return new Date().toISOString().replace(/[:]/g, "").replace(".", "-");
}

function safeSegment(value) {
  return value.replace(/[^A-Za-z0-9_.-]+/g, "-");
}

function sha256(text) {
  return createHash("sha256").update(text, "utf8").digest("hex");
}

function resolveInside(baseDir, childSegment, description) {
  const base = path.resolve(baseDir);
  const target = path.resolve(base, childSegment);
  if (target !== base && !target.startsWith(`${base}${path.sep}`)) {
    throw new Error(`${description} escaped expected base directory`);
  }

  return target;
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

function requireGitValue(args, description) {
  const value = gitValue(args);
  if (!value) {
    throw new Error(`Unable to determine ${description}`);
  }

  return value;
}

function buildPrompt(options) {
  if (options.promptFile) {
    const fullPath = path.resolve(cwd, options.promptFile);
    if (!existsSync(fullPath)) {
      throw new Error(`Prompt file not found: ${options.promptFile}`);
    }

    return readFileSync(fullPath, "utf8");
  }

  const specDir = resolveInside(path.join(cwd, ".kiro", "specs"), options.phase, "Spec path");
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

  const branch = requireGitValue(["branch", "--show-current"], "git branch");
  const commit = requireGitValue(["rev-parse", "HEAD"], "git commit SHA");
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

function serializeSpawnError(error) {
  if (!error) {
    return null;
  }

  return {
    code: error.code ?? null,
    errno: error.errno ?? null,
    syscall: error.syscall ?? null,
    path: error.path ?? null,
    message: error.message ?? String(error),
  };
}

function statusFromSpawnResult(result) {
  if (result.error?.code === "ETIMEDOUT") {
    return 124;
  }

  if (result.status !== null && result.status !== undefined) {
    return result.status;
  }

  if (result.error?.code === "ENOENT") {
    return 127;
  }

  if (result.error?.code === "EACCES") {
    return 126;
  }

  return result.error ? 1 : 0;
}

function buildAnalysisGaps({ toolDenied, spawnError }) {
  const gaps = [];
  if (toolDenied) {
    gaps.push({
      kind: "ToolDenied",
      evidenceTier: "Tier4Unknown",
      ruleId: "kiro.review.wrapper.v1",
      message: "Kiro reported denied tool access; review coverage is reduced.",
    });
  }

  if (spawnError) {
    gaps.push({
      kind: "SpawnError",
      evidenceTier: "Tier4Unknown",
      ruleId: "kiro.review.wrapper.v1",
      message: "kiro-cli could not be executed completely; review coverage is reduced.",
    });
  }

  return gaps;
}

function main() {
  if (process.argv.includes("--self-test")) {
    runSelfTests();
    return;
  }

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
  const outputDir = resolveInside(path.join(cwd, ".tmp", "kiro-reviews"), options.phase, "Artifact path");
  mkdirSync(outputDir, { recursive: true });

  const baseName = `${timestamp}-${options.kind}-${modelSegment}`;
  const promptPath = path.join(outputDir, `${baseName}.prompt.md`);
  const rawPath = path.join(outputDir, `${baseName}.raw.md`);
  const cleanPath = path.join(outputDir, `${baseName}.clean.md`);
  const metaPath = path.join(outputDir, `${baseName}.meta.json`);

  const { values: envFileValues, loaded: loadedEnvFiles, sourceByKey } = loadConfiguredEnvFiles();
  const env = { ...envFileValues, ...process.env };
  const auth = resolveAuthMode(env, loadedEnvFiles, sourceByKey, process.env);
  const prompt = buildPrompt(options);
  if (options.saveReviewText) {
    writeFileSync(promptPath, prompt, "utf8");
  }

  const gitCommit = requireGitValue(["rev-parse", "HEAD"], "git commit SHA");
  const gitBranch = requireGitValue(["branch", "--show-current"], "git branch");

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
    reviewCoverage: "NotRun",
    status: null,
    startedAt: startedAt.toISOString(),
    finishedAt: null,
    saveReviewText: options.saveReviewText,
    promptPath: options.saveReviewText ? path.relative(cwd, promptPath) : null,
    rawPath: options.saveReviewText ? path.relative(cwd, rawPath) : null,
    cleanPath: options.saveReviewText ? path.relative(cwd, cleanPath) : null,
    metaPath: path.relative(cwd, metaPath),
    promptSha256: sha256(prompt),
    rawSha256: null,
    cleanSha256: null,
    sessionId: null,
    toolDenied: false,
    analysisGaps: [],
    spawned: null,
    spawnError: null,
    gitCommit,
    gitBranch,
    dryRun: options.dryRun,
    auth: {
      mode: auth.mode,
      kiroApiKeyPresent: auth.kiroApiKeyPresent,
      profileAvailable: auth.profileAvailable,
      loadedEnvFiles: auth.loadedEnvFiles,
      kiroApiKeySource: auth.kiroApiKeySource,
    },
  };

  if (options.dryRun) {
    meta.status = 0;
    meta.reviewComplete = false;
    meta.reviewCoverage = "NotRun";
    meta.finishedAt = new Date().toISOString();
    meta.rawSha256 = sha256("");
    meta.cleanSha256 = sha256("");
    if (options.saveReviewText) {
      writeFileSync(rawPath, "", "utf8");
      writeFileSync(cleanPath, "", "utf8");
    }
    writeJson(metaPath, meta);
    printArtifacts({ meta, promptPath, rawPath, cleanPath, metaPath, sessionId: null });
    return;
  }

  if (auth.mode === "missing") {
    console.error("Kiro auth is missing. Set KIRO_API_KEY in the environment, .env.kiro.local, or KIRO_ENV_FILE; or run kiro-cli login/profile so profile auth is available.");
    meta.status = 1;
    meta.reviewCoverage = "NotRun";
    meta.finishedAt = new Date().toISOString();
    meta.rawSha256 = sha256("");
    meta.cleanSha256 = sha256("");
    if (options.saveReviewText) {
      writeFileSync(rawPath, "", "utf8");
      writeFileSync(cleanPath, "", "utf8");
    }
    writeJson(metaPath, meta);
    process.exit(1);
  }

  const args = [
    "chat",
    "--no-interactive",
    "--model",
    options.model,
    `--trust-tools=${options.trustTools}`,
    prompt,
  ];
  if (options.resumeId) {
    args.splice(args.length - 1, 0, "--resume-id", options.resumeId);
  } else if (options.resume) {
    args.splice(args.length - 1, 0, "--resume");
  }

  const result = spawnSync("kiro-cli", args, {
    cwd,
    env,
    encoding: "utf8",
    timeout: options.timeoutMs,
    maxBuffer: 50 * 1024 * 1024,
  });

  const spawnError = serializeSpawnError(result.error);
  if (spawnError) {
    console.error(`kiro-cli execution issue: ${spawnError.message}`);
  }

  const errorBlock = spawnError
    ? `\n\n## Wrapper Spawn Error\n\n${spawnError.message}\n`
    : "";
  const raw = [result.stdout ?? "", result.stderr ?? "", errorBlock].filter(Boolean).join("\n");
  const clean = stripAnsi(raw);
  meta.rawSha256 = sha256(raw);
  meta.cleanSha256 = sha256(clean);
  if (options.saveReviewText) {
    writeFileSync(rawPath, raw, "utf8");
    writeFileSync(cleanPath, clean, "utf8");
  } else if (clean.trim()) {
    process.stdout.write(clean.endsWith("\n") ? clean : `${clean}\n`);
  }

  meta.spawned = !spawnError;
  meta.spawnError = spawnError;
  meta.timedOut = Boolean(result.error && result.error.code === "ETIMEDOUT");
  meta.terminatedBySignal = result.signal ?? null;
  meta.status = statusFromSpawnResult(result);
  meta.sessionId = result.error ? null : (extractSessionId(clean) ?? latestSessionId(env));
  meta.toolDenied = clean.includes("is rejected because it matches one or more rules on the denied list");
  meta.analysisGaps = buildAnalysisGaps({ toolDenied: meta.toolDenied, spawnError: meta.spawnError });
  meta.reviewCoverage = meta.analysisGaps.length > 0 ? "Reduced" : "Full";
  meta.reviewComplete = meta.status === 0 && clean.trim().length > 0 && looksLikeCompleteReview(clean);
  meta.finishedAt = new Date().toISOString();
  writeJson(metaPath, meta);

  if (meta.toolDenied) {
    console.warn("Review coverage reduced: Kiro reported denied tool access. See meta analysisGaps.");
  }

  printArtifacts({ meta, promptPath, rawPath, cleanPath, metaPath, sessionId: meta.sessionId });

  if (!meta.reviewComplete || meta.status !== 0) {
    process.exit(meta.status || 1);
  }
}

function printArtifacts({ meta, promptPath, rawPath, cleanPath, metaPath, sessionId }) {
  if (meta.saveReviewText) {
    console.log(`Prompt: ${path.relative(cwd, promptPath)}`);
    console.log(`Raw:    ${path.relative(cwd, rawPath)}`);
    console.log(`Clean:  ${path.relative(cwd, cleanPath)}`);
  } else {
    console.log("Prompt: not saved (use --save-review-text to persist)");
    console.log("Raw:    not saved (use --save-review-text to persist)");
    console.log("Clean:  not saved (use --save-review-text to persist)");
  }

  console.log(`Meta:   ${path.relative(cwd, metaPath)}`);
  console.log(`Hashes: prompt=${meta.promptSha256} raw=${meta.rawSha256 ?? ""} clean=${meta.cleanSha256 ?? ""}`);
  console.log(`Coverage: ${meta.reviewCoverage}`);
  console.log(`Session: ${sessionId ?? ""}`);
}

main();
