# TraceMap Product Requirements

## Purpose

TraceMap is a deterministic C# repository indexer and contract-change reducer. It helps reviewers answer a narrow question: "Given this contract delta and this indexed repository state, what evidence exists for code that may need review?"

TraceMap must not guess impact. It records facts with rule IDs, evidence tiers, file paths, line spans, commit SHA, and extractor versions, then reduces contract deltas against those facts.

## Problem

C# service and library repositories often contain DTOs, generated clients, HTTP integrations, database access, and legacy project formats. When a contract changes, reviewers need a fast way to find likely affected code without relying on broad text search or unverifiable AI summaries.

The hard parts are:

- Build and project load can fail, especially in legacy repositories.
- Syntax-only evidence is useful but not proof of symbol identity.
- Generic names such as `id`, `name`, `type`, and `status` can create noisy matches.
- A failed or partial scan must never be reported as clean.

## Goals

- Produce deterministic scan artifacts for C# repositories.
- Continue scanning when semantic analysis fails.
- Emit evidence-backed facts into `facts.ndjson` and `index.sqlite`.
- Emit human-readable scan and impact reports.
- Reduce contract deltas against indexed facts using documented classifications.
- Make partial coverage explicit in every no-evidence result.

## Non-Goals

- No LLM calls in the scanner or reducer.
- No embeddings, vector databases, or prompt-based classification.
- No claim that a repo is unaffected unless full semantic coverage supports that conclusion.
- No raw source snippets by default.
- No attempt to infer runtime-only behavior such as reflection, dynamic dispatch, DI container state, collection contents, branch reachability, or custom serializer aliases in the basic reducer; scanner facts may record statically visible runtime-adjacent evidence with explicit limitations.

## Users

- API owners checking a contract change before release.
- Platform maintainers reviewing service upgrade blast radius.
- Pull request reviewers who need evidence rows instead of prose guesses.
- Engineers working with legacy C# repos where full build success is not guaranteed.

## Core Workflows

### Scan

```bash
tracemap scan --repo <repo-path> --out <output-path>
```

Required outputs:

- `scan-manifest.json`
- `facts.ndjson`
- `index.sqlite`
- `report.md`
- `logs/analyzer.log`

### Reduce

```bash
tracemap reduce --index <index.sqlite> --contract-delta <contract-delta.json> --out <impact-report.md>
```

Required output:

- `impact-report.md`

### Flow

```bash
tracemap flow --index <index.sqlite> --symbol <symbol-or-fragment> --out <flow-report.md>
```

Required output:

- `flow-report.md`

### Contract Delta Shape

```json
{
  "contract": "CustomerProfileResponse",
  "source": "contracts/customer-profile-v2.json",
  "changes": [
    {
      "element": "CustomerProfileResponse.primaryEmail",
      "changeType": "removed",
      "oldType": "string",
      "newType": null
    }
  ]
}
```

## Evidence Model

Every fact must include:

- `factId`
- `scanId`
- `repo`
- `commitSha`
- `factType`
- `ruleId`
- `evidenceTier`
- source or target symbols when available
- file path and line span
- extractor identity and version

Evidence tiers:

- `Tier1Semantic`: compiler-resolved Roslyn symbol evidence.
- `Tier2Structural`: known framework, project, config, HTTP, DB, or DTO structure pattern.
- `Tier3SyntaxOrTextual`: syntax-only or string/textual evidence.
- `Tier4Unknown`: analysis gap or inability to prove/disprove.

## Reducer Classifications

- `DefiniteImpact`: compiler-resolved type evidence or compiler-resolved member usage matches the changed element.
- `ProbableImpact`: strong structural evidence matches the changed element, such as DTO, HTTP, DB, or config usage.
- `NeedsReview`: syntax-only or textual evidence matches, but symbol identity is not proven.
- `NoEvidenceFullCoverage`: no matches and the scan reports full semantic coverage at a known commit SHA.
- `NoEvidenceReducedCoverage`: no matches, but the scan reports reduced or syntax-only coverage.
- `UnknownAnalysisGap`: analysis-gap evidence names the changed element and prevents a credible conclusion.

## Current Product State

Milestones 0 through 8 establish the current MVP:

- CLI skeleton.
- repo manifest and file inventory.
- C# syntax fallback extractor.
- SQLite index.
- Roslyn semantic extractor.
- HTTP, DB, config extractors.
- Markdown scan report.
- Basic contract reducer.
- Call-flow facts and logic/boilerplate shape signals.

## Near-Term Roadmap

- Improve DTO and serialization evidence while keeping deterministic rule IDs and documented limitations.
- Add schema/version documentation for fact and report formats.
- Add machine-readable reducer output alongside Markdown reports.
- Expand repo-specific external smoke fixtures beyond the first two sample repositories.

## Success Criteria

- `dotnet build` and `dotnet test` pass.
- The CLI can scan at least one modern sample repo with full semantic coverage.
- The CLI can scan legacy or dependency-incomplete sample repos with reduced coverage and useful syntax facts.
- The reducer emits evidence rows for matches and coverage evidence for no-match classifications.
- The reducer emits warnings for generic or high fan-out name matches without suppressing evidence.
- The scanner emits queryable call edges, object creation facts, argument-flow facts, local-alias facts, field-alias facts, parameter-forwarding edges, flow-boundary facts, runtime-evidence facts, assembly identity, and review-routing logic shape facts.
- No reducer finding is emitted without a rule ID.
- No no-evidence result hides reduced coverage.
