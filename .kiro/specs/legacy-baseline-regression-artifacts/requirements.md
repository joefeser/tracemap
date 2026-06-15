# Legacy Baseline Regression Artifacts Requirements

## Introduction

TraceMap is being exercised against old and large .NET repositories where
baseline behavior matters as much as future improvement. Maintainers need a
safe "original parser snapshot" that preserves what the scanner could see
before additional legacy enhancements, without committing raw scan artifacts,
private sample identities, local paths, remotes, source snippets, raw SQL, or
configuration values.

This spec defines deterministic, redacted baseline summaries and regression
comparisons for legacy sample scans. The baseline is a measurement artifact, not
an impact analysis claim. It helps decide whether future legacy parser work is
still worth doing by comparing counts, coverage labels, rule coverage, fact
coverage, limitations, and known gaps across scanner versions.

Public claim level: hidden until at least one redacted baseline manifest is
reviewed and explicitly marked public-safe.

## Scope

In scope:

- A local-only workflow for creating redacted baseline summaries from legacy
  scan outputs under ignored working directories.
- A public-safe committed manifest format that uses neutral sample labels and
  sanitized counts only when promotion checks pass.
- A local-only manifest format for baselines that are useful but not safe to
  publish.
- Count summaries for facts, rules, evidence tiers, coverage labels, extractor
  versions, scan status, build status, and known gaps.
- Regression comparison output that reports changed counts and coverage, not
  business impact.
- Safety checks that reject unsafe strings before a baseline is committed or
  comparison output is promoted.
- Tests using synthetic or public fixtures only.

Out of scope:

- Storing raw `facts.ndjson`, `index.sqlite`, `report.md`, analyzer logs, source
  snippets, raw SQL, config values, connection strings, endpoint addresses, raw
  remotes, local absolute paths, or private repository names in committed
  baseline artifacts.
- Proving runtime behavior, production reachability, security posture, or
  business impact.
- Adding LLM calls, embeddings, vector databases, prompt-based classification,
  or probabilistic model inference to TraceMap core.
- Changing scanner extraction rules as part of this spec.
- Site copy or public marketing claims.

## Requirements

### Requirement 1: Redacted Baseline Manifest

**User Story:** As a maintainer, I want a frozen original-parser baseline for
legacy samples that can be safely compared later without leaking private sample
details.

#### Acceptance Criteria

1. WHEN a baseline is created THEN the tool SHALL produce a deterministic
   baseline manifest with neutral sample label, baseline ID, deterministic
   creation metadata, TraceMap version, extractor versions, scan coverage label,
   build status, scan status, safe repo identity, commit identity, and artifact
   schema version. Time fields SHALL be injectable for tests and public-safe
   manifests SHALL use at most year-month precision unless a fixture explicitly
   pins a stable value.
2. WHEN repo or commit identity is included THEN the manifest SHALL use a
   neutral label plus a stable hash or safe public commit SHA only when the
   source is explicitly marked public-safe.
3. WHEN the source sample is private or locally named THEN the manifest SHALL
   NOT include raw repository name, local absolute path, raw remote URL, raw
   branch name if identifying, username, organization name, or machine-specific
   path fragments.
4. WHEN scan outputs include file paths THEN committed baseline manifests SHALL
   store only aggregate counts by safe path class, language, or file extension;
   local-only manifests MAY store sanitized relative path hashes if explicitly
   labeled local-only.
5. WHEN baseline data is partial due to failed build, timeout, truncated output,
   unsupported project format, or parser gap THEN the manifest SHALL label the
   baseline partial and preserve known gap counts.
6. WHEN a manifest is serialized THEN keys, arrays, and count maps SHALL be
   sorted deterministically.
7. WHEN a baseline manifest is written THEN it SHALL include limitations and
   safety classification fields explaining whether it is `public-safe`,
   `local-only`, or `rejected`.

### Requirement 2: Count and Coverage Summary

**User Story:** As a reviewer, I want the baseline to preserve enough aggregate
evidence to judge future improvements without preserving unsafe artifacts.

#### Acceptance Criteria

1. WHEN a baseline is created THEN it SHALL summarize total fact count, gap
   count, rule ID counts, fact type counts, evidence tier counts, extractor
   counts, coverage labels, build/project-load status, and output artifact
   existence.
2. WHEN rule or fact counts are emitted THEN every count SHALL trace to a rule
   ID, fact type, evidence tier, extractor ID/version, or known gap code.
3. WHEN output artifact existence is summarized THEN the manifest SHALL record
   presence, byte-size bucket, and content hash of redacted summaries only; it
   SHALL NOT hash or commit raw private artifacts unless they have first been
   sanitized into a safe summary.
4. WHEN large samples exceed configured timeout or artifact-size bounds THEN the
   baseline SHALL record `truncated` or `deferred` state with a limitation, not
   silently treat missing counts as zero.
5. WHEN a scanner run has no facts for a category THEN the baseline SHALL render
   an explicit zero only for categories that were in scope for that scan;
   unsupported or unscanned categories SHALL be labeled unknown or not-in-scope.
6. WHEN a rule catalog entry is absent for a rule observed in facts THEN the
   baseline workflow SHALL fail validation or mark the baseline rejected rather
   than committing unexplainable evidence.

### Requirement 3: Rule and Fact Coverage Snapshot

**User Story:** As an implementer, I want baseline coverage snapshots that show
which legacy surfaces the current parser saw so future work can compare real
movement.

#### Acceptance Criteria

1. WHEN a baseline is created THEN it SHALL include a rule coverage snapshot
   listing observed rule IDs, evidence tier distribution, extractor versions,
   limitations, and known unsupported categories.
2. WHEN a fact type appears in the scan THEN the snapshot SHALL include its
   count, evidence tier distribution, and contributing rule IDs.
3. WHEN legacy-specific surfaces such as UI events, WCF/service references,
   config, SQL, package metadata, WebForms, WinForms, database metadata, or
   build environment diagnostics are present THEN the snapshot SHALL summarize
   counts using safe category names only.
4. WHEN a surface is important but not detected THEN the snapshot SHALL record
   that absence as `not-observed` only if the scan scope included the relevant
   files; otherwise it SHALL use `not-in-scope` or `unknown`.
5. WHEN semantic analysis fails or is reduced THEN the snapshot SHALL preserve
   syntax/config fallback counts separately from semantic counts.
6. WHEN limitations are rendered THEN each limitation SHALL name the rule ID,
   extractor, or gap category that constrains interpretation.

### Requirement 4: Regression Comparison Output

**User Story:** As a maintainer, I want to compare future TraceMap changes
against the original parser snapshot without overstating what changed.

#### Acceptance Criteria

1. WHEN a new scan summary is compared to a baseline THEN the comparison SHALL
   report changed counts for facts, rules, evidence tiers, extractors, coverage
   labels, build status, known gaps, and supported surfaces.
2. WHEN counts change THEN the comparison SHALL classify the movement as
   `increase`, `decrease`, `unchanged`, `new-category`, `removed-category`, or
   `coverage-changed`.
3. WHEN a comparison shows more evidence THEN it SHALL say the scanner produced
   additional static evidence; it SHALL NOT claim code is impacted, safe,
   reachable, used in production, or business-critical.
4. WHEN a comparison shows fewer facts or reduced coverage THEN it SHALL flag
   the change as review-needed unless an explicit migration map explains the
   expected extraction or schema change.
5. WHEN rule IDs or fact types change names THEN the comparison SHALL require an
   explicit migration map or mark the result review-needed.
6. WHEN baseline and candidate scans use different schema versions or extractor
   versions THEN the comparison SHALL display that difference and avoid
   pretending the counts are directly comparable without a documented migration.
7. WHEN a comparison report is emitted THEN it SHALL be available as
   machine-readable JSON and human-readable Markdown.

### Requirement 5: Public-Safe Versus Local-Only Artifacts

**User Story:** As a repository maintainer, I want clear rules for what can be
committed and what must stay local while still preserving useful baselines.

#### Acceptance Criteria

1. WHEN a baseline contains only neutral labels, safe hashes, counts, coverage
   labels, rule IDs, evidence tier distributions, extractor versions,
   limitations, and known gap categories THEN it MAY be marked `public-safe`
   after safety checks pass.
2. WHEN a baseline requires path hashes, private sample grouping, private
   commit hashes, or other details that could aid local comparison but should
   not be published THEN it SHALL be marked `local-only` and written only under
   ignored `.tmp/` storage.
3. WHEN a baseline or comparison contains raw source snippets, raw SQL, config
   values, connection strings, endpoint addresses, remotes, absolute paths,
   private repository names, usernames, secrets, credentials, or raw analyzer
   output THEN it SHALL be rejected for committed storage.
4. WHEN a public-safe baseline is promoted into `.kiro/`, `docs/`, or another
   tracked location THEN the promotion command SHALL rerun safety checks over
   the promoted files.
5. WHEN local-only baselines exist THEN tracked docs SHALL describe how to
   regenerate or compare them without naming local samples or paths.

### Requirement 6: Safety and Redaction Checks

**User Story:** As a reviewer, I want automated checks that fail before unsafe
baseline data reaches the repository.

#### Acceptance Criteria

1. WHEN baseline or comparison files are generated THEN a safety validator SHALL
   scan them for absolute paths, home-directory fragments, private sample
   names, raw remotes, raw SQL, connection strings, config values, endpoint
   addresses, secrets, credentials, and source-like snippets.
2. WHEN unsafe content is detected THEN the validator SHALL fail with a
   sanitized diagnostic category and file path; it SHALL NOT echo the unsafe
   value.
3. WHEN existing `scripts/check-private-paths.sh` runs THEN the new public-safe
   baseline files SHALL pass without allowlisting private strings.
4. WHEN a redaction hash is used THEN the hash SHALL be deterministic,
   documented, fixed-length, and computed from safe-to-hash inputs with
   unambiguous context separation such as length-prefixed components or an
   existing structured TraceMap hash helper.
5. WHEN a value is secret-like, low-entropy/enumerable private identity, or not
   safe to hash THEN it SHALL be omitted or represented category-only.
6. WHEN redaction removes detail THEN the manifest SHALL record a limitation so
   comparisons do not treat omitted values as absent evidence.

### Requirement 7: Local Workflow and Validation

**User Story:** As an implementer, I want implementation tasks that can be
completed and validated without access to private legacy repositories.

#### Acceptance Criteria

1. WHEN implementing this spec THEN tests SHALL use synthetic fixtures or
   public sample outputs that do not contain private paths, names, remotes,
   snippets, SQL, config values, or secrets.
2. WHEN the local workflow reads legacy scan artifacts THEN it SHALL read them
   only from ignored `.tmp/legacy-baselines/`, an explicitly documented ignored
   path, or a tracked `samples/` fixture explicitly marked synthetic and
   public-safe.
3. WHEN a CLI command creates or compares baselines THEN it SHALL support a
   dry-run or validation mode that reports safety classification without
   writing tracked files.
4. WHEN implementation changes are complete THEN validation SHALL include
   focused tests, `dotnet test`, `./scripts/check-private-paths.sh`,
   `git diff --check`, and any relevant pinned smoke checks from
   `docs/VALIDATION.md` or an explicit deferral rationale.
5. WHEN a future implementation completes tasks in this spec THEN
   `tasks.md` SHALL be updated with completed checkboxes and
   `implementation-state.md` SHALL record branch, validation, oddities, and
   follow-up items.
