# Static HTML Evidence Explorer Follow-Up Design

## Overview

This follow-up adds a deterministic compatibility ledger and safety profile
conflict hardening layer to the local static HTML evidence explorer. The goal
is to make the generated explorer clearer and safer before adding broader
artifact readers for surfaces, paths, reducer output, or richer report JSON.

The implementation should remain a renderer over generated TraceMap artifacts:

1. Discover generated artifacts from the selected input directory.
2. Classify each artifact and section with a safe compatibility status.
3. Reconcile selected safety profile, artifact claim metadata, schema support,
   commit/source provenance, and safety omissions.
4. Render the ledger and section statuses deterministically in HTML and safe
   downloadable data.
5. Validate generated output for safety and local-only assets.

No part of this follow-up should rescan source code, query live repositories,
read raw source files, derive new impact conclusions, call a model, use
embeddings, or add a hosted/public site surface.

## Current Context

The predecessor spec `.kiro/specs/static-html-evidence-explorer/` is marked
`implemented-pr1-with-follow-ups`. It records that the first explorer slice was
implemented and that follow-up slices have already added section status rows,
safety/redaction visibility, richer gap/limitation/rule/evidence metadata, and
compatible rule catalog rendering.

This spec was drafted against `origin/dev` at:

```text
4b5844ff07199969eacd040e9383037d0b266d49 Promote dev to main after legacy .NET v0 completion (#390)
```

Live code observed on `origin/dev`:

- `src/dotnet/TraceMap.Reporting/StaticHtmlEvidenceExplorer.cs`
  implements `tracemap explorer generate` output for:
  - `scan-manifest.json` as supported provenance/coverage input;
  - `facts.ndjson` as supported safe evidence-row input;
  - `index.sqlite` as supported provenance-only input;
  - `report.md` as supported provenance-only input;
  - `rule-catalog.yml` or `rules/rule-catalog.yml` as a bounded compatible
    rule catalog input;
  - other top-level JSON artifacts as unsupported.
- `src/dotnet/tests/TraceMap.Tests/StaticHtmlEvidenceExplorerTests.cs`
  covers local bundle generation, no-network validation, generated-file
  collision safety, hidden-local labeling, unsupported JSON, legacy artifact
  compatibility, deterministic output, and compatible rule catalog cases.
- `rules/rule-catalog.yml` already contains explorer rules for unsupported
  schema, provenance conflict, missing commit, redacted display values,
  omitted unsafe values, catalog unavailable, no-network assets, partial
  sections, section status, generated-file stale guard, user-file collision,
  and unsafe generated value rejection.
- `docs/STATIC_HTML_EVIDENCE_EXPLORER.md` documents the current command,
  output layout, safety profiles, manifest schema, partial scope, and
  first-slice limitations.

Relevant current limitations:

- Claim-level conflict detection across multiple compatible structured
  artifacts is explicitly deferred.
- Surfaces, paths, and reducer-backed results are counted as zero and shown as
  not provided or not rendered in the current slice.
- `index.sqlite` and `report.md` are hashed for provenance only.
- Unsupported JSON report artifacts are not parsed.
- The current section status table is useful, but it is not yet a full
  artifact/section compatibility ledger with profile conflict statuses.

## Selected Slice

PR 1 for this follow-up should implement only:

```text
Explorer compatibility ledger and safety profile conflict hardening.
```

The slice should add a safe data model and rendering for artifact/section
compatibility and profile reconciliation. It should not add new source
analysis, a broad UI rebuild, SQLite readers, reducer readers, graph
visualization, or public site changes.

## Non-Goals

- No public `tracemap.tools` changes.
- No new static site spec and no edits under `site/`.
- No hosted explorer, backend, remote assets, service worker, analytics,
  telemetry, CDN, remote fonts, remote scripts, or external images.
- No source repository scanning, Roslyn invocation, language-adapter changes,
  or runtime analysis.
- No LLM calls, embeddings, vector database, AI ranking, semantic search, or
  prompt-based classification.
- No proof of runtime reachability, impact, production use, safety to deploy,
  ownership, vulnerability, or complete product coverage.
- No raw snippets, raw SQL, config values, secrets, local absolute paths, raw
  remotes, raw URLs, hostnames, private labels, or raw generated artifact
  paths in generated explorer output.

## Data Model

Add a compatibility ledger to `ExplorerData` or an equivalent versioned safe
view model. The exact record names may follow local conventions, but the model
should carry these concepts:

```csharp
public sealed record ExplorerCompatibilityRow(
    string RowId,
    string SubjectKind,
    string SubjectId,
    string SafeLabel,
    string CompatibilityStatus,
    string RuleId,
    string EvidenceTier,
    string CoverageLabel,
    string Scope,
    IReadOnlyList<string> SupportIds,
    IReadOnlyList<string> LimitationIds,
    string Message);
```

Closed `SubjectKind` values should include at least:

- `artifact`
- `section`
- `safety-profile`
- `claim-level`

Closed `CompatibilityStatus` values should include at least:

- `rendered-compatible`
- `compatible-empty`
- `provenance-only`
- `not-provided`
- `unsupported-schema`
- `unsupported-artifact`
- `profile-incompatible`
- `safety-omitted`
- `partial`
- `compatible`

The ledger status vocabulary must stay distinct from existing coverage and
section status values. In particular, ledger rows SHALL NOT use the existing
coverage status literal `available`; use `compatible`, `rendered-compatible`,
or `compatible-empty` as appropriate. Tests must access the ledger field by
name and must not infer ledger state by searching for a generic `status` value.

Subject IDs must use closed deterministic conventions:

- artifact subjects reuse the existing safe `ExplorerInputArtifact.ArtifactId`
  values, such as `artifact:scan-manifest`, `artifact:facts-ndjson`,
  `artifact:sqlite-index`, `artifact:markdown-report`, and
  `artifact:rule-catalog`;
- unsupported artifact subjects reuse the already-hashed unsupported artifact
  IDs, such as `artifact:unsupported-json:<hash>`;
- section subjects reuse the existing `ExplorerSectionStatus.SectionId`
  strings, such as `overview`, `sources`, `artifacts`, `evidence-rows`,
  `surfaces`, `paths`, `reducer-results`, `rules`, and `redactions`;
- safety profile subjects use
  `safety-profile:<normalized-profile-name>`, where the normalized profile is
  `public-demo` or `hidden-local`;
- claim-level subjects use `claim-level:<claim-level-token>` for known closed
  tokens and `claim-level:unknown` when no artifact claim metadata exists.

Ledger `SafeLabel` and `Message` fields should be closed strings assembled by
the explorer, not user-derived artifact text. If a future implementation needs
user-derived text in a ledger field, it must pass through the existing
generated-output safety policy and tests must prove HTML/data parity.

Rows must be sorted deterministically by `SubjectKind`, then `SubjectId`, then
`CompatibilityStatus`, then `RuleId`, all ordinal.

Adding a top-level compatibility ledger field requires an explorer schema
version bump to `tracemap-static-html-evidence-explorer.v2`. The implementation
PR must update manifest/data schema constants, docs, focused tests, and any
schema-version assertions in the same change. This conservative bump avoids
depending on unknown-field tolerance in downstream explorer data consumers.

## Profile Reconciliation

The selected output safety profile is the controlling policy:

- `public-demo`, `demo-safe`, and `public-safe` normalize to the existing
  strict public/demo rendering path.
- `hidden-local`, `hidden`, and `local-only` normalize to hidden/local output
  with visible labeling.

Artifact claim metadata should be read only from generated artifacts that
already expose safe structured claim/profile fields. If an artifact has no
claim metadata, use `unknown` and render only values safe under the selected
profile.

Current live artifact contracts do not expose a readable claim-level or safety
profile field independent of the selected explorer output profile. In PR 1,
real production conflict handling should therefore be limited to dimensions
available from currently parsed artifacts, primarily commit SHA disagreement
between `scan-manifest.json` and `facts.ndjson`. Claim-level, safety-profile,
schema-version, and source-identity conflict handling should be implemented as
forward-compatible hooks only when the code has safe structured fields to read;
otherwise the explorer should emit the unknown-metadata limitation path and no
conflict row.

Profile names and claim-level names are separate namespaces. The existing
`public-safe` string can appear as a safety-profile alias at the CLI boundary
and as a claim-level token in generated output; implementation must normalize
profiles first and compare profile namespace values only with profile namespace
values.

PR 1 conflict-dimension map:

| Conflict dimension | Current parsed source | PR 1 behavior |
| --- | --- | --- |
| Commit SHA | `scan-manifest.json` and `facts.ndjson` fact rows | Implement against real inputs with `commit-conflict` or equivalent closed kind. |
| Missing commit | `scan-manifest.json` and `facts.ndjson` fact rows | Already supported; preserve or represent in the ledger. |
| Claim level | No current input artifact field independent of selected output profile | Treat as `unknown`; emit limitation only when interpretation is affected. Future hook only. |
| Safety profile | No current input artifact field independent of selected output profile | Treat as `unknown`; future hook only. |
| Source identity | Current safe source is a generated stable source ID plus safe commit SHA | Do not infer conflicts from raw names, paths, or remotes. Future hook only. |
| Schema version | Supported file kinds are classified by reader, but unsupported JSON is not parsed | Use existing unsupported-schema rows; future structured schema conflicts require a reader. |

PR 1 should emit one `safety-profile` ledger row for the normalized selected
profile and one `claim-level:unknown` row when no compatible artifact exposes
independent claim-level metadata. The unknown claim row is a limitation/status
record, not a conflict. It must not create a `profile-incompatible`,
`claim-level-conflict`, or equivalent conflict row unless a future compatible
artifact exposes safe structured metadata that actually conflicts.

Suggested conflict policy:

| Condition | Public/demo behavior | Hidden/local behavior | Rule |
| --- | --- | --- | --- |
| artifact explicitly hidden/local only | stop or omit affected artifact | render only safe fields and label hidden/local | existing `explorer.input.provenance-conflict.v1` or new cataloged rule |
| artifact has unknown claim level | render safe fields with limitation | render safe fields with limitation | existing provenance/partial rule |
| artifacts disagree on commit SHA within same rendered section | mark section partial or stop if no safe partition exists | same | `explorer.input.provenance-conflict.v1` |
| artifacts disagree on schema version or source identity | mark affected section partial or unsupported | same | `explorer.input.provenance-conflict.v1` |
| selected public/demo profile would be weakened by any artifact | stop or omit affected artifact | not applicable | existing or newly cataloged conflict rule |

If the implementation uses `explorer.input.provenance-conflict.v1`, include a
closed `conflictKind` through gap kind, limitation kind, row status, or safe
metadata so claim-level, profile, commit, schema, and source identity conflicts
are distinguishable. If a new rule is cleaner, add it to
`rules/rule-catalog.yml` first with limitations.

## Rendering

The ledger may be rendered as a new table or folded into the existing
`Coverage`/section status area. Keep the change conservative:

- reuse current layout, typography, and generated file set;
- avoid broad visual redesign;
- keep no-JavaScript baseline useful;
- keep deterministic section anchors;
- render only safe row fields;
- include support IDs and rule IDs in the visible table;
- mirror the same safe rows in `data/explorer-data.json`.

Recommended section placement:

1. Evidence Overview
2. Coverage
3. Compatibility Ledger
4. Sources
5. Artifacts
6. Gaps
7. Limitations
8. Safety & Redactions
9. Rules
10. Evidence Rows
11. About This Local Explorer

If implementation chooses to avoid a new section, the existing `Coverage`
section must still include equivalent ledger detail and tests must prove all
required statuses are visible.

The ledger is additive to existing `sectionStatuses`; it should not replace or
silently reinterpret current section status rows. If a new Compatibility
Ledger section is added, update the existing pinned section-order test before
implementing rendering so CI reflects the intended new order from the start.
If ledger rows are folded into Coverage instead, keep the current section order
intact and add a test that ledger rows appear inside Coverage while remaining
distinguishable from `sectionStatuses`.

## Safety

The ledger is itself a generated artifact surface and must pass the same safety
requirements as the rest of the explorer:

- no raw local absolute paths;
- no raw remotes, URLs, hosts, endpoint addresses, or query strings;
- no raw SQL, config values, connection strings, credentials, secrets, raw
  snippets, raw facts, raw SQLite content, analyzer logs, private repo names,
  private sample names, or generated scan directory names;
- no unsafe value in diagnostics;
- no remote asset fallback;
- no JavaScript network calls or telemetry.

Do not hash high-risk raw secrets into generated output. Prefer omission,
category labels, or hard failure according to the existing safety policy. Hash
only values already permitted by the current generated-output safety approach,
such as normalized safe placeholders for display values.

## Rule Catalog

Before implementation emits any new rule ID, gap kind, limitation kind, or
validation failure, update `rules/rule-catalog.yml` with:

- rule ID;
- title;
- description;
- evidence tier;
- emitted row types;
- limitations;
- explicit non-claims.

Existing explorer rules may be reused when their catalog limitations cover the
new emitted row. If reused, tests should verify the row's `gapKind`,
`limitationKind`, `CompatibilityStatus`, or equivalent field distinguishes the
case.

If `explorer.input.provenance-conflict.v1` is reused for any subtype that the
current catalog calls deferred, update that catalog limitation in the same PR
before emitting the subtype. Add a focused test or catalog assertion so an
emitted conflict kind is not still described as deferred.

Potential existing rules:

- `explorer.input.provenance-conflict.v1`
- `explorer.input.unsupported-schema.v1`
- `explorer.render.partial-section.v1`
- `explorer.render.omitted-unsafe-value.v1`
- `explorer.render.section-status.v1`
- `explorer.validation.unsafe-value-rejected.v1`

## Implementation Seams

Likely files for the implementation PR:

- `src/dotnet/TraceMap.Reporting/StaticHtmlEvidenceExplorer.cs`
- `src/dotnet/tests/TraceMap.Tests/StaticHtmlEvidenceExplorerTests.cs`
- `docs/STATIC_HTML_EVIDENCE_EXPLORER.md`
- `rules/rule-catalog.yml` only if emitted rules/limitations change

Avoid touching `site/` and avoid unrelated product code.

## Validation

Expected focused validation for the implementation PR:

```bash
dotnet test src/dotnet/TraceMap.sln --filter StaticHtmlEvidenceExplorerTests
```

Broader validation when the implementation touches shared safety helpers or
public report contracts:

```bash
dotnet test src/dotnet/TraceMap.sln
```

Required spec/repo hygiene:

```bash
git diff --check
./scripts/check-private-paths.sh
```

If rendering or JavaScript changes, run a generated explorer smoke:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- scan \
  --repo samples/modern-sample \
  --out /tmp/tracemap-explorer-ledger-smoke/scan
dotnet run --project src/dotnet/TraceMap.Cli -- explorer generate \
  --input /tmp/tracemap-explorer-ledger-smoke/scan \
  --out /tmp/tracemap-explorer-ledger-smoke/explorer
```

Then verify generated `index.html`, `data/explorer-data.json`,
`data/explorer-manifest.json`, local asset references, no remote references,
and expected compatibility/status text.

Because `./scripts/check-private-paths.sh` scans tracked repository files only,
implementation smoke checks should also inspect the generated explorer output
directory directly, or use the explorer's post-render validator, to confirm no
private paths or unsafe values leaked into the generated artifacts.

If JavaScript behavior changes, add a desktop and mobile browser sanity check
against the generated static output and record whether console/network checks
passed. Browser checks may be explicitly deferred only when implementation does
not touch rendering or JavaScript.

## Compatibility And Rollback

The new ledger should be additive to the explorer schema where possible. If a
schema version bump is required, document it in `docs/STATIC_HTML_EVIDENCE_EXPLORER.md`
and keep existing fields stable.

If a compatibility conflict is found in real generated artifacts after release,
the safe fallback should be partial/unavailable labeling rather than raw
artifact rendering or claim weakening.
