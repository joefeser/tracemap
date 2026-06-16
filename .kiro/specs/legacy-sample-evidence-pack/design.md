# Legacy Sample Evidence Pack Design

## Overview

Legacy validation currently produces useful local scan outputs and several
nearby workflows already produce redacted summaries or reports. This spec adds a
separate evidence-pack layer that turns those existing artifacts into
shareable, deterministic proof material.

The pack generator is intentionally downstream of the scanner and reducer. It
does not decide whether code is impacted, safe, vulnerable, reachable, or used
in production. It summarizes static evidence that TraceMap already emitted,
preserving rule IDs, evidence tiers, coverage labels, extractor versions, gaps,
limitations, and safe provenance.

## Goals

- Give docs, site, demo, and review work a public-safe artifact to consume.
- Keep raw scans, raw reports, analyzer logs, private sample manifests, and
  local paths out of git.
- Preserve enough aggregate evidence to support honest public claims about what
  TraceMap observed in old or large codebases.
- Make claim levels explicit: `local-only`, `demo-safe`, `public-safe`, or
  `rejected`.
- Provide deterministic JSON plus generated Markdown from the same source
  model.
- Reuse existing redaction, safe path, stable hashing, and report rendering
  helpers wherever possible.

## Non-Goals

- No site implementation in this spec.
- No scanner extractor or reducer changes.
- No raw `facts.ndjson`, SQLite, scan manifests, logs, raw report prose, raw
  SQL, config values, source snippets, local paths, raw remotes, secrets, or
  private sample names in tracked pack artifacts.
- No runtime behavior, production usage, vulnerability status, release safety,
  service reachability, query execution, or business impact claims.
- No LLM calls, embeddings, vector databases, prompt-based classification, or
  probabilistic summary generation in TraceMap core.

## Relationship To Nearby Work

This spec should compose, not duplicate:

- `legacy-codebase-validation` supplies local-only validation summaries.
- `legacy-baseline-regression-artifacts` supplies redacted baseline snapshots
  and comparison movement.
- `legacy-build-environment-diagnostics`, `legacy-wcf-metadata-normalization`,
  `legacy-webforms-event-flow`, `legacy-data-metadata-extraction`, and
  `legacy-flow-composition-reporting` supply specialized evidence categories.
- `public-demo-workflow` and `release-review-report` already define safe
  generated report conventions.
- Site specs may later consume promoted pack files, but site page work stays out
  of this implementation slice.

An evidence pack is a curated redacted review packet over these artifacts. It is
not the source of truth. The source of truth remains TraceMap scan artifacts and
their redacted summaries.

## Storage Model

Local-only generation:

```text
.tmp/legacy-evidence-packs/<pack-id>/
  evidence-pack.json
  evidence-pack.md
  validation-result.json
```

Tracked public-safe promotion:

```text
docs/evidence-packs/legacy/<pack-id>/
  evidence-pack.json
  evidence-pack.md
```

The tracked location is a docs/tooling handoff location, not a site page. Site
work can copy, import, or render these packs in a later spec after reviewing
claim boundaries.

`packId` is derived deterministically from a neutral label, purpose, claim
level, schema version, and an explicit injected date such as `--date YYYY-MM` or
a fixture-pinned date. Public-safe and demo-safe pack generation fails if the
date is omitted. Create adds a deterministic content-derived safe suffix to
avoid collisions, but the suffix must not reveal private identity. Promote still
refuses to overwrite an existing destination unless `--force` is supplied.

The safe suffix is computed before pack JSON serialization from a normalized
input fingerprint, not from the final pack JSON. The fingerprint includes the
input kind, requested claim level, purpose, injected date, source
classification, source schema version, and a safe hash of the redacted input
summary or aggregate pre-pack model. It excludes `packId`, output path, local
raw input path, environment values, and any unsafe identity.

Raw inputs remain in ignored storage:

```text
.tmp/legacy-codebase-validation/
.tmp/legacy-baselines/
.tracemap-demo/
```

If implementation introduces a new local output root, it must add a generic
ignore rule and a test or validation step proving `git check-ignore` accepts it.
Any `git check-ignore` non-zero or error exit is a refusal. Promotion uses the
opposite rule: the destination must resolve under an approved tracked root such
as `docs/evidence-packs/legacy/` and must not be ignored. Promote runs
`git check-ignore` against the destination and treats a zero exit as refusal
because zero means the destination is ignored.

`validation-result.json` is local-only by default. It captures validator schema
version, validator version, checked file names, requested claim level, observed
classification, sanitized rejection categories, sanitized gap categories, exit
status, and the explicit injected or fixture-pinned date used for deterministic
validation. It must not echo unsafe values.

## Command Shape

Preferred CLI:

```bash
tracemap evidence-pack create \
  --input <scan-output|summary-json|baseline-manifest|demo-summary> \
  --input-kind <scan-output|legacy-validation-summary|legacy-baseline|public-demo-summary> \
  --label <neutral-label> \
  --purpose <legacy-validation-proof|baseline-comparison-proof|demo-review-proof> \
  --claim-level <local-only|demo-safe|public-safe> \
  --date <YYYY-MM> \
  --out <output-dir> \
  [--dry-run]

tracemap evidence-pack validate \
  --pack <evidence-pack.json> \
  --expected-claim-level <local-only|demo-safe|public-safe>

tracemap evidence-pack promote \
  --pack <evidence-pack.json> \
  --markdown <evidence-pack.md> \
  --out docs/evidence-packs/legacy/<pack-id> \
  [--force] \
  [--dry-run]
```

Script fallback:

```bash
scripts/evidence-pack.mjs create ...
scripts/evidence-pack.mjs validate ...
scripts/evidence-pack.mjs promote ...
```

A script fallback is acceptable only as a temporary implementation decision if
the CLI architecture blocks the preferred command shape. The implementation
state must record the blocker and the migration plan.

`promote` copies files; it does not move or delete local generated artifacts. It
errors when the destination already exists unless `--force` is passed, and it
rejects ignored destinations even when the pack itself validates.
Before copying files, `promote` reruns `tracemap evidence-pack validate`, the
pack safety validator or generated-output sentinel against the candidate JSON
and Markdown files, and `./scripts/check-private-paths.sh`; any failure aborts
the copy. Because `check-private-paths.sh` uses tracked-file search, promotion
must not rely on it alone for ignored source files or untracked destination
files. `--force` overrides only the destination-exists check, not validation,
sentinel, tracked root, or private-path gates.
The approved tracked-root allowlist is maintained as a hardcoded constant in the
evidence-pack implementation, initially only `docs/evidence-packs/legacy/`.
Adding a new root requires a code change and tests.

Enum option values such as `claim-level`, `purpose`, and `input-kind` may be
recorded in command provenance. Free-text or path-bearing options such as
`input`, `out`, `pack`, and `markdown` are recorded name-only or category-only.
Public-safe provenance omits the raw `--label` value entirely or records a fixed
placeholder such as `[label]`; it must not record the operator-supplied label.

## Input Kinds

| `--input-kind` | Upstream owner | Initial support expectation | Notes |
| --- | --- | --- | --- |
| `scan-output` | `tracemap scan` artifacts | supported via aggregate reader | Raw inputs must be ignored and are never copied. |
| `legacy-validation-summary` | `legacy-codebase-validation` | supported first | Preferred synthetic fixture shape for v1 tests. |
| `legacy-baseline` | `legacy-baseline-regression-artifacts` | supported or explicitly deferred | Reader validates baseline safety classification before use. |
| `public-demo-summary` | `public-demo-workflow` | supported or explicitly deferred | Reader accepts public-shareable summary fields only. |

If an input kind is deferred in the first implementation PR, the command must
emit an unavailable-schema gap rather than silently skipping the source.

## Artifact Schema

Suggested JSON shape:

```json
{
  "schemaVersion": "legacy-evidence-pack.v1",
  "packId": "legacy-alpha__legacy-validation-proof__public-safe__2026-06__c9d4a1b2",
  "packPurpose": "legacy-validation-proof",
  "claimLevel": "public-safe",
  "generatedAt": "2026-06",
  "sourceLabels": [
    {
      "label": "legacy-alpha",
      "classification": "public-safe",
      "identityKind": "neutral-label",
      "commitIdentity": {
        "kind": "redacted-sha256",
        "value": "sha256:c9d4a1b2...",
        "shaPresent": true
      }
    }
  ],
  "sourceClassifications": ["public-safe"],
  "commandProvenance": {
    "traceMapVersion": "x.y.z",
    "packGeneratorVersion": "legacy-evidence-pack.v1",
    "normalizedCommand": "tracemap evidence-pack create",
    "inputKind": "legacy-validation-summary",
    "inputClassifications": ["public-safe"],
    "options": [
      { "name": "claim-level", "value": "public-safe" },
      { "name": "purpose", "value": "legacy-validation-proof" }
    ],
    "validationCommands": [
      "tracemap evidence-pack validate",
      "./scripts/check-private-paths.sh",
      "git diff --check"
    ]
  },
  "coverage": {
    "overallLabel": "Level1SemanticAnalysisReduced",
    "partial": true,
    "buildStatus": "FailedOrPartial",
    "scanStatus": "completed-with-gaps"
  },
  "extractorVersions": [
    { "extractor": "csharp.semantic", "version": "..." }
  ],
  "summary": {
    "factsTotal": 0,
    "gapsTotal": 0,
    "rulesObserved": 0,
    "sectionsAvailable": 0,
    "sectionsReduced": 0
  },
  "evidenceSections": [
    {
      "sectionId": "webforms-event-flow",
      "title": "WebForms Event Flow",
      "status": "available",
      "claimBoundary": "static-evidence-only",
      "ruleIds": ["legacy.webforms.event-binding.v1"],
      "evidenceTiers": ["Tier2Structural", "Tier3SyntaxOrTextual"],
      "coverageLabel": "Level1SemanticAnalysisReduced",
      "counts": [
        { "name": "bindingsObserved", "value": 0, "ruleId": "legacy.webforms.event-binding.v1" }
      ],
      "limitations": [
        {
          "limitationId": "runtime-not-proven",
          "ruleId": "legacy.evidence-pack.claim-boundary.v1",
          "evidenceTier": "Tier4Unknown",
          "message": "Static event evidence does not prove runtime execution."
        }
      ]
    }
  ],
  "gaps": [],
  "limitations": [],
  "safety": {
    "classification": "public-safe",
    "redactionProfile": "counts-and-safe-labels",
    "validatorVersion": "legacy-evidence-pack-safety.v1",
    "rejectedReasons": []
  }
}
```

The exact implementation model can evolve, but these concepts should remain
additive once emitted publicly.

`generatedAt` is not wall-clock time. It is the explicit injected or
fixture-pinned `YYYY-MM` value used for public-safe and demo-safe determinism.
Local-only artifacts may record fuller local timestamps only if they remain
local-only and are excluded from byte-stability expectations.

Top-level `summary` is a pack-level aggregate backed by
`legacy.evidence-pack.summary.v1`; individual scalar fields such as
`factsTotal` do not need their own rule IDs. Evidence section rows, gaps,
limitations, and per-section counts do require rule IDs and evidence tiers.

## Proposed Rule IDs

The implementation PR must add catalog entries before emitting these rule IDs.
The spec defines their intended scope and minimum limitation so reviewers can
evaluate evidence boundaries early.

| Rule ID | Scope | Minimum limitation |
| --- | --- | --- |
| `legacy.evidence-pack.summary.v1` | Pack-level aggregate counts, source classifications, and high-level rollups. | Aggregates summarize static evidence only and may hide redacted detail. |
| `legacy.evidence-pack.section.v1` | Section availability, counts, and safe per-section rows. | Section counts do not prove runtime behavior or complete coverage. |
| `legacy.evidence-pack.claim-boundary.v1` | Claim boundary notes and prohibited-claim downgrades. | Boundary notes describe what TraceMap cannot prove from static evidence. |
| `legacy.evidence-pack.safety-validation.v1` | Safety classification, rejection reasons, and validation gaps. | Validator categories are conservative and may reject ambiguous content. |
| `legacy.evidence-pack.command-provenance.v1` | Sanitized command, tool, schema, and extractor provenance. | Provenance omits raw paths, environment values, and private identity. |
| `legacy.evidence-pack.input-availability.v1` | Missing, deferred, incompatible, or unsupported input schemas. | Missing input support is an analysis gap, not evidence of absence. |

## Section Status Vocabulary

Use a closed vocabulary:

| Status | Meaning |
| --- | --- |
| `available` | Section has validated static evidence or explicit zero counts under available coverage. |
| `not_requested` | Input or command scope did not request this section. |
| `unavailable` | Required source schema, extractor, or summary is absent. |
| `deferred` | Known future workflow is outside the current pack generation scope. |
| `reduced` | Section exists but coverage/build/schema limitations constrain interpretation. |
| `truncated` | Input was bounded by time, size, row, or section limits. |
| `rejected` | Section was excluded from shareable output by safety validation. |

Unknown or missing evidence must not be represented as clean zero unless the
section was in scope and coverage supports that zero.

Any section with status `rejected` forces the top-level pack safety
classification to `rejected`. Public-safe and demo-safe packs may omit rejected
source material only when they also emit a rule-backed omission gap and do not
include the rejected section.

## Claim Boundary Vocabulary

Use conservative wording in JSON and Markdown:

- `static-evidence-only`
- `aggregate-counts-only`
- `coverage-relative`
- `review-needed`
- `analysis-gap`
- `redacted-summary`

Forbidden meanings:

- runtime execution or branch feasibility
- service reachability or deployment state
- SQL execution or database existence
- vulnerability, license, or compatibility status
- production usage, traffic, or user behavior
- business impact or release approval
- contract impact in v1 evidence packs

Future packs may include reducer outputs only after a separate spec defines the
schema field and requires preserving original reducer rule IDs and limitations
verbatim.

## Prohibited Claim Detection

The safety validator owns the generated-output sentinel for evidence packs. It
checks every string-valued leaf in pack JSON and every generated Markdown
paragraph, heading, table cell, and list item using a reviewed
case-insensitive phrase list. The phrase list should live next to the validator,
for example in `EvidencePackSafetyValidator`, and tests must prove nested fields
such as `limitations[].message` are scanned.
The phrase list is versioned as part of `safety.validatorVersion`; any phrase
list change updates that validator version and forces tests to review expected
safe and unsafe phrases.

Initial prohibited phrase categories:

| Category | Example patterns |
| --- | --- |
| Runtime execution | `executed`, `ran at runtime`, `user clicked`, `request reached` |
| Service reachability | `reachable service`, `service is live`, `endpoint is available` |
| SQL/database execution | `query executed`, `database contains`, `table exists in production` |
| Security posture | `vulnerable`, `not vulnerable`, `safe from`, `exploit` |
| Production usage | `production traffic`, `users depend on`, `used in production` |
| Release approval | `safe to release`, `approved`, `merge gate passed` |
| Business impact | `business critical`, `revenue impact`, `customer impact` |
| Unsupported contract impact | `impacted contract`, `breaking change impact` without reducer provenance |

The validator should prefer category-oriented checks with a small explicit
allowlist for TraceMap's safe phrases, such as "not runtime proof" or "does not
prove production usage".

## Safety Classification

`local-only`:

- May include local-only hashes, private commit categories, or operator-only
  grouping metadata.
- Must remain under ignored storage.
- Must not be promoted or consumed by public site work.

`demo-safe`:

- Uses checked-in public fixtures, public demo samples, or reviewed synthetic
  summaries.
- May be used for local demos and review, but still needs approval before public
  site/docs claims.
- Must not hash low-entropy or enumerable private identities.

`public-safe`:

- Contains only neutral labels, approved public identifiers, safe counts, rule
  IDs, evidence tiers, coverage labels, extractor versions, limitations, and
  safe command provenance.
- May be promoted to tracked docs after validation and review.

`rejected`:

- Contains unsafe data, unsupported source schemas, overstrong claim wording, or
  unverifiable provenance.
- Must not be promoted.

## Redaction Rules

Reject or omit:

- local absolute paths and home-directory fragments
- raw repository remotes, private repo names, private organization names,
  usernames, machine names, and private hostnames
- raw SQL, DBML/EDMX/XSD/XML snippets, config values, connection strings,
  endpoint URLs, SOAP actions, WSDL addresses, bearer tokens, credentials, API
  keys, certificates, and private keys
- source snippets, analyzer log lines, stack traces with local paths, and raw
  compiler diagnostics that include paths or source text
- unescaped Markdown metacharacters in user-controlled display fields

Hash only values that are safe to hash. Public-safe packs should avoid hashing
low-entropy private identities because those can be guessed; the same
prohibition applies to demo-safe packs. When hashes are allowed, use SHA-256
with context-separated, length-prefixed input components. If implementation
extracts a shared TraceMap stable-hash helper, it must preserve those same input
separation rules.

Secret-like values include at least:

- key-value pairs whose key contains `password`, `passwd`, `pwd`, `secret`,
  `token`, `apiKey`, `apikey`, `credential`, `connectionString`,
  `clientSecret`, or `privateKey`
- `Bearer` or `Basic` authorization fragments
- JWT-shaped strings with three base64url segments
- private key or certificate block markers
- connection string fragments such as `Server=`, `Data Source=`, `User ID=`,
  `Uid=`, `Password=`, `Initial Catalog=`, or provider-specific credential
  keys
- long base64-like or hex-like tokens above a conservative length threshold
  when the surrounding key or context is credential-like

Low-entropy private identities include private repository names, organization
names, usernames, machine names, short project codes, branch names, and private
hostnames. Public-safe and demo-safe packs omit or category-only represent them.

`tracemap evidence-pack validate --expected-claim-level <level>` fails whenever
the observed classification is not exactly the requested level. A lower observed
classification such as `demo-safe` when `public-safe` was requested is a
validation failure, not a warning.

## Shared Safety Component

The implementation should avoid creating another isolated redaction engine. It
should either reuse existing report/path/portfolio safe display helpers or
extract a small shared safety component used by evidence packs and future
redacted summary workflows. Any shared extraction must preserve existing report
behavior unless tests are updated intentionally.

## Synthetic Fixture

The first implementation PR should create this fixture; it is intentionally not
committed by the spec-only branch. It should live under:

```text
samples/synthetic-legacy-evidence-pack/
  legacy-validation-summary.json
  README.md
```

The JSON fixture should be public-safe and contain:

- neutral label `synthetic-legacy-alpha`
- fixture-pinned date such as `2026-06`
- partial or reduced coverage label
- build status and scan status
- two extractor version entries
- at least two rule IDs, two evidence tiers, two fact categories, one gap, and
  one limitation
- safe counts for WebForms, WCF/service-reference, SQL/query, build diagnostic,
  and flow sections where practical
- no paths, remotes, snippets, raw SQL, config values, endpoint addresses,
  secrets, or private identities

## Implementation Layout

Likely locations:

```text
src/dotnet/
  TraceMap.Reporting/
    EvidencePackModels.cs
    EvidencePackGenerator.cs
    EvidencePackSafetyValidator.cs
    EvidencePackMarkdownWriter.cs
  TraceMap.Cli/
    Program.cs
  tests/TraceMap.Tests/
    EvidencePackTests.cs

docs/evidence-packs/legacy/
  README.md

samples/
  synthetic-legacy-evidence-pack/
```

If implementation chooses a script-first path, keep helpers under `scripts/`
with focused unit tests and record the CLI deferral in `implementation-state.md`.

The `docs/evidence-packs/legacy/README.md` promotion-root placeholder is also an
implementation PR deliverable, not a spec-branch artifact.

## Validation Strategy

Spec delivery validation:

```bash
node scripts/kiro-review.mjs --self-test
./scripts/check-private-paths.sh
git diff --check
```

Implementation validation:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
tracemap evidence-pack create --input samples/synthetic-legacy-evidence-pack --input-kind legacy-validation-summary --label synthetic-legacy-alpha --purpose legacy-validation-proof --claim-level public-safe --date 2026-06 --out .tmp/legacy-evidence-packs/synthetic-legacy-alpha --dry-run
tracemap evidence-pack create --input samples/synthetic-legacy-evidence-pack --input-kind legacy-validation-summary --label synthetic-legacy-alpha --purpose legacy-validation-proof --claim-level public-safe --date 2026-06 --out .tmp/legacy-evidence-packs/synthetic-legacy-alpha
tracemap evidence-pack validate --pack .tmp/legacy-evidence-packs/synthetic-legacy-alpha/evidence-pack.json --expected-claim-level public-safe
git check-ignore .tmp/legacy-evidence-packs/synthetic-legacy-alpha/evidence-pack.json
./scripts/check-private-paths.sh
git diff --check
```

Run additional pinned smokes from `docs/VALIDATION.md` when implementation
touches shared report rendering, scanner output, public demo behavior, release
review behavior, or language adapters.
