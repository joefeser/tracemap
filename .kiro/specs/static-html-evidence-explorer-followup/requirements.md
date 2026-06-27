# Static HTML Evidence Explorer Follow-Up Requirements

## Introduction

The local static HTML evidence explorer is implemented and has follow-up slices
available. Current `origin/dev` includes a generated static bundle for
`tracemap explorer generate`, safe first-slice rendering from
`scan-manifest.json` and `facts.ndjson`, provenance-only handling for
`index.sqlite` and `report.md`, compatible `rule-catalog.yml` rows, visible
gaps/limitations/rules/evidence rows, section status rows, and generated-output
safety validation.

This follow-up spec defines one bounded implementation slice for generated
static explorer artifacts: a deterministic compatibility ledger and safety
profile conflict hardening pass. The slice keeps the explorer useful when
artifacts are missing, unsupported, provenance-only, or profile-incompatible,
without adding broad UI rebuilds or new source analysis.

Public claim level: hidden.

Rationale: this is an implementation-planning and hardening slice for local
generated artifacts. It should not create a public/demo product claim until it
is implemented, validated against public-safe fixtures, and reviewed. The
public `tracemap.tools` site is out of scope.

## Scope

In scope:

- Add a safe explorer compatibility ledger for generated artifact inputs and
  sections.
- Harden safety profile and claim-level conflict handling before richer
  artifact readers are added.
- Preserve deterministic navigation over generated evidence artifacts, with
  clear statuses for supported, provenance-only, missing, unsupported,
  profile-incompatible, safety-omitted, and compatible-empty sections.
- Preserve rule IDs, evidence tiers, supporting IDs, safe commit SHA, coverage
  labels, limitations, and extractor/generator versions where available.
- Require rule catalog entries and documented limitations before any new
  explorer-emitted rules, gaps, limitations, redactions, or validation failures.
- Add focused .NET tests and, if rendering changes, generated-output browser or
  smoke validation.

Out of scope:

- Public `tracemap.tools` site work, site specs, site copy, site deployment, or
  hosted explorer changes.
- Broad UI redesign, graph visualization, new landing page, or marketing copy.
- New scanner, reducer, adapter, Roslyn, SQLite relationship, or source
  repository analysis behavior.
- New surface/path/reducer readers except small compatibility hooks needed to
  label those artifact families as unavailable or future-supported.
- Runtime behavior, runtime reachability, production use, deployment safety,
  ownership, vulnerability, or complete product coverage claims.
- LLM calls, embeddings, vector databases, prompt-based classification,
  generated summaries, semantic search, or AI impact analysis in TraceMap core.
- Raw snippets, raw facts, raw SQLite content, analyzer logs, raw SQL, config
  values, secrets, local absolute paths, raw remotes, hostnames, raw endpoint
  addresses, query strings, private repo names, private sample names, or
  generated scan directory names in public/demo output.

## Requirements

### Requirement 1: Compatibility Ledger For Generated Inputs

**User Story:** As a reviewer, I want the explorer to show why each generated
artifact and section is available, partial, unsupported, or unavailable without
reading raw artifacts.

#### Acceptance Criteria

1. WHEN the explorer discovers generated input artifacts THEN it SHALL record a
   safe compatibility ledger in `explorer-data.json` or equivalent safe view
   data.
2. WHEN a ledger row is emitted THEN it SHALL include a stable artifact or
   section ID, safe label, artifact kind or section kind, compatibility status,
   rule ID, evidence tier, support IDs, coverage label where available,
   limitation IDs or messages, and safe source/artifact scope.
3. WHEN a supported artifact is rendered into evidence rows THEN the ledger
   SHALL distinguish rendered-compatible from compatible-empty.
4. WHEN an artifact is hashed for provenance only THEN the ledger SHALL label
   it `provenance-only` or an equivalent closed value and SHALL NOT imply that
   raw artifact contents were inspected.
5. WHEN an artifact is missing THEN the ledger SHALL label it `not-provided`
   and distinguish required first-slice inputs from optional/future inputs.
6. WHEN an artifact is present but unsupported THEN the ledger SHALL label it
   `unsupported-schema` or `unsupported-artifact` with an existing or newly
   cataloged rule ID.
7. WHEN an artifact is omitted because of safety or profile compatibility THEN
   the ledger SHALL label it with a closed status such as `safety-omitted` or
   `profile-incompatible` and SHALL include the rule-backed limitation.
8. WHEN ledger rows reference artifacts THEN they SHALL use stable artifact
   IDs or safe generated labels, not raw file paths, raw remotes, private repo
   names, private sample labels, or generated scan directory names.
9. WHEN ledger statuses appear in HTML THEN the same safe data SHALL appear in
   downloadable or embedded explorer data with no less redaction than the UI.

### Requirement 2: Safety Profile And Claim-Level Conflict Hardening

**User Story:** As a maintainer, I want the explorer to prevent profile
weakening when multiple generated artifacts disagree about claim level or
safety expectations.

#### Acceptance Criteria

1. WHEN `--safety-profile public-demo` or an alias such as `public-safe` is
   selected THEN the explorer SHALL use the strict public/demo rendering and
   validation path.
2. WHEN `--safety-profile hidden-local` is selected THEN the explorer SHALL
   visibly label the generated output as hidden/local and SHALL record
   redaction, hashing, category-only, omission, and compatibility counts.
3. WHEN a currently parsed generated artifact carries an explicit claim level
   or safety profile field independent of the selected output profile THEN the
   explorer SHALL reconcile it against the selected profile. If the artifact
   field is stronger or weaker than the selected output profile, the explorer
   SHALL stop, omit the affected artifact, or mark affected sections partial
   using a rule-backed conflict row; it SHALL NOT silently weaken public/demo
   output.
4. WHEN an artifact lacks claim-level metadata THEN the explorer SHALL treat
   the metadata as unknown, render only fields already safe under the selected
   profile, and SHALL NOT treat unknown metadata as a conflict by itself. The
   explorer SHALL emit a visible limitation when unknown metadata affects
   interpretation.
5. WHEN multiple compatible structured artifacts that are parsed in the current
   slice disagree on a conflict dimension available from currently parsed
   artifact fields, such as commit SHA, THEN the explorer SHALL emit a conflict
   kind from a closed vocabulary and SHALL NOT present them as a single coherent
   complete view. Conflict dimensions that no current parsed artifact can expose,
   including claim level, safety profile, source identity, and schema version,
   SHALL remain forward-compatible hooks, not PR 1 production conflict claims.
6. WHEN conflict handling uses an existing rule such as
   `explorer.input.provenance-conflict.v1` THEN the conflict kind and
   limitation text SHALL make claim-level, commit, schema, profile, and source
   identity conflicts distinguishable.
7. WHEN implementation introduces a new conflict rule ID OR reuses an existing
   rule for a previously-deferred conflict subtype THEN `rules/rule-catalog.yml`
   SHALL be updated in the same PR so the emitted subtype is no longer
   documented as deferred and still has accurate limitations.
8. WHEN conflict diagnostics are thrown or rendered THEN they SHALL include
   rule ID, evidence tier, safe artifact/section ID, and safe category only,
   never the unsafe raw value.
9. WHEN safety profiles are compared with claim levels THEN implementation
   SHALL treat normalized safety profile names and claim-level names as
   separate namespaces; it SHALL NOT rely on direct string equality between
   profile aliases such as `public-safe` and the public-safe claim-level token.

### Requirement 3: Public-Safe Generated Artifact Safety

**User Story:** As a project owner, I want generated explorer outputs to remain
safe to inspect or share at their declared claim level.

#### Acceptance Criteria

1. WHEN public/demo output is generated THEN HTML, CSS, JavaScript, JSON,
   manifests, README text, comments, generated paths, downloadable data, copy
   text, and diagnostics SHALL NOT contain raw snippets, raw facts, raw SQLite
   content, analyzer logs, raw SQL, config values, secrets, local absolute
   paths, raw remotes, raw URLs, hostnames, endpoint addresses, query strings,
   private repo names, private sample names, or generated scan directory names.
2. WHEN hidden/local output is generated THEN raw secrets, raw credentials,
   connection strings, raw SQL, raw snippets, local absolute paths, raw
   remotes, raw URLs, hostnames, endpoint addresses, private sample names, and
   production data SHALL still be rejected or omitted unless a later spec
   explicitly defines a safe hidden/local context for that value class.
3. WHEN the explorer omits, hashes, or category-labels a value because of the
   safety profile THEN it SHALL record a redaction, limitation, gap, or ledger
   row unless the omission is already covered by an existing row with the same
   rule ID and scope.
4. WHEN post-render safety validation fails THEN generation SHALL fail with a
   sanitized diagnostic containing rule ID, evidence tier, output artifact
   path, and category; it SHALL NOT print the raw unsafe value.
5. WHEN implementation adds new downloadable, embedded, or copyable data THEN
   tests SHALL prove those data surfaces are no less redacted than visible
   HTML.
6. WHEN implementation touches generated HTML navigation or JavaScript THEN it
   SHALL preserve local-only assets, no telemetry, no network calls, no remote
   fonts, no remote images, no remote scripts, and no remote CSS.

### Requirement 4: Deterministic Navigation And Absence States

**User Story:** As a reviewer, I want navigation labels to tell me what the
explorer can and cannot show without confusing missing data with evidence
absence.

#### Acceptance Criteria

1. WHEN the explorer renders section navigation THEN section anchors, section
   order, labels, support IDs, and sort order SHALL be deterministic.
2. WHEN a section has compatible input and credible coverage but no rows THEN
   the status MAY say no static evidence rows were found for that section, with
   rule IDs and coverage labels.
3. WHEN a section lacks compatible input THEN the status SHALL say
   unavailable, not provided, provenance-only, unsupported, or profile
   incompatible; it SHALL NOT say no evidence exists.
4. WHEN a section is future-supported but not implemented in the current slice
   THEN the status SHALL remain visibly partial and SHALL NOT be counted as
   complete explorer coverage.
5. WHEN a section links to rows, gaps, limitations, or rules THEN the links
   SHALL use stable anchors from closed section names or stable IDs.
6. WHEN JavaScript is disabled THEN the compatibility ledger, section statuses,
   gaps, limitations, rule IDs, and first deterministic evidence-row baseline
   SHALL remain inspectable.
7. WHEN JavaScript filtering or sorting is updated THEN it SHALL operate only
   on already-rendered safe fields and closed vocabularies.

### Requirement 5: Evidence-Bound Wording

**User Story:** As an engineer, I want the explorer to preserve TraceMap's
evidence boundaries so local navigation does not overstate what static evidence
proves.

#### Acceptance Criteria

1. WHEN scanner facts or path-adjacent rows are rendered without reducer output
   THEN the explorer SHALL use static evidence, candidate, evidence row,
   section status, gap, limitation, or needs-review wording.
2. WHEN reducer output is absent or unsupported THEN the explorer SHALL NOT
   say impacted, broken, affected, safe to deploy, reachable, used in
   production, complete coverage, or runtime behavior.
3. WHEN reducer output is added by a future reader THEN impact wording SHALL
   appear only on reducer-backed rows with reducer rule ID, evidence tier,
   supporting fact/path IDs, classification, coverage labels, and limitations.
4. WHEN profile or compatibility conflicts exist THEN the explorer SHALL label
   affected output partial rather than weakening the claim-level boundary.
5. WHEN a new conclusion-like row is introduced THEN it SHALL have a rule ID,
   evidence tier, support IDs, limitations, and rule catalog entry before
   implementation is considered complete.

### Requirement 6: Validation And Tests

**User Story:** As an implementer, I want a tight validation plan that proves
the slice without expanding product scope.

#### Acceptance Criteria

1. WHEN implementation lands THEN focused .NET tests SHALL cover ledger rows
   for supported rendered inputs, provenance-only inputs, missing inputs,
   unsupported JSON inputs, present-but-empty compatible inputs, and
   profile-incompatible inputs.
2. WHEN implementation lands THEN tests SHALL cover claim-level/profile
   conflicts without emitting unsafe raw values in HTML, JSON, manifest,
   README, or diagnostics.
3. WHEN implementation lands THEN tests SHALL cover deterministic ordering of
   ledger rows, section statuses, anchors, support IDs, and downloadable data.
4. WHEN implementation lands THEN tests SHALL cover public/demo strictness and
   hidden/local labeling for the slice.
5. WHEN implementation touches rendering THEN a generated-output smoke check
   SHALL generate an explorer from a sample scan and verify `index.html`,
   `data/explorer-data.json`, `data/explorer-manifest.json`, local asset
   references, no remote references, and expected ledger/status text.
6. WHEN implementation touches browser behavior or JavaScript THEN a desktop
   and mobile browser sanity check SHALL verify the generated output loads
   locally, has no console errors, preserves no-JavaScript baseline content,
   and makes no network calls beyond local static files.
7. WHEN implementation changes emitted rule IDs or rule limitations THEN
   `rules/rule-catalog.yml` and relevant docs SHALL be updated in the same PR.
8. WHEN the implementation adds ledger rows THEN tests SHALL prove the ledger
   remains inspectable without JavaScript and that HTML ledger rows and
   downloadable ledger data are no less redacted than each other.
9. WHEN generated output is tested for evidence-bound wording THEN tests SHALL
   assert that scanner-only explorer output does not contain forbidden impact
   or runtime-proof phrases such as impacted, broken, affected, reachable, safe
   to deploy, complete coverage, or runtime behavior except in explicit
   non-claim limitation text.
10. BEFORE the implementation PR is complete, `dotnet test` or a justified
   focused .NET test subset SHALL pass, `git diff --check` SHALL pass, and
   `./scripts/check-private-paths.sh` SHALL pass. If generated smoke output is
   produced outside the git worktree, the implementation SHALL additionally
   inspect that generated explorer output directly or use the explorer
   post-render validator because `check-private-paths.sh` covers tracked
   repository files only.
