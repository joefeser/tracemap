# TraceMap Decision Log

This log records product and engineering decisions that affect how TraceMap interprets evidence. Keep entries short, dated, and tied to observable behavior.

## 2026-06-12: TraceMap is deterministic, not AI-driven

Decision: scanner and reducer logic must not call LLMs, embeddings, vector databases, or prompt-based classifiers.

Why: TraceMap findings need to be reproducible, testable, and grounded in rule IDs and file/line evidence.

Consequence: AI may summarize or navigate existing facts outside the core scanner/reducer, but it must not invent findings or classifications.

## 2026-06-12: Reduced coverage is useful but never clean

Decision: failed builds, project load failures, compilation errors, and syntax-only scans can still produce facts, but no-match reducer findings must use `NoEvidenceReducedCoverage`.

Why: legacy and dependency-incomplete repos still need useful analysis, but absence of evidence is not evidence of absence when coverage is partial.

Consequence: `NoEvidenceFullCoverage` requires full semantic analysis, successful build status, a known commit SHA, and no known gaps.

## 2026-06-12: Basic reducer uses deterministic name matching

Decision: Milestone 7 matches changed type, property, and field names against indexed fact candidates using normalized deterministic name comparison.

Why: this gives an explainable MVP that works across semantic, structural, syntax, and textual facts.

Consequence: generic names such as `status`, `id`, `name`, and `type` can produce noisy but evidence-backed matches in large repositories.

## 2026-06-12: Generic match noise should be reported, not hidden

Decision: reducer reports add warnings or match fan-out summaries for generic contract element names instead of suppressing matches silently.

Why: hiding deterministic matches would make the reducer less auditable. Warnings preserve evidence while signaling review risk.

Consequence: acceptance fixtures include generic-name examples so future rule tuning is deliberate.

## 2026-06-12: External sample repos are opt-in smoke fixtures

Decision: repositories under a developer-provided `<external-csharp-sample-repos>` path should be used for manual or scripted smoke tests, not required unit tests.

Why: they are larger, machine-local, and may depend on SDKs or packages unavailable in every environment.

Consequence: normal `dotnet test` remains fast and portable. `scripts/smoke-sample-repos.sh` provides broader confidence when the fixture folder exists.

## 2026-06-12: Rule catalog limitations must track behavior

Decision: any reducer or extractor behavior change that affects evidence meaning must update `rules/rule-catalog.yml`.

Why: "No rule without documented limitations" is a project invariant.

Consequence: behavior changes are incomplete until rule limitations and tests are updated.

## 2026-06-12: Endpoint alignment compares existing indexes

Decision: the endpoint alignment MVP reads one client index and one server index and emits derived Markdown/JSON reports, rather than requiring a combined multi-language database.

Why: separate scans preserve language ownership, keep evidence provenance intact, and let nested client/server apps be compared without a monolithic scanner.

Consequence: `tracemap combine`, N-way endpoint matching, and endpoint diffing across commit SHAs remain backlog work. Endpoint matches are derived report rows, not source facts.

## 2026-06-13: Combine before broad JVM work

Decision: implement `tracemap combine` before or alongside Java/Kotlin support.

Why: cross-repo and cross-language dependency analysis needs a shared source-index model, namespaced facts, and derived rows. Building JVM first without combine would push multi-index behavior into language-specific code.

Consequence: JVM planning should assume combined-index provenance from the start.

## 2026-06-13: JVM language family layout

Decision: plan Java/Kotlin under `src/jvm` unless implementation discovery proves separate top-level language roots are necessary.

Why: Java and Kotlin share Gradle/Maven metadata, package/module identity, JVM descriptors, bytecode signatures, dependency facts, inheritance relationships, and many downstream query concepts.

Consequence: language-specific parser/compiler code can live under subfolders, but shared JVM contracts should stay together.
