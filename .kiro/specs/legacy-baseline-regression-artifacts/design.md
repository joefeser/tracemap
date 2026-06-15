# Legacy Baseline Regression Artifacts Design

## Overview

This spec adds a deterministic baseline and comparison layer around TraceMap
scan artifacts. The scanner already emits raw artifacts such as
`scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
`logs/analyzer.log`. Those raw outputs are useful locally but are too detailed
for committed legacy baselines because they can contain local paths, raw
diagnostics, file identities, or private sample context.

The proposed workflow creates a redacted baseline summary from an existing scan
output directory, validates the summary for safety, and optionally promotes only
the public-safe summary into tracked repository files. Future scanner changes can
then compare a candidate redacted summary against the original parser snapshot.

The comparison reports movement in static evidence counts and coverage labels.
It does not produce reducer conclusions and does not say code is impacted.

## Goals

- Preserve original parser snapshots long enough to judge whether additional
  legacy enhancements are still worth implementing.
- Keep committed baselines safe by default: neutral sample labels, counts,
  hashes where appropriate, rule IDs, evidence tiers, extractor versions,
  coverage labels, limitations, and known gaps.
- Support local-only baselines for private samples when useful details cannot be
  safely committed.
- Provide deterministic JSON and Markdown comparison output.
- Make safety classification explicit: `public-safe`, `local-only`, or
  `rejected`.

## Non-Goals

- No storage of raw scan outputs in tracked baseline files.
- No scanner extractor changes in this spec.
- No runtime reachability, production usage, security, business impact, or
  reducer conclusion claims.
- No LLM calls, embeddings, vector databases, prompt-based classification, or
  model inference in TraceMap core.
- No site work or public marketing copy.

## Current State

Related existing work:

- Legacy validation already reads local sample inputs from ignored `.tmp/`
  locations and emits redacted summary candidates.
- Existing specs use neutral labels for local legacy samples.
- `scripts/check-private-paths.sh` guards tracked files against known private
  path and identity leaks.
- TraceMap scan artifacts already contain coverage labels, build status, repo
  identity, commit SHA, scanner versions, facts, rule IDs, evidence tiers, and
  analyzer gaps.
- A WCF/service-reference spec contains a one-off current parser snapshot, but
  there is no reusable baseline manifest or comparison workflow.

The missing piece is a repeatable, schema-backed baseline artifact that future
legacy improvements can compare against without copying raw artifacts into git.

## Artifact Model

Introduce a redacted baseline schema. Generated tracked public-safe baselines
should live under:

```text
.kiro/baselines/legacy/<baseline-id>/baseline-manifest.json
.kiro/baselines/legacy/<baseline-id>/baseline-summary.md
```

Local-only baselines must stay under the ignored directory:

```text
.tmp/legacy-baselines/<baseline-id>/
```

Suggested public-safe files:

- `baseline-manifest.json`: machine-readable sanitized baseline.
- `baseline-summary.md`: human-readable sanitized baseline.
- `comparison.json`: machine-readable comparison between baseline and candidate
  redacted summaries.
- `comparison.md`: human-readable comparison report.

Raw `facts.ndjson`, `index.sqlite`, `report.md`, analyzer logs, raw file lists,
raw paths, and raw scan directories must remain local-only and ignored.

## Baseline Manifest Shape

Suggested top-level shape:

```json
{
  "schemaVersion": "legacy-baseline-manifest.v1",
  "baselineId": "legacy-sample-alpha__original-parser__2026-06",
  "baselinePurpose": "original-parser-snapshot",
  "sample": {
    "label": "legacy-sample-alpha",
    "identityKind": "neutral-label",
    "repoIdentityHash": null,
    "commitIdentity": {
      "kind": "hash",
      "value": "sha256:..."
    }
  },
  "safety": {
    "classification": "public-safe",
    "redactionProfile": "counts-only",
    "rejectedReasons": [],
    "limitations": []
  },
  "scan": {
    "traceMapVersion": "x.y.z",
    "scanStartedAt": "2026-06",
    "coverageLabel": "Level1SemanticAnalysisReduced",
    "buildStatus": "FailedOrPartial",
    "scanStatus": "completed",
    "partial": true,
    "truncated": false
  },
  "extractors": {
    "csharp.semantic": {
      "version": "..."
    }
  },
  "counts": {
    "factsTotal": 0,
    "gapsTotal": 0,
    "byFactType": {},
    "byRuleId": {},
    "byEvidenceTier": {},
    "byExtractor": {},
    "bySurface": {}
  },
  "coverage": {
    "semanticFacts": 0,
    "structuralFacts": 0,
    "syntaxOrTextualFacts": 0,
    "unknownOrGapFacts": 0
  },
  "knownGaps": [],
  "limitations": []
}
```

Top-level `limitations` describe scanner, coverage, schema, and comparability
limits. `safety.limitations` describes redaction, local-only, rejection, and
promotion limits.

For private sources, `repoIdentityHash` is omitted or `null` in public-safe
manifests because repository names and remotes are often enumerable. It may be
present only in local-only manifests or in public-safe manifests for explicitly
public repositories where the source identity is already safe to disclose. Commit
identity follows the same rule: public commit SHAs may be stored as SHAs; private
commit identifiers are omitted, category-only, or local-only hashes.

Suggested identity kinds:

| `identityKind` | Allowed classification | Meaning |
| --- | --- | --- |
| `neutral-label` | `public-safe`, `local-only` | Uses only the operator-supplied neutral sample label. |
| `public-repo-sha` | `public-safe`, `local-only` | Public repository identity and commit SHA are safe to disclose. |
| `private-category-only` | `public-safe` | Private source identity exists but is omitted from committed artifacts. |
| `local-only-hash` | `local-only` | Private identity hash is useful locally but not publishable. |

Local-only manifests with `identityKind: neutral-label` may also carry a
`repoIdentityHash` when useful for local comparison, provided the artifact stays
under `.tmp/legacy-baselines/` and remains classified `local-only`.

Public-safe `scanStartedAt` and creation metadata use year-month precision or a
fixture-pinned value. Full timestamps are allowed only in local-only artifacts.
Determinism tests must inject the clock or compare byte-identical outputs using
a fixture-pinned time value.

The exact schema can evolve during implementation, but it must preserve these
concepts and be additive where possible.

## Safety Classification

Use three classifications:

| Classification | Meaning |
| --- | --- |
| `public-safe` | Contains only neutral labels, safe hashes, counts, rule IDs, evidence tiers, extractor versions, coverage labels, known gap categories, and limitations. |
| `local-only` | Useful for local comparison but contains details such as path hashes, private grouping, or private commit identity that should stay under ignored `.tmp/` storage. |
| `rejected` | Contains unsafe raw values or cannot be validated for committed storage. |

Public-safe is not automatic. Promotion from local output to tracked files must
run the safety validator and `scripts/check-private-paths.sh`.

## Redaction Rules

Never include in tracked baselines:

- local absolute paths or home-directory fragments
- private sample names, repository names, organization names, or usernames
- raw remotes or URLs
- source snippets or source-like fragments
- raw SQL
- config values, connection strings, endpoint addresses, package source values
- secrets, credentials, tokens, certificates, or keys
- raw analyzer output or raw native tool diagnostics

Allowed public-safe data:

- neutral labels such as `legacy-sample-alpha`
- stable counts
- rule IDs and documented limitations
- evidence tiers
- extractor IDs and versions
- coverage labels and build status labels
- known gap codes
- safe hashes for repo or commit identity only when the source identity is
  public or otherwise safe to hash and the hash input is context-separated
- date-only or explicit baseline version metadata if it does not identify a
  private sample

Secret-like values are values that match credential or sensitive-data patterns,
including connection strings, tokens, credentials, passwords, private keys,
certificates, bearer strings, API keys, package source credentials, endpoint
credentials, and config keys or values containing terms such as `password`,
`passwd`, `pwd`, `secret`, `token`, `key`, `credential`, or `connectionString`.
Secret-like values should be omitted or represented category-only. They should
not be hashed.

Low-entropy or enumerable private identities, such as private repository names,
organization names, usernames, short project codes, raw branch names, and
private hostnames, should also be omitted or category-only in public-safe
manifests. Hashing those values is allowed only in local-only manifests when the
workflow marks the artifact local-only.

## Hashing Guidance

Redaction hashes should use the existing TraceMap stable hash helper where
possible. If a baseline-specific helper is needed, use SHA-256 over
deterministic UTF-8 input with a fixed lowercase hex representation and
unambiguous structured input. Components must be length-prefixed, escaped, or
encoded before concatenation. A length-prefixed form is preferred:

```text
legacy-baseline
field:<len>:<field-name>
label:<len>:<neutral-label>
value:<len>:<raw-value>
```

Hashing is for stable comparison and redaction only. A hash is not evidence that
two private samples are public-identifiable, and a hash collision should be
treated as indistinguishable redaction metadata rather than stronger evidence.

## Count Extraction

The baseline generator reads scan artifacts locally and emits only aggregates.

Inputs:

- `scan-manifest.json` for scanner version, coverage label, build status, repo
  and commit identity, known gaps, extractor versions, scan status, and artifact
  existence.
- `facts.ndjson` for fact type, rule ID, evidence tier, extractor, and gap
  counts.
- Optional SQLite reads from `index.sqlite` only when needed to verify aggregate
  counts. SQLite queries must be restricted to `COUNT` or grouped aggregate
  queries over safe columns such as fact type, rule ID, evidence tier,
  extractor ID, extractor version, or gap code. Do not `SELECT` path, symbol,
  member, raw property, source text, snippet, SQL, config, message, or log
  columns when building baseline manifests.
- Optional `report.md` presence/size bucket only; do not parse or preserve raw
  report prose for committed baselines.
- Optional analyzer log presence/size bucket only; do not preserve raw log
  content for committed baselines.

Surface group examples:

| Surface | Example fact/rule families |
| --- | --- |
| `csharp` | method declarations, call edges, semantic gaps |
| `ui-events` | WinForms or WebForms event evidence |
| `http` | route and client evidence |
| `wcf-service-reference` | WCF config, generated clients, contracts, hosts, mappings |
| `sql-data` | SQL files, query text hashes, database or ORM metadata |
| `config` | config declarations and safe config categories |
| `packages` | package metadata and restore categories |
| `build-environment` | target framework, toolset, project-format, restore, workspace diagnostics |

Surface grouping must be table-driven and tested. Unknown fact types should fall
into `other` with rule IDs preserved rather than being dropped.

## Comparison Model

A comparison takes two redacted summaries. The candidate manifest is produced by
the same baseline create workflow against a newer scan output, so both sides
share the same safety validator and schema rules:

```text
baseline summary + candidate summary -> comparison report
```

Comparison dimensions:

- total fact and gap count
- counts by fact type
- counts by rule ID
- counts by evidence tier
- counts by extractor ID/version
- counts by supported surface
- coverage label and build status movement
- known gap category movement
- schema version and migration map status

Movement labels:

| Label | Meaning |
| --- | --- |
| `increase` | Candidate count is greater than baseline. |
| `decrease` | Candidate count is lower than baseline. |
| `unchanged` | Counts are equal. |
| `new-category` | Candidate contains a category absent from baseline. |
| `removed-category` | Baseline category is absent from candidate. |
| `coverage-changed` | Coverage or build status label changed. |
| `not-comparable` | Schema or extractor change needs a migration map. |
| `review-needed` | Decrease, removed category, rejected safety state, or unmapped schema change needs human review. |

Reports should say "additional static evidence" or "changed evidence count".
They must not say "new impact", "safe", "unsafe", "reachable", or "used by the
business" unless a separate reducer feature with evidence supports that claim.

Suggested comparison shape:

```json
{
  "schemaVersion": "legacy-baseline-comparison.v1",
  "baselineId": "legacy-sample-alpha__original-parser__2026-06",
  "candidateId": "legacy-sample-alpha__candidate__2026-07",
  "generatedAt": "2026-07",
  "overallStatus": "review-needed",
  "schemaCompatibility": {
    "status": "comparable",
    "migrationMap": null
  },
  "dimensions": {
    "byRuleId": [],
    "byFactType": [],
    "byEvidenceTier": [],
    "byExtractor": [],
    "bySurface": [],
    "coverage": []
  },
  "reviewNeeded": [],
  "limitations": []
}
```

## Migration Maps

Schema or rule/fact rename comparisons require an explicit migration map. The
first implementation should support a small JSON file alongside the comparison
input:

```json
{
  "schemaVersion": "legacy-baseline-migration-map.v1",
  "fromBaselineSchema": "legacy-baseline-manifest.v1",
  "toCandidateSchema": "legacy-baseline-manifest.v1",
  "ruleIdRenames": [
    {
      "fromRuleId": "old.rule.v1",
      "toRuleId": "new.rule.v1",
      "reason": "renamed without semantic change"
    }
  ],
  "factTypeRenames": [],
  "limitations": []
}
```

Missing or incomplete migration maps make the affected comparison dimension
`not-comparable` and `review-needed`.

## Rule Catalog and Limitations

If implementation introduces new rule IDs for baseline generation or comparison
facts, add them to `rules/rule-catalog.yml` before emitting them.

Suggested rule IDs if the implementation emits facts about baseline work:

- `legacy.baseline.redacted-manifest.v1`
- `legacy.baseline.coverage-snapshot.v1`
- `legacy.baseline.regression-comparison.v1`
- `legacy.baseline.safety-validation.v1`

These IDs are not pre-committed by this spec-only branch. Implementation task 1
must add the selected IDs to `rules/rule-catalog.yml` with limitations before
any baseline workflow emits them.

Each rule must document limitations, including:

- summaries are counts-only and omit raw artifacts;
- comparisons measure static evidence movement, not business impact;
- public-safe classification depends on validator coverage and reviewer
  judgment;
- local-only baselines may be useful but are not publishable.

## CLI Shape

Implement this workflow in `TraceMap.Cli` unless implementation discovers a
blocking CLI architecture issue and records the fallback in
`implementation-state.md`. Suggested CLI shape:

```text
tracemap baseline create \
  --scan-output <ignored-local-scan-output> \
  --label legacy-sample-alpha \
  --purpose original-parser-snapshot \
  --out .tmp/legacy-baselines/legacy-sample-alpha

tracemap baseline validate \
  --manifest .tmp/legacy-baselines/legacy-sample-alpha/baseline-manifest.json

tracemap baseline compare \
  --baseline .kiro/baselines/legacy/legacy-sample-alpha/baseline-manifest.json \
  --candidate .tmp/legacy-baselines/legacy-sample-alpha-candidate/baseline-manifest.json \
  --out .tmp/legacy-baselines/comparisons/legacy-sample-alpha
```

`baseline compare` does not promote output into tracked storage. Promotion is a
separate explicit step that must run `baseline validate`, the broader redaction
validator, and `scripts/check-private-paths.sh` over the promoted files.
The first implementation may document promotion as manual `validate` plus safety
checks; a dedicated `baseline promote` command is optional unless manual steps
prove error-prone.

Baseline labels should be short neutral slugs. Reject labels containing path
separators (`/` or `\`), URI schemes such as `://`, `@`-prefixed identifiers,
`.git` suffixes, home-directory fragments such as `~`, Windows drive patterns
such as `C:\`, raw hostnames, or organization/user identifiers.

The exact command location can be adjusted only if current CLI architecture
blocks this shape. If a script is implemented first, it should produce the same
schema and safety classification expected of the CLI command and record the
deviation in `implementation-state.md`.

Concrete synthetic fixture smoke shape for implementation validation:

```text
tracemap baseline create \
  --scan-output samples/synthetic-legacy-scan \
  --label synthetic-alpha \
  --purpose original-parser-snapshot \
  --out .tmp/legacy-baselines/synthetic-alpha \
  --dry-run

tracemap baseline create \
  --scan-output samples/synthetic-legacy-scan \
  --label synthetic-alpha \
  --purpose original-parser-snapshot \
  --out .tmp/legacy-baselines/synthetic-alpha

tracemap baseline compare \
  --baseline .tmp/legacy-baselines/synthetic-alpha/baseline-manifest.json \
  --candidate .tmp/legacy-baselines/synthetic-alpha-candidate/baseline-manifest.json \
  --out .tmp/legacy-baselines/comparisons/synthetic-alpha
```

## Validation Strategy

Tests should use synthetic fixtures that mimic scan artifacts:

- successful semantic scan summary
- failed or partial build with syntax fallback
- truncated large sample
- new rule/fact category in candidate
- removed category in candidate
- unsafe path, remote, SQL, config value, connection string, and secret-like
  content rejected by validator
- public-safe manifest promotion path
- local-only manifest path
- schema mismatch requiring migration map

Implementation validation should include:

- unit tests for schema serialization, deterministic sorting, hashing, safety
  classification, and comparisons
- CLI/script smoke against a checked-in synthetic scan-output fixture
- `dotnet test src/dotnet/TraceMap.sln`
- `./scripts/check-private-paths.sh`
- `git diff --check`
- relevant smoke checks from `docs/VALIDATION.md` when scan, report, or adapter
  behavior changes, or explicit deferral in implementation state
- `git check-ignore .tmp/legacy-baselines/example`

## Open Implementation Choices

- Whether a support script is also useful after the CLI shape exists.
- Whether local-only manifests include path hashes in the first implementation
  slice. Default to no path hashes unless implementation evidence shows counts
  are insufficient.
- Whether comparison output should support allowlists beyond the migration-map
  file in the first implementation slice. Default to migration maps only.

These choices should be resolved before implementation begins and recorded in
`implementation-state.md`.
