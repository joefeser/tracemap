# Site Demo Summary Refresh Design

Status: implemented
Readiness: implemented
Public claim level: hidden

## Overview

The future implementation should add an explicit refresh workflow that converts
public demo output into a small committed fixture for static site pages. The
fixture becomes the stable site-facing data source; raw generated artifacts stay
local-only.

The workflow has three parts:

1. A local maintainer runs `./scripts/demo-public.sh <ignored-out>`.
2. A refresh command reads approved public-safe output and writes a scrubbed
   fixture under `site/src/`.
3. A validation command checks the fixture and affected page references during
   normal site validation.

## Proposed Files

- `site/src/_data/demo-public-summary.json`: committed public-safe fixture that
  stays out of `site/dist/` because `site/scripts/build.mjs` skips
  underscore-prefixed source directories.
- `site/scripts/refresh-demo-summary.mjs`: explicit maintainer refresh command.
- `site/scripts/validate-demo-summary.mjs`: cheap fixture and hard-coded page
  reference validation, possibly called by `site/scripts/validate.mjs`.
- `site/scripts/refresh-demo-summary.test.mjs`: focused tests for scrubbing,
  allowed fields, and rejection behavior.

Names may change during implementation if the existing site script layout
suggests a better local convention, but the fixture must stay under `site/src/`
and the raw demo output must stay outside the static site.

The underscore exclusion in `site/scripts/build.mjs` applies to directories,
not files. The fixture must stay inside `site/src/_data/` or another
underscore-prefixed directory; a bare underscore-prefixed file directly under
`site/src/` would still be copied.

## Fixture Shape

Suggested top-level shape:

```json
{
  "version": "1.0",
  "publicClaimLevel": "demo",
  "source": {
    "generator": "scripts/demo-public.sh",
    "demoSummary": {
      "version": "1.0",
      "outputRootHash": "path-hash:000000000000000000000000"
    }
  },
  "sections": []
}
```

The fixture `source.demoSummary.version` and
`source.demoSummary.outputRootHash` values are copied from the real top-level
`demo-summary.json` fields `version` and `outputRootHash`. They are nested in
the fixture only to keep provenance metadata separate from the fixture's own
schema `version`.

Suggested section fields:

- `id`
- `name`
- `status`
- `classification`
- `evidenceTier`
- `ruleIds`
- `coverage`
- `counts`
- `reason`
- `artifacts`

The refresh maps from the real `demo-summary.json` section fields:

| Source field | Fixture field | Notes |
| --- | --- | --- |
| `name` | `name` and `id` | `id` comes from a checked mapping table keyed by known `name` values. Unknown names fail validation. |
| `status` | `status` | Preserve source value. |
| `classification` | `classification` | Preserve source value; do not silently drop it. |
| `evidenceTier` | `evidenceTier` | Preserve source value. |
| `ruleIds` | `ruleIds` | Preserve source array and require at least one rule ID. |
| `reportCoverage` | `coverage` | Alias is allowed only with tests documenting the mapping. |
| `artifactPaths` | `artifacts` | Alias is allowed only for paths that pass validation and public-safe allowlist checks. Raw local-only paths such as current `sample-scans` `scans/.../report.md` values are omitted from `artifacts`. |
| `counts` | `counts` | Preserve numeric keys used by affected pages. |
| `reason` | `reason` | Preserve, and require for deferred, unavailable, or failed statuses. |

`artifacts` should contain only approved public-safe relative paths and report
families. The fixture should not copy report bodies into site data unless a
future spec explicitly approves a smaller sanitized excerpt format. Per-section
limitations should not be fabricated from `demo-summary.json`; they can be
extracted only from approved public-safe report data with a documented bounded
extractor, or remain page prose validated against status and coverage labels.
If the source summary row contains raw local-only artifact paths, the refresh
may record a generic `localOnlyArtifactFamilies` value such as `scan-reports`
without storing the raw path.

All sections produced by the current `demo-public.sh` summary writer carry
`ruleIds: ["public.demo.summary.v1"]`; the fixture preserves this array
verbatim rather than recalculating per-section rule IDs.

## Refresh Behavior

The refresh command should:

- accept an explicit generated demo output root;
- require `demo-summary.json`;
- parse JSON with structured validation;
- optionally read `demo-summary.md` only for existence, checksum, or reviewed
  public-safe summary metadata;
- optionally read public-safe report JSON files when listed by the summary,
  selected by the public-file allowlist, and accepted by leak rejection checks;
- omit source `artifactPaths` entries that point to local-only raw artifact
  families such as `scans/`, optionally recording only a pathless local-only
  family label;
- validate every `portfolio-manifest.json` `indexPath` value as an approved
  relative path before reading or copying any manifest-derived field;
- derive fixture `id` values from a checked mapping table keyed by known
  `demo-summary.json` `name` values, failing on unknown names instead of
  generating new public identifiers ad hoc;
- keep the known-section mapping table co-located with
  `site/scripts/refresh-demo-summary.mjs`, initialized from the current
  `demo-public.sh` `add_section` names: `toolchains`, `python`, `jvm`, `build`,
  `sample-scans`, `combine-and-dependency-report`, `paths-and-reverse`,
  `portfolio`, `diff`, `impact`, and `release-review`;
- write deterministic JSON with stable key ordering and trailing newline;
- fail closed on unknown sections or unknown artifact families unless the
  implementation updates the schema and tests.

## Validation Behavior

Validation should:

- confirm the committed fixture exists and matches the expected schema;
- reject local absolute paths, path traversal, raw generated artifact families,
  repository remotes, `file://` URLs, secrets, source snippets, SQL text, and
  config values;
- confirm all affected pages reference only facts present in the fixture;
- extract the relevant hard-coded values from HTML source and compare them to
  the fixture, rather than only asserting fixture values directly;
- run as a cheap site validation step without requiring a fresh demo run;
- provide clear error messages that name the offending field and section.

## Affected Pages

The future implementation should validate these pages against the fixture:

- `/demo/result/`
- `/demo/proof-upgrades/`
- `/demo/proof-assets/`
- `/packets/`
- `/manager-packet/`
- `/capabilities/`

The page changes should stay content-bounded. They should not introduce a
client-side data fetch, a backend, or a hidden dependency on a developer's local
demo output directory.

## Page Fact Mapping

The first implementation should validate only facts with a direct source in
`demo-summary.json` unless it also adds a bounded extractor for approved
public-safe reports.

| Page | Section names | Fixture fields and count keys |
| --- | --- | --- |
| `/demo/result/` | `toolchains`, `python`, `jvm`, `build`, `sample-scans`, `combine-and-dependency-report`, `paths-and-reverse`, `portfolio`, `diff`, `impact`, `release-review` | `status`, `classification`, `evidenceTier`, `coverage`, `ruleIds`, `reason`, `artifacts`, and visible `counts` keys. |
| `/demo/proof-upgrades/` | `combine-and-dependency-report`, `paths-and-reverse`, `portfolio`, `diff`, `impact`, `release-review` | `status`, `coverage`, `artifacts`; counts including `sources`, `endpointFindings`, `dependencySurfaces`, `dependencyEdges`, `gaps`, `paths`, `pathGaps`, `reversePaths`, `reverseRoots`, `reverseGaps`, `selectedSurfaces`, `portfolioInputs`, `portfolioSources`, `diffRows`, `surfaceDiffs`, `impactItems`, `surfaceImpacts`, `findings`, `topChangedSurfaces`, `contractFindings`, and `checklistItems` where those facts appear in copy. |
| `/demo/proof-assets/` | `sample-scans`, `combine-and-dependency-report`, `paths-and-reverse`, `portfolio`, `diff`, `impact`, `release-review` | Public-safe summary names, approved report-family paths, `status`, `coverage`, local-only/public-safe boundary references, and hard-coded count keys used by the page. |
| `/packets/` | Any section used as an evidence-packet example | Public-safe summary names, approved report-family paths, `ruleIds`, `evidenceTier`, `coverage`, and local-only/public-safe boundary references. |
| `/manager-packet/` | Any section used in manager-facing count examples | Only count keys present in the fixture, plus proof links and status/coverage labels. |
| `/capabilities/` | Capability rows tied to demo evidence sections | `status`, `coverage`, `artifacts`, `ruleIds`, and proof paths tied to known section IDs. |

## Tradeoffs

Validation-only without a fixture would catch little because every page would
still have manually duplicated counts and paths. A build-time script that reads
local output would reduce duplication, but it risks making normal site builds
depend on private or absent generated artifacts. A committed public-safe fixture
is the middle path: one reviewed data source for public pages, deterministic
static builds, and a clear boundary between demo generation and public
publishing. The first implementation should validate hard-coded static HTML
against the fixture rather than adding a templating layer to the current plain
HTML copier.
