# Legacy Sample Smoke Catalog Requirements

## Introduction

TraceMap needs a public-safe, operator-safe catalog of old-code validation
sample families and pinned smoke expectations. Maintainers should be able to see
which neutral samples exercise WCF, .NET Remoting, WebForms, legacy data
metadata, build diagnostics, huge repository stress, and related legacy
surfaces without reading raw scan outputs or leaking private local context.

The catalog is not raw scan output, not an evidence pack, not a baseline
comparison artifact, not a site page, and not an impact-analysis result. It is
a deterministic inventory of sample labels, safe source classification, pinned
commit identity, expected evidence families, validation commands, claim level,
and redaction requirements.

Public claim level: hidden until catalog entries are backed by public or
demo-safe proof and pass catalog validation. Individual entries may later
graduate to `demo-safe`; public-facing claims remain conservative and require
separate promotion through evidence-pack or docs/site workflows.

## Scope

In scope:

- A versioned catalog schema for legacy sample smoke validation entries.
- A tracked storage location for public-safe and demo-safe catalog metadata.
- An ignored local storage location for operator-only sample discovery notes.
- Safe sample labels, checked-out commit identity, source classification,
  expected evidence families, validation commands, redaction rules, limitations,
  and public claim level.
- Relationships to `legacy-codebase-validation`,
  `legacy-baseline-regression-artifacts`, and `legacy-sample-evidence-pack`
  without copying their raw artifacts or generated proof packets.
- Validation and tests that prevent local paths, private identities, raw
  remotes, SQL/config values, secrets, snippets, analyzer logs, and raw scan
  outputs from entering tracked catalog files.

Out of scope:

- Scanner, reducer, extractor, or CLI behavior changes beyond future catalog
  tooling.
- Site implementation or public marketing copy.
- Raw `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`,
  analyzer logs, raw validation outputs, raw baselines, raw evidence packs, or
  private local sample manifests in tracked files.
- Storing source snippets, raw SQL, raw config, endpoint values, secrets,
  connection strings, raw repository remotes, local paths, private repo names,
  organization names, usernames, hostnames, or branch names.
- Runtime reachability, production usage, vulnerability status, release safety,
  business impact, SQL execution, or reducer impact conclusions.
- LLM calls, embeddings, vector databases, prompt-based classification, or
  model-generated catalog decisions in TraceMap core.

## Requirements

### Requirement 1: Catalog Storage And Boundaries

**User Story:** As a maintainer, I want a tracked catalog of neutral legacy
sample smoke families without committing raw scan artifacts or local operator
details.

#### Acceptance Criteria

1. WHEN the catalog is implemented THEN tracked public-safe and demo-safe
   metadata SHALL live under `docs/validation/legacy-sample-smoke-catalog/`.
2. WHEN a machine-readable catalog is emitted THEN it SHALL use
   `catalog.json` with schema version `legacy-sample-smoke-catalog.v1`.
3. WHEN a human-readable catalog is emitted THEN it SHALL use `catalog.md`
   generated from the JSON catalog and SHALL NOT be hand-edited independently.
4. WHEN operators need local candidate sample paths or private source notes THEN
   those inputs SHALL stay under ignored `.tmp/legacy-sample-smoke-catalog/`.
5. WHEN tracked catalog files are created or promoted THEN the workflow SHALL
   reject raw scan artifacts, raw validation summaries, baseline manifests,
   evidence-pack JSON, analyzer logs, SQLite indexes, and raw reports.
6. WHEN the catalog references related artifacts THEN it SHALL store only safe
   artifact class, neutral identifier, claim level, schema version, hash or
   count metadata where allowed, and generation command names.

### Requirement 2: Safe Sample Identity And Source Classification

**User Story:** As an operator, I want each sample entry to identify what it
validates while keeping public, private, and local identities separated.

#### Acceptance Criteria

1. WHEN a sample entry is stored THEN it SHALL include a neutral `sampleLabel`
   such as `legacy-wcf-public-fixture`, `legacy-remoting-demo-sample`,
   `legacy-webforms-public-app`, `legacy-data-metadata-public-fixture`,
   `legacy-build-diagnostics-synthetic`, or `large-public-dotnet-client`.
2. WHEN a sample entry is stored THEN it SHALL include `sourceClassification`
   from a closed vocabulary: `synthetic-fixture`, `public-repo`,
   `public-archive`, `public-doc-sample`, `private-local`, `operator-local`,
   or `unknown`.
3. WHEN a sample source is private, local, operator-only, or unknown THEN the
   tracked catalog SHALL use neutral labels and category-only identity; it SHALL
   NOT include raw repository names, raw remotes, local paths, organization
   names, usernames, hostnames, or branch names.
4. WHEN a sample source is explicitly public and reviewed THEN the tracked
   catalog MAY include a safe source alias and raw checked-out commit SHA, but
   SHALL NOT include the raw remote URL.
5. WHEN commit identity exists for a non-public or not-yet-reviewed source THEN
   tracked catalog entries SHALL record `commitIdentity.kind` as
   `category-only` and SHALL preserve `shaPresent: true` when a SHA was
   observed without exposing or hashing the raw SHA.
6. WHEN a sample lacks a pinned commit or fixture version THEN the catalog entry
   SHALL be classified no higher than `hidden` and SHALL include a limitation
   explaining that expectations are not reproducible.
7. WHEN labels are validated THEN labels containing path separators, URI
   schemes, `.git`, `@` identities, Windows drive prefixes, home fragments,
   hostnames, organization/user patterns, private-looking tokens, secrets, or
   raw project names SHALL be rejected for tracked output.

### Requirement 3: Evidence Family Expectations

**User Story:** As a maintainer, I want to know which old-code evidence families
each sample is expected to exercise and how strong those expectations are.

#### Acceptance Criteria

1. WHEN an entry is stored THEN it SHALL include one or more expected evidence
   families from a closed vocabulary covering at least:
   `wcf-service-reference`, `wcf-config-endpoint-shape`,
   `remoting-registration`, `remoting-channel-config`,
   `webforms-event-binding`, `webforms-markup-codebehind`,
   `dbml-linq-to-sql`, `edmx-entity-framework`,
   `typed-dataset`, `legacy-sql-or-query-surface`,
   `build-environment-diagnostics`, `msbuild-project-load-failure`,
   `packages-config`, `binding-redirects`, `large-repo-stress`,
   `fallback-syntax-scan`, and `analysis-gap-reporting`.
2. WHEN an expected evidence family is listed THEN the row SHALL include
   expected rule ID patterns or exact rule IDs, expected evidence tiers,
   expected coverage labels, expected extractor IDs, limitation codes, and
   whether the expectation is required, optional, or exploratory.
3. WHEN expected evidence is known only through syntax/text fallback THEN the
   expectation SHALL remain Tier3 or reduced-coverage wording and SHALL NOT be
   upgraded to semantic proof.
4. WHEN a sample is intended to exercise huge repository behavior THEN the
   entry SHALL include timeout and artifact-size expectation buckets without
   raw file lists or repository path counts that could identify private code.
5. WHEN evidence is absent, unsupported, truncated, deferred, reduced, or
   blocked by missing tooling THEN the catalog SHALL represent that as a
   catalog expectation state rather than claiming the sample lacks the feature.
6. WHEN catalog entries mention "coverage" THEN they SHALL use TraceMap coverage
   labels and limitations, not informal success language.

### Requirement 4: Validation Commands And Pinned Expectations

**User Story:** As an operator, I want each catalog entry to tell me the safe
commands and expectations needed to rerun the smoke without exposing local
details.

#### Acceptance Criteria

1. WHEN an entry is stored THEN it SHALL include sanitized validation command
   templates, not operator-specific command lines.
2. WHEN command templates reference paths THEN they SHALL use placeholders such
   as `<sample-root>`, `<scan-output>`, and `<catalog-output>` only.
3. WHEN command provenance is stored THEN it SHALL include command names,
   required options, timeout bucket, artifact-size bucket, expected output
   artifact classes, and validation gate names.
4. WHEN a smoke expectation depends on related specs THEN the entry SHALL state
   whether it consumes a redacted validation summary, a redacted baseline
   manifest, or an evidence-pack summary by safe schema name and claim level
   only.
5. WHEN expected outputs are listed THEN they SHALL use artifact class names
   such as `scan-manifest`, `facts-ndjson-present`, `index-sqlite-present`,
   `report-md-present`, `analyzer-log-present`, `redacted-summary`, or
   `evidence-pack-reference`; they SHALL NOT include raw file paths or raw
   artifact contents.
6. WHEN a validation command cannot be run on a developer machine because a
   sample requires local tooling or licensing THEN the entry SHALL mark the
   command as `operator-local` and cap the public claim level at `hidden` until
   a public/demo-safe substitute exists.
7. WHEN command templates include string option values that can carry identity
   or operator-specific data, including labels, names, repo references, raw
   dates beyond `YYYY-MM`, branch names, artifact IDs, or source identifiers,
   THEN those values SHALL use angle-bracket placeholders; literal option
   values are allowed only for booleans and fixed values from documented closed
   vocabularies.

### Requirement 5: Redaction And Claim Levels

**User Story:** As a reviewer, I want the catalog to be safe to share inside the
repo while clearly showing what can and cannot be claimed publicly.

#### Acceptance Criteria

1. WHEN an entry is classified THEN it SHALL use a closed `claimLevel`
   vocabulary: `hidden`, `demo-safe`, or `public-safe`.
2. WHEN proof is private, local-only, missing, unreviewed, or relies on
   operator-only samples THEN `claimLevel` SHALL be `hidden`.
3. WHEN proof comes from synthetic fixtures, public doc samples, or reviewed
   neutral examples sufficient for demonstrations but not public product claims
   THEN `claimLevel` MAY be `demo-safe`.
4. WHEN proof comes from reviewed public or synthetic sources, pinned commit or
   fixture identity, passing catalog validation, and public-safe downstream
   proof THEN `claimLevel` MAY be `public-safe`.
5. WHEN tracked catalog output is rendered or promoted for `demo-safe` or
   `public-safe` use THEN hidden entries SHALL cause validation failure unless
   they were explicitly omitted before render using
   `--minimum-entry-claim-level <demo-safe|public-safe>`; validators SHALL
   reject any tracked catalog whose top-level classification is higher than the
   least-safe included entry.
6. WHEN catalog text describes expected evidence THEN it SHALL avoid runtime
   behavior, production usage, service reachability, vulnerability/security
   posture, SQL execution, release approval, customer impact, business impact,
   or reducer impact conclusions.
7. WHEN a catalog entry is promoted or validated THEN the validator SHALL reject
   local absolute paths, home-directory fragments, raw remotes, raw SQL, config
   values, connection strings, endpoint values, secrets, credentials, tokens,
   source snippets, analyzer diagnostics, raw private names, and unsafe Markdown.
8. WHEN hashing is used THEN hash inputs SHALL be context-separated,
   deterministic, and documented; secret-like, credential-like, low-entropy,
   enumerable private, and source-derived values SHALL be omitted or
   category-only rather than hashed for tracked output.
9. WHEN a tracked catalog stores rule IDs or rule ID patterns THEN those strings
   SHALL be subject to the same redaction and prohibited-claim scanning as every
   other JSON string field.

### Requirement 6: Relationship To Nearby Legacy Work

**User Story:** As a maintainer, I want this catalog to coordinate with existing
legacy validation specs without duplicating their raw artifacts or generated
outputs.

#### Acceptance Criteria

1. WHEN the catalog references `legacy-codebase-validation` THEN it SHALL treat
   that workflow as the owner of local scan execution and redacted validation
   summaries.
2. WHEN the catalog references `legacy-baseline-regression-artifacts` THEN it
   SHALL treat that workflow as the owner of redacted baseline snapshots and
   comparison movement.
3. WHEN the catalog references `legacy-sample-evidence-pack` THEN it SHALL
   treat evidence packs as downstream proof material for selected catalog
   entries, not as catalog source records to copy.
4. WHEN a catalog entry links to any related artifact THEN it SHALL use safe
   schema names, neutral artifact IDs, claim levels, and validation status only.
5. WHEN raw artifacts are needed to inspect a failing smoke THEN the catalog
   SHALL point operators to ignored local roots and regeneration commands with
   placeholders, not to tracked raw outputs.
6. WHEN site work needs public claims THEN site specs SHALL consume promoted
   public-safe evidence packs or docs generated from them; this catalog SHALL
   remain a maintainer validation inventory and SHALL NOT become a site page.

### Requirement 7: Tests And Validation

**User Story:** As a reviewer, I want automated checks proving catalog files are
deterministic, safe, and useful before implementation is considered complete.

#### Acceptance Criteria

1. WHEN catalog tooling is implemented THEN tests SHALL cover schema validation,
   deterministic JSON ordering, Markdown generation from JSON, label
   validation, source classification, checked-out SHA handling, evidence-family
   expectation states, command-template redaction, claim-level gating, and
   relationship references.
2. WHEN unsafe fixture values are planted THEN tests SHALL prove the validator
   rejects raw local paths, private names, raw remotes, SQL/config values,
   secrets, snippets, analyzer diagnostics, endpoint values, and unsafe
   Markdown without echoing the unsafe token.
3. WHEN public-safe entries are validated THEN tests SHALL prove they contain
   only safe labels, safe source aliases, allowed commit identity, expected
   evidence families, rule IDs or patterns, evidence tiers, coverage labels,
   limitations, extractor IDs, command template names, and safe relationship
   references.
4. WHEN hidden or operator-local entries are present THEN tests SHALL prove they
   cannot be promoted to public-safe catalog output without reviewed proof.
5. WHEN validation commands are documented THEN implementation validation SHALL
   run focused catalog tests, `./scripts/check-private-paths.sh`, `git diff
   --check`, and only the pinned smokes from `docs/VALIDATION.md` affected by
   catalog tooling changes; unrelated scanner/language-adapter smokes may be
   explicitly deferred.
