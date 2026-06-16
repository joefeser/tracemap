# Legacy Sample Evidence Pack Requirements

## Introduction

TraceMap is being validated against old and large .NET repositories so the team
can understand what the scanner actually finds in legacy code. Those local scan
outputs are useful evidence, but they can contain local paths, raw remotes,
private sample names, raw SQL, config values, endpoint addresses, source
snippets, secrets, analyzer diagnostics, and other details that must not be
committed or published.

This spec defines a deterministic evidence-pack workflow that turns existing
legacy scan outputs or already-redacted legacy summaries into public-safe review
material. Evidence packs are bounded summaries for docs, site, demo, and review
handoff. They are not raw scan outputs, not reducer conclusions, not runtime
proof, and not marketing claims.

Public claim level: hidden until at least one generated pack is validated,
reviewed, and explicitly promoted as public-safe.

## Scope

In scope:

- A core/docs tooling workflow for generating redacted evidence packs from
  legacy sample scans, baseline manifests, validation summaries, or public demo
  report summaries.
- A versioned evidence-pack schema with safe labels, counts, rule IDs, evidence
  tiers, coverage labels, limitations, extractor versions, command provenance,
  and hashed or neutral identifiers where needed.
- Safety classifications that distinguish `local-only`, `demo-safe`, and
  `public-safe` artifacts.
- Validation commands and safety gates that reject raw paths, remotes, SQL,
  config values, secrets, snippets, raw facts, raw logs, and unredacted private
  identities.
- Claim-boundary language that tells site/docs consumers what a pack may and
  may not prove.
- Tests using checked-in synthetic fixtures or already public-safe summaries.

Out of scope:

- Site page implementation, site copy, screenshots, or public launch content.
- Committing raw `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
  `report.md`, analyzer logs, raw validation outputs, or private sample
  manifests.
- Scanner extractor changes, reducer behavior changes, or new impact
  classifications.
- Runtime tracing, app hosting, browser execution, database connections, API
  calls, vulnerability/license analysis, production usage inference, or business
  impact analysis.
- LLM calls, embeddings, vector databases, prompt-based classification, or
  model-based summarization in TraceMap core.

## Requirements

### Requirement 1: Evidence Pack Input Boundaries

**User Story:** As a maintainer, I want to create public-safe proof material
from legacy validation scans without committing raw scan outputs or leaking
operator-local details.

#### Acceptance Criteria

1. WHEN an evidence pack is generated from raw scan outputs THEN the input
   directory SHALL be under an ignored local path such as
   `.tmp/legacy-codebase-validation/`, `.tmp/legacy-baselines/`, or another
   documented ignored output root, and the workflow SHALL verify that
   repository-relative raw input paths are ignored before reading them.
2. WHEN an evidence pack is generated from already-redacted summaries THEN the
   generator SHALL validate the source summary schema and safety classification
   before using it.
3. WHEN input artifacts include raw scan outputs THEN the pack generator SHALL
   read only the minimum structured fields and aggregate counts needed for the
   pack; it SHALL NOT copy raw facts, SQLite rows, analyzer logs, report prose,
   source snippets, raw SQL, config values, local paths, remotes, or private
   sample names into generated pack files.
4. WHEN input artifacts are missing, truncated, too old, schema-incompatible, or
   classified below the requested output level THEN the generator SHALL emit a
   rule-backed pack gap or fail validation; it SHALL NOT treat missing evidence
   as zero.
5. WHEN input scan identity is used THEN every source SHALL preserve commit
   identity proof. Public sources MAY include raw commit SHA when safe to
   disclose; private sources SHALL include a redacted deterministic commit
   identity such as a context-separated SHA-256 hash plus `shaPresent: true`.
   Public-safe packs SHALL NOT allow commit identity to be silently omitted.
6. WHEN the input scan has reduced coverage, failed build, fallback extraction,
   timeout, or analysis gaps THEN the pack SHALL carry those labels and
   limitations into every affected section.

### Requirement 2: Versioned Evidence Pack Artifact

**User Story:** As a docs or site consumer, I want a stable redacted artifact
that explains what TraceMap found with enough provenance to review the claims.

#### Acceptance Criteria

1. WHEN a pack is emitted THEN it SHALL include `schemaVersion`, `packId`,
   `packPurpose`, `claimLevel`, `sourceLabels`, `sourceClassifications`,
   `coverage`, `extractorVersions`, `commandProvenance`, `summary`,
   `evidenceSections`, `gaps`, `limitations`, and `safety` fields.
2. WHEN a pack contains findings or summary rows THEN every row SHALL include at
   least one rule ID, evidence tier, source label, coverage label, and safe
   provenance reference. Top-level aggregate summary scalar fields MAY omit
   per-field rule IDs only when the enclosing summary object cites
   `legacy.evidence-pack.summary.v1`.
3. WHEN a row summarizes file-backed evidence THEN it SHALL use safe path
   classes, extension counts, surface categories, or redacted path hashes only
   when the output classification allows them; public-safe packs SHALL NOT
   include raw file paths or developer-local fragments.
4. WHEN a row summarizes SQL, config, endpoint, package, WCF, WebForms, legacy
   data, build diagnostic, or flow evidence THEN it SHALL use safe shape,
   category, rule, count, hash, or neutral display metadata only.
5. WHEN command provenance is recorded THEN it SHALL include the TraceMap
   version, extractor versions, schema versions, normalized command name,
   sanitized option names, input artifact classifications, generation mode, and
   validation command names; it SHALL NOT include raw CLI paths, remotes,
   environment values, secrets, usernames, machine names, or unredacted input
   arguments.
6. WHEN pack JSON is serialized THEN keys, arrays, maps, and evidence sections
   SHALL be ordered deterministically.
7. WHEN Markdown is emitted THEN it SHALL be generated from the JSON pack and
   SHALL preserve the same claim boundaries, gaps, limitations, rule IDs, and
   evidence tiers.
8. WHEN pack identity or time metadata is needed THEN public-safe and demo-safe
   packs SHALL use an explicit injected date such as `--date YYYY-MM` or a
   fixture-pinned value; omitting this date SHALL fail for public-safe and
   demo-safe packs rather than falling back to wall-clock time. Local-only packs
   MAY omit `--date` only when the output remains local-only and is excluded
   from byte-stability expectations.
9. WHEN two different inputs would produce the same base `packId` THEN create
   SHALL add a deterministic content-derived safe suffix, and promote SHALL
   refuse to overwrite an existing destination by default.

### Requirement 3: Claim Levels And Promotion Rules

**User Story:** As a reviewer, I want to know whether an artifact is local-only,
demo-safe, or public-safe before it is shared.

#### Acceptance Criteria

1. WHEN a pack includes local paths, local-only hashes, private commit hashes,
   private grouping metadata, raw diagnostics, or operator-only labels THEN it
   SHALL be classified `local-only` and written only under ignored storage.
2. WHEN a pack uses checked-in public fixtures, public demo samples, or reviewed
   synthetic summaries but has not been approved for public site/docs claims
   THEN it MAY be classified `demo-safe`.
3. WHEN a pack contains only neutral labels, safe counts, rule IDs, evidence
   tiers, coverage labels, limitations, extractor versions, safe command
   provenance, and approved safe hashes or public SHAs THEN it MAY be classified
   `public-safe` after validation passes.
4. WHEN a pack is promoted to a tracked location THEN the promotion workflow
   SHALL rerun the pack validator, the generated-output sentinel, and
   `./scripts/check-private-paths.sh`, and SHALL verify that the destination is
   under an approved tracked root such as `docs/evidence-packs/legacy/`.
   The generated-output sentinel SHALL inspect the candidate JSON and Markdown
   files directly before copy or after staging, because `check-private-paths.sh`
   only inspects tracked files.
5. WHEN a pack is not public-safe THEN tracked docs SHALL be allowed to describe
   how to regenerate it using neutral placeholders, but SHALL NOT include its
   local labels, raw identifiers, paths, or outputs.
6. WHEN a pack is rejected THEN the diagnostic SHALL report sanitized categories
   and file paths only; it SHALL NOT echo unsafe values.

### Requirement 4: Evidence Sections For Legacy Samples

**User Story:** As a site manager or reviewer, I want enough aggregate proof to
show realistic legacy coverage without exposing raw applications.

#### Acceptance Criteria

1. WHEN legacy scan evidence exists THEN packs SHALL summarize fact counts,
   evidence tier counts, rule ID counts, extractor coverage, build/project-load
   status, scan status, analysis gaps, and output artifact availability.
2. WHEN legacy-specific evidence exists THEN packs SHOULD include sections for
   build diagnostics, WebForms event flow, WCF/service-reference metadata,
   static flow composition, SQL/query surfaces, legacy data metadata, package or
   config metadata, and baseline regression movement, as supported by available
   input summaries.
3. WHEN a section is unsupported, not requested, unavailable, truncated,
   reduced, or absent under reduced coverage THEN the section SHALL use the
   closed status vocabulary defined in `design.md` and include a rule-backed gap
   or limitation.
4. WHEN a section reports "found" evidence THEN it SHALL state static evidence
   counts and provenance; it SHALL NOT say runtime behavior occurred, queries
   executed, services were reachable, vulnerabilities exist, production traffic
   uses a path, or a business process is affected.
5. WHEN evidence came from syntax or text fallback THEN the section SHALL
   preserve Tier3 wording and SHALL NOT upgrade the claim because it appears in
   a pack.
6. WHEN high fan-out, ambiguous names, generic identifiers, missing extractors,
   or reduced coverage affect interpretation THEN packs SHALL include review
   caveats and cap any summary language at review-needed static evidence.

### Requirement 5: Redaction And Safety Validator

**User Story:** As a maintainer, I want automated gates that prevent unsafe
evidence packs from reaching docs or site work.

#### Acceptance Criteria

1. WHEN pack JSON or Markdown is generated THEN a validator SHALL scan the files
   for local absolute paths, home-directory fragments, private sample names,
   raw repository remotes, raw SQL, config values, connection strings, endpoint
   addresses, credentials, secrets, tokens, source snippets, analyzer log
   fragments, and unescaped user-controlled Markdown.
2. WHEN unsafe content is detected THEN validation SHALL fail with sanitized
   categories and the generated file path; it SHALL NOT print the unsafe token.
3. WHEN hashing is used THEN hash inputs SHALL be deterministic,
   context-separated, length-prefixed or otherwise unambiguous, and documented.
4. WHEN a value is secret-like, credential-like, low-entropy private identity,
   enumerable private identity, or derived from raw source text THEN it SHALL be
   omitted or represented category-only in demo-safe and public-safe packs, not
   hashed.
5. WHEN Markdown is emitted THEN table cells and inline display fields SHALL
   escape or omit pipes, angle brackets, backticks, brackets, line breaks, and
   other user-controlled Markdown syntax.
6. WHEN existing `scripts/check-private-paths.sh` runs THEN tracked pack files
   SHALL pass without adding machine-specific allowlists.
7. WHEN a generated-output sentinel runs over pack JSON and Markdown THEN it
   SHALL inspect the generated public-shareable files recursively, not raw local
   scan artifacts. The pack safety validator MAY provide this sentinel behavior
   if it can run independently against generated pack files.

### Requirement 6: CLI Or Script Workflow

**User Story:** As an implementer, I want a concrete workflow that can be tested
without private repositories and consumed by later docs/site work.

#### Acceptance Criteria

1. WHEN implementation starts THEN it SHOULD prefer first-class CLI commands
   such as `tracemap evidence-pack create`, `tracemap evidence-pack validate`,
   and `tracemap evidence-pack promote`; a temporary script fallback is allowed
   only if the implementation state records the CLI blocker.
2. WHEN `create` runs THEN it SHALL support input modes for raw scan directory,
   legacy validation summary, legacy baseline manifest, and public demo summary
   where schemas are available.
3. WHEN `create` runs THEN it SHALL require a neutral `--label`, a
   `--claim-level`, a `--purpose`, and an output directory. Public-safe and
   demo-safe creation SHALL also require an explicit or fixture-pinned
   `--date`.
4. WHEN `validate` runs THEN it SHALL be usable independently against a pack
   JSON file and SHALL fail rejected or overclassified packs.
5. WHEN `promote` runs THEN it SHALL copy only public-safe JSON/Markdown pack
   files to an approved tracked destination after validation and safety gates
   pass; raw inputs SHALL remain local-only.
6. WHEN dry-run mode is requested for create or promote THEN the workflow SHALL
   report the would-be classification, gaps, destination, and output file names
   without writing tracked files.
7. WHEN output is inside the repository root THEN the workflow SHALL refuse
   local-only output unless `git check-ignore` proves the destination is
   ignored. Any non-zero or error exit from `git check-ignore` SHALL be treated
   as refusal, not as approval.

### Requirement 7: Tests And Validation

**User Story:** As a future implementer, I want the spec to define enough tests
and commands to make the evidence-pack generator implementation-ready.

#### Acceptance Criteria

1. Tests SHALL use synthetic fixtures, checked-in public samples, or already
   public-safe summaries only.
2. Tests SHALL cover deterministic JSON and Markdown output for identical
   inputs.
3. Tests SHALL cover `local-only`, `demo-safe`, `public-safe`, and `rejected`
   safety classifications.
4. Tests SHALL prove public-safe packs omit raw paths, remotes, SQL, config
   values, endpoint addresses, snippets, logs, secrets, and private identity.
5. Tests SHALL prove validator diagnostics do not echo unsafe values.
6. Tests SHALL prove every evidence row has rule ID, evidence tier, coverage
   label, source label, limitations, and safe provenance.
7. Tests SHALL cover reduced coverage, failed build, truncated inputs, missing
   summary schemas, missing extractor versions, and unsupported sections.
8. Tests SHALL cover command provenance redaction and deterministic ordering.
9. Tests SHALL cover Markdown escaping and prohibited-claim wording, including
   runtime execution, vulnerability, production usage, release approval, and
   business impact phrases.
10. Implementation validation SHALL include focused tests, `dotnet build
    src/dotnet/TraceMap.sln`, `dotnet test src/dotnet/TraceMap.sln`,
    `./scripts/check-private-paths.sh`, `git diff --check`, and any relevant
    pinned smoke checks from `docs/VALIDATION.md` or an explicit deferral
    rationale.
11. WHEN future implementation checks off tasks in this spec THEN
    `tasks.md` and `implementation-state.md` SHALL be updated in the same PR.

## Deferred Follow-Ups

- Site page or docs copy that consumes promoted public-safe packs.
- Hosted pack artifact publishing.
- Portfolio-level evidence packs across multiple public-safe sample labels.
- Optional local-only drilldown packs for private validation review.
- A richer visual explorer over public-safe pack JSON.
