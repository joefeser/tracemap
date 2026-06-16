# Legacy Sample Smoke Catalog Design

## Overview

The legacy sample smoke catalog is a small, deterministic metadata layer for
maintainers. It answers:

- Which neutral samples exercise which old-code validation families?
- Which commit or fixture version was checked out for the smoke?
- Which evidence families and rule IDs are expected?
- Which commands rerun the smoke using placeholders?
- Which claim level is safe today?
- Which related redacted artifacts exist elsewhere?

It deliberately does not answer whether code is impacted, reachable, used in
production, vulnerable, safe to release, or behaviorally exercised. It is a
catalog of validation expectations, not a scan result.

## Storage Model

Tracked catalog files should live under:

```text
docs/validation/legacy-sample-smoke-catalog/
  catalog.json
  catalog.md
  README.md
```

`catalog.json` is the source of truth. `catalog.md` is generated from the JSON
and used for maintainer review. `README.md` documents the workflow and should
not contain per-sample raw details that are absent from the JSON.

After the implementation adds and verifies a matching ignore rule,
operator-only inputs and scratch outputs live under:

```text
.tmp/legacy-sample-smoke-catalog/
  local-samples.json
  candidate-catalog.json
  validation-result.json
```

The implementation must prove this root is ignored with `git check-ignore`
before writing private operator data there. The spec does not authorize storing
private sample paths in that root until the ignore rule exists and passes.

The tracked location is a docs/validation handoff location, not a static site
page. Site specs may later consume public-safe evidence packs produced from
cataloged samples, but they should not scrape this catalog for claims.

## Relationship To Existing Work

The catalog composes nearby workflows without owning their artifacts:

| Workflow | Catalog relationship | Catalog must not do |
| --- | --- | --- |
| `legacy-codebase-validation` | Owns local sample execution and redacted validation summaries. Catalog entries may reference safe summary schema names and neutral IDs. | Copy raw scans, local manifests, raw summaries, file lists, analyzer logs, or private paths. |
| `legacy-baseline-regression-artifacts` | Owns redacted baseline snapshots and comparison reports. Catalog entries may say a sample has a baseline expectation. | Store baseline manifests inline or restate comparison rows. |
| `legacy-sample-evidence-pack` | Owns curated proof material for docs, site, demo, and review. Catalog entries may point to a pack ID and claim level. | Treat evidence packs as raw source records or duplicate pack sections. |
| Site specs | May consume promoted public-safe proof through separate site work. | Turn the catalog into a site page or marketing copy. |

## Catalog Schema

Suggested top-level shape:

```json
{
  "schemaVersion": "legacy-sample-smoke-catalog.v1",
  "catalogId": "legacy-sample-smoke-catalog",
  "generatedFrom": {
    "kind": "manual-reviewed-metadata",
    "toolVersion": "legacy-sample-smoke-catalog.v1",
    "generatedAt": "2026-06"
  },
  "safety": {
    "classification": "demo-safe",
    "validatorVersion": "legacy-sample-smoke-catalog-validator.v1",
    "redactionProfile": "catalog-metadata-only",
    "limitations": [
      {
        "code": "catalog-is-not-proof",
        "message": "Catalog entries describe expected static evidence families; downstream validated artifacts provide proof."
      }
    ]
  },
  "entries": []
}
```

`generatedAt` should be an explicit injected year-month for demo-safe or
public-safe output. Hidden/local-only drafts may use local timestamps only in
ignored `.tmp/` files and are excluded from byte-stability expectations.

## Entry Schema

Suggested entry shape:

```json
{
  "sampleLabel": "legacy-wcf-public-fixture",
  "displayName": "Legacy WCF public fixture",
  "claimLevel": "demo-safe",
  "source": {
    "classification": "synthetic-fixture",
    "identityKind": "neutral-label",
    "safeSourceAlias": "trace-map-fixture",
    "commitIdentity": {
      "kind": "fixture-version",
      "value": "legacy-wcf-fixture@2026-06",
      "shaPresent": false,
      "limitations": []
    }
  },
  "families": [
    {
      "familyId": "wcf-service-reference",
      "expectation": "required",
      "expectedRuleIds": [
        "legacy.wcf.metadata.v1",
        "legacy.wcf.mapping.v1"
      ],
      "expectedEvidenceTiers": [
        "Tier2Structural",
        "Tier3SyntaxOrTextual"
      ],
      "expectedCoverageLabels": [
        "Level1SemanticAnalysisReduced"
      ],
      "expectedExtractors": [
        "csharp.syntax",
        "xml.config"
      ],
      "statesAllowed": [
        "observed",
        "reduced",
        "analysis-gap"
      ],
      "limitations": [
        {
          "code": "static-metadata-only",
          "message": "Static WCF metadata does not prove service reachability or runtime calls."
        }
      ]
    }
  ],
  "validation": {
    "commandTemplates": [
      {
        "name": "scan-sample",
        "template": "tracemap scan --repo <sample-root> --out <scan-output>",
        "mode": "operator-local",
        "timeoutBucket": "medium",
        "artifactSizeBucket": "medium",
        "expectedArtifacts": [
          "scan-manifest",
          "facts-ndjson-present",
          "index-sqlite-present",
          "report-md-present",
          "analyzer-log-present"
        ]
      }
    ],
    "gates": [
      "catalog validate",
      "./scripts/check-private-paths.sh",
      "git diff --check"
    ]
  },
  "relationships": [
    {
      "spec": "legacy-codebase-validation",
      "artifactKind": "redacted-validation-summary",
      "schemaVersion": "legacy-codebase-validation-summary.v1",
      "safeArtifactId": "legacy-wcf-public-fixture-summary",
      "claimLevel": "demo-safe"
    }
  ],
  "limitations": [
    {
      "code": "catalog-not-raw-output",
      "message": "This entry records expected smoke coverage only; it is not raw scan evidence."
    }
  ]
}
```

The implementation may adjust field names while preserving these concepts and
stable, additive evolution.

## Closed Vocabularies

### Claim Level

| Value | Meaning |
| --- | --- |
| `hidden` | Entry is private, local-only, missing proof, unreviewed, or operator-only. It can guide maintainers but supports no public claim. |
| `demo-safe` | Entry uses reviewed synthetic, public-doc, or neutral demo proof that is safe for demos but not necessarily public product claims. |
| `public-safe` | Entry has reviewed public or synthetic source identity, pinned commit or fixture version, public-safe proof, passing validation, and safe downstream evidence. |

The top-level catalog classification is the lowest safe classification across
included entries. A catalog containing any `hidden` entry must itself remain
hidden or local-only unless the tracked output omits those entries. Render and
promote commands must not silently drop entries. If a candidate catalog contains
hidden entries and the requested tracked output level is `demo-safe` or
`public-safe`, the command fails with a sanitized diagnostic unless the caller
supplies `--minimum-entry-claim-level <demo-safe|public-safe>` during render to
produce a new output set without those entries. `--minimum-entry-claim-level
demo-safe` includes `demo-safe` and `public-safe` entries.
`--minimum-entry-claim-level public-safe` includes only `public-safe` entries.
If no entries remain after filtering, render fails with a sanitized diagnostic.
The validator rejects any tracked catalog whose `safety.classification` is
higher than the least-safe included entry. Individual entries may be safer than
the top-level classification; for example, a demo-safe catalog rendered with
`--minimum-entry-claim-level demo-safe` may contain both `demo-safe` and
`public-safe` entries. The top-level classification remains the floor for the
included set, not a cap that rejects safer entries.

### Source Classification

| Value | Meaning |
| --- | --- |
| `synthetic-fixture` | Checked-in or generated fixture designed for validation. |
| `public-repo` | Public repository source reviewed for safe identity disclosure. |
| `public-archive` | Public archived source reviewed for safe identity disclosure. |
| `public-doc-sample` | Sample copied or generated from public documentation under allowed terms. |
| `private-local` | Private source available only to an operator. |
| `operator-local` | Source path or checkout exists only on one machine. |
| `unknown` | Source safety or identity has not been reviewed. |

### Commit Identity Kind

| Value | Public tracked use | Meaning |
| --- | --- | --- |
| `public-sha` | Allowed only for reviewed public sources | Raw checked-out commit SHA. |
| `fixture-version` | Allowed | Stable fixture version or fixture tag. |
| `redacted-sha256` | Not allowed in tracked output | Reserved for ignored local-only `.tmp/` drafts if a future hashing policy defines safe inputs. |
| `category-only` | Allowed for hidden or demo-safe entries only | Commit exists or is absent as a category, with no raw value. |
| `local-only` | Not allowed in tracked public/demo output | Raw or local identity exists only in ignored `.tmp/` output. |

The tracked catalog schema must not allow `redacted-sha256` or `local-only`.
Those values belong only to an ignored local-draft schema variant under `.tmp/`
until a future spec defines a safe hashing policy for commit identities and
local-only identity handling.

Tracked `commitIdentity.kind` enum:

```json
["public-sha", "fixture-version", "category-only"]
```

Ignored local-draft catalog schema variants may additionally allow
`redacted-sha256` and `local-only`, but those values are schema errors in
tracked catalog files before policy validation runs.

Public-safe catalog entries require a pinned `public-sha` for reviewed public
sources or a `fixture-version` for synthetic/public-doc fixtures. If a private
SHA is observed, use `shaPresent: true` with `category-only`, keep the entry no
higher than `demo-safe`, and do not expose or hash the private SHA in tracked
output. Do not hash secret-like, low-entropy, enumerable, or source-text values.

### Evidence Family

Initial family IDs:

| Family ID | Purpose |
| --- | --- |
| `wcf-service-reference` | Service reference metadata, generated client shape, or project reference patterns. |
| `wcf-config-endpoint-shape` | WCF config endpoint and binding shape without endpoint values. |
| `remoting-registration` | .NET Remoting registration, activation, or marshal patterns. |
| `remoting-channel-config` | Remoting channel config shape without raw endpoint/config values. |
| `webforms-event-binding` | WebForms event handler wiring from markup or code-behind. |
| `webforms-markup-codebehind` | Markup/code-behind association and fallback extraction. |
| `dbml-linq-to-sql` | DBML metadata shape and generated data context surfaces. |
| `edmx-entity-framework` | EDMX metadata shape without raw SQL or connection values. |
| `typed-dataset` | Typed DataSet XSD/designer metadata shape. |
| `legacy-sql-or-query-surface` | SQL/query surface categories without raw SQL. |
| `build-environment-diagnostics` | SDK, framework, MSBuild, restore, or toolchain diagnostics. |
| `msbuild-project-load-failure` | Project load failure coverage labeling. |
| `packages-config` | `packages.config` package metadata counts or categories. |
| `binding-redirects` | Binding redirect shape and count categories. |
| `large-repo-stress` | Duration, size, and count buckets for huge repo smoke runs. |
| `fallback-syntax-scan` | Syntax/text fallback coverage when semantic analysis fails. |
| `analysis-gap-reporting` | Rule-backed gaps and reduced coverage states. |

### Relationship Artifact Kind

| Value | Meaning |
| --- | --- |
| `redacted-validation-summary` | Public-safe or demo-safe summary from `legacy-codebase-validation`. |
| `redacted-baseline-snapshot` | Redacted baseline manifest or summary from `legacy-baseline-regression-artifacts`. |
| `redacted-comparison-report` | Redacted baseline comparison report from `legacy-baseline-regression-artifacts`. |
| `evidence-pack-summary` | Public-safe or demo-safe summary reference from `legacy-sample-evidence-pack`. |

### Command Input Kind

| Value | Meaning |
| --- | --- |
| `legacy-validation-summary` | Already redacted validation summary input. |
| `legacy-baseline` | Already redacted legacy baseline input. |
| `legacy-baseline-comparison` | Already redacted comparison report input. |
| `legacy-evidence-pack` | Already validated evidence-pack summary input. |
| `catalog-json` | Existing catalog JSON used for validation or render. |

### Timeout Bucket

| Value | Meaning |
| --- | --- |
| `small` | Expected to finish quickly on a maintainer machine. |
| `medium` | Expected to require a normal smoke timeout. |
| `large` | Expected to require an extended smoke timeout. |
| `extra-large` | Expected to require explicit operator approval or a long timeout. |
| `operator-defined` | Bounds are supplied only in ignored local configuration. |

### Artifact Size Bucket

| Value | Meaning |
| --- | --- |
| `small` | Small redacted or generated output. |
| `medium` | Normal smoke output size. |
| `large` | Large output requiring explicit bounds. |
| `extra-large` | Very large output requiring operator approval. |
| `operator-defined` | Size bounds are supplied only in ignored local configuration. |

### Extractor Gap Code

| Value | Meaning |
| --- | --- |
| `extractor-not-available` | Expected extractor is not present in the current build. |
| `extractor-deferred` | Extractor support is planned but not implemented. |
| `extractor-unsupported` | Extractor is intentionally unsupported for this family. |
| `extractor-reduced-coverage` | Extractor ran with fallback or reduced semantic coverage. |

### Expectation State

| Value | Meaning |
| --- | --- |
| `observed` | Expected static evidence was observed in a redacted proof artifact. |
| `expected-not-yet-run` | Entry declares intended coverage but no validated run exists. |
| `reduced` | Evidence was observed with reduced coverage or fallback extraction. |
| `analysis-gap` | Tooling could not prove or disprove the family and emitted a gap. |
| `unsupported` | The family is intentionally not supported by current extractors. |
| `deferred` | Validation is planned but out of current implementation scope. |
| `truncated` | Scan or artifact generation exceeded configured bounds. |
| `rejected` | Unsafe source or output prevented tracked catalog use. |

`expected-not-yet-run`, `analysis-gap`, `unsupported`, `deferred`,
`truncated`, and `rejected` must not be rendered as absence of legacy behavior.

## Redaction Rules

Never include in tracked catalog files:

- local absolute paths, home fragments, drive prefixes, or machine names
- raw repository remotes or URLs
- private repository, organization, username, hostname, project, or branch names
- raw SQL, stored procedure text, query snippets, connection strings, or config
  values
- endpoint addresses, credentials, secrets, tokens, certificates, private keys,
  package source credentials, or bearer strings
- source snippets or source-like fragments
- analyzer log lines, raw diagnostics, stack traces, or raw tool output
- raw scan artifacts, raw evidence packs, raw baseline manifests, raw
  validation summaries, SQLite rows, or file lists
- unescaped Markdown supplied from labels, commands, diagnostics, or metadata

Allowed tracked catalog data:

- neutral sample labels and display names
- reviewed safe source aliases that are not raw remotes
- public commit SHAs only for reviewed public sources
- fixture versions
- category-only commit proof with `shaPresent: true`
- evidence family IDs, exact expected rule IDs, evidence tiers, extractor
  IDs, coverage labels, limitation codes, and expectation states
- command templates containing placeholders only
- safe artifact classes and related schema names
- validation command names and gate names
- count, timeout, and artifact-size buckets

## Command Templates

Catalog command templates should show the shape of validation without leaking a
developer machine:

```bash
tracemap scan --repo <sample-root> --out <scan-output>
tracemap legacy-codebase-validation summarize --input <scan-output> --out <redacted-summary>
tracemap evidence-pack create --input <redacted-summary> --input-kind legacy-validation-summary --label <sample-label> --claim-level <claim-level> --date <YYYY-MM> --out <pack-output>
catalog validate --catalog <catalog-json> --expected-claim-level <claim-level>
```

If implementation chooses a script fallback such as
`scripts/legacy-sample-smoke-catalog.mjs`, `implementation-state.md` must record
the blocker and migration plan to a first-class CLI command.

Literal option values are permitted only for booleans, fixed enumerations from
closed vocabularies, and fixed command switches that exist in the current CLI.
The catalog must not document speculative scan options. String option values
that can carry identity or
operator-specific data, including labels, names, repo references, branch names,
artifact IDs, source identifiers, and dates more precise than `YYYY-MM`, must be
angle-bracket placeholders in tracked catalog templates. A literal
`--label my-internal-project` is invalid even though it is not a path, and a
literal `--date 2026-06` is invalid in a stored template even though `2026-06`
is a valid invocation value outside the catalog. Templates should use
`--date <YYYY-MM>`.

`--input-kind legacy-validation-summary` is valid because `input-kind` values
come from the closed Command Input Kind vocabulary above. Unknown input kinds
are validation errors.

## Validation Design

The catalog validator should:

1. Validate JSON schema and closed vocabularies.
2. Confirm deterministic ordering of entries, families, relationships,
   limitations, commands, and maps.
3. Ensure `catalog.md` is generated from `catalog.json`. Stale detection uses a
   deterministic content hash of canonical `catalog.json` embedded as a
   sentinel HTML comment on the first line of generated Markdown using the
   exact format `<!-- catalog-json-sha256: <64 lowercase hex chars> -->`;
   validation re-hashes the current JSON and fails if the sentinel differs.
4. Reject hidden entries in demo-safe or public-safe tracked output unless an
   explicit render filter has already omitted them from the candidate output.
5. Validate labels and safe aliases.
6. Validate commit identity rules for source classification and claim level.
7. Validate every family has rule IDs or rule patterns, evidence tiers,
   coverage labels, extractor IDs or explicit extractor gaps, and limitations.
8. Validate command templates contain only approved placeholders for paths.
9. Validate relationship references use safe schema names and neutral artifact
   IDs only.
10. Scan every JSON string and generated Markdown text for unsafe content and
   prohibited claim wording.
11. Emit sanitized diagnostics that include file path, JSON pointer or Markdown
   section plus line number, and rejection category without echoing unsafe
   values.

The prohibited claim check should reject wording that implies runtime execution,
production usage, service reachability, SQL execution, vulnerability/security
status, release approval, business impact, customer impact, or reducer impact.

Rule IDs are exact catalog IDs and must exist in `rules/rule-catalog.yml` when
they refer to current TraceMap rules. This v1 schema does not use implicit glob,
prefix, or regex matching inside `expectedRuleIds`; a future schema may add a
separate `expectedRuleIdPatterns` field with explicit matching semantics. Rule
IDs are still ordinary strings for safety purposes. The validator must scan them
for private names, raw SQL/table identifiers, snippets, and prohibited wording
rather than assuming rule identifiers are always safe. `displayName` is also
scanned for redaction and prohibited-claim violations, but it is not constrained
to the `sampleLabel` syntax because it may contain spaces.

## Determinism

For public-safe and demo-safe output:

- Require an explicit injected `--date YYYY-MM` or fixture-pinned date.
- Commit `catalog.json` with the date supplied at the last reviewed render; do
  not re-stamp the catalog on every validation or read.
- On the first tracked write, require the caller to supply `--date YYYY-MM`
  explicitly and validate that `generatedAt` matches that value; there is no
  implicit wall-clock fallback for bootstrap.
- Treat render as the only date-update operation: render requires
  `--date YYYY-MM`, writes that value into `generatedAt`, regenerates
  `catalog.md`, and produces a normal reviewable diff. Validation does not
  update dates and does not accept a new date to override an existing catalog;
  it verifies the embedded `generatedAt` value, schema, deterministic ordering,
  and Markdown sentinel.
- If an existing tracked `catalog.json` already has the same `generatedAt` value
  as the supplied render `--date`, render is idempotent and produces no date
  diff. If the values differ, render updates `generatedAt`, regenerates
  Markdown, and leaves a normal reviewable diff.
- Define canonical JSON for hashing and byte-stability as UTF-8 without BOM,
  LF line endings, two-space indentation, a final newline, object keys sorted by
  ordinal string comparison at every level, and arrays ordered by the schema's
  deterministic rules. The Markdown sentinel hashes the full canonical
  `catalog.json` byte sequence.
- Sort entries by `sampleLabel`.
- Sort families by `familyId`.
- Sort limitations, relationships, commands, rule IDs, evidence tiers, coverage
  labels, extractor IDs, and validation gates deterministically.
- Serialize JSON with stable indentation and newline behavior.
- Generate Markdown from JSON in the same order.
- Avoid wall-clock timestamps, local output paths, environment values, and
  absolute paths.

## Public Claim Guidance

Catalog claim levels are conservative:

- `hidden`: default for local/private/operator-only or unvalidated entries.
- `demo-safe`: enough for internal demos using safe metadata, not public product
  claims.
- `public-safe`: requires reviewed source identity, pinned version, safe
  catalog validation, and separate public-safe proof material.

Even public-safe catalog entries should say that a sample is expected to
exercise static evidence families. Public docs or site claims should rely on
promoted evidence packs or other reviewed proof, not the catalog alone.

## Relationship Validation

Relationship references are validated by closed-vocabulary artifact kind,
schema-name pattern, neutral artifact ID syntax, and claim level. Catalog
validation does not require the referenced sibling spec to be implemented or the
referenced artifact to exist. If a referenced schema is not yet published, the
relationship state is `deferred` and the entry cannot use that relationship as
public-safe proof.

`safeArtifactId` uses the same safety shape as `sampleLabel`: lowercase
kebab-case ASCII, no path separators, URI schemes, `.git`, `@` identities,
Windows drive prefixes, home fragments, hostnames, organization/user patterns,
private-looking tokens, branch names, or secret-like values.

## Promotion And Overwrite Semantics

Promotion writes only to the approved tracked root
`docs/validation/legacy-sample-smoke-catalog/`. If destination files already
exist, promotion fails unless `--force` is supplied. `--force` only permits
replacing existing catalog files after all validation, redaction,
generated-output, tracked-root, ignored-destination, claim-level, and
private-path gates pass. It never changes classification, omits entries, or
bypasses safety checks.

Render filtering with `--minimum-entry-claim-level` is separate from promotion.
It creates a new candidate output set from the source catalog, omits entries
below the requested claim level, and computes `safety.classification` from the
remaining entries. It does not mutate the original catalog in place. Promotion,
including promotion with `--force`, may copy only an already-rendered,
already-validated output set and must not perform filtering itself.

`--dry-run` applies to render and promote. Dry-run must execute schema,
redaction, generated-output, tracked-root, claim-level, and private-path gates
that can be evaluated without writing tracked files, then report planned files
and sanitized rejection categories without creating or replacing catalog files.
Validate does not need `--dry-run` because it is read-only.

## Implementation Notes

- This spec is spec-only. The implementation PR should create the tracked docs
  location, local ignore rule if needed, schema/tooling, generated Markdown, and
  tests.
- Keep catalog rule IDs documented before any generated catalog rows cite them.
- Reuse existing stable hash, safe path, generated-output sentinel, and
  private-path validation helpers where practical.
- Do not broaden scanner output or alter reducer behavior to satisfy catalog
  expectations. Gaps are valid catalog states.
