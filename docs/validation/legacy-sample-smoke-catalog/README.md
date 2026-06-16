# Legacy Sample Smoke Catalog

This directory contains reviewed validation metadata for neutral legacy sample
smoke families. `catalog.json` is the source of truth. `catalog.md` is generated
from that JSON and starts with a deterministic hash sentinel.

The catalog is not raw scan output, not an evidence pack, not a baseline, not a
site page, and not an impact-analysis result. It records sample labels, source
classification, pinned fixture or reviewed public commit identity, expected
static evidence families, validation command templates, relationship references,
claim levels, and limitations.

Operator-only paths, private source notes, candidate manifests, and local
validation outputs belong under `.tmp/legacy-sample-smoke-catalog/` only. Before
placing local input there, prove the root is ignored:

```bash
git check-ignore .tmp/legacy-sample-smoke-catalog/example
```

## Workflow

Validate the tracked catalog:

```bash
node scripts/legacy-sample-smoke-catalog.mjs validate --catalog docs/validation/legacy-sample-smoke-catalog/catalog.json
```

Render a reviewed candidate with an explicit year-month:

```bash
node scripts/legacy-sample-smoke-catalog.mjs render --catalog docs/validation/legacy-sample-smoke-catalog/catalog.json --out docs/validation/legacy-sample-smoke-catalog --date <YYYY-MM> --force
```

Render only entries at or above a claim level:

```bash
node scripts/legacy-sample-smoke-catalog.mjs render --catalog <catalog-json> --out docs/validation/legacy-sample-smoke-catalog --date <YYYY-MM> --minimum-entry-claim-level <claim-level> --force
```

Promote an already-rendered, already-validated candidate:

```bash
node scripts/legacy-sample-smoke-catalog.mjs promote --catalog <catalog-json> --markdown <catalog-md> --out docs/validation/legacy-sample-smoke-catalog --force
```

## Safe Vocabulary

Safe labels use lowercase kebab-case neutral names. Source classifications are
`synthetic-fixture`, `public-repo`, `public-archive`, `public-doc-sample`,
`private-local`, `operator-local`, or `unknown`. Tracked commit identity kinds
are exactly `public-sha`, `fixture-version`, and `category-only`.

Claim levels are `hidden`, `demo-safe`, and `public-safe`. The catalog
classification is the least-safe included entry. Hidden entries are not allowed
in demo-safe or public-safe tracked output unless a render filter omitted them
from the candidate output.

Command templates must use placeholders such as `<sample-root>`,
`<scan-output>`, `<redacted-summary>`, `<pack-output>`, `<catalog-json>`, and
`<YYYY-MM>`. Literal option values are allowed only for booleans and documented
closed vocabularies such as `--input-kind legacy-validation-summary`.

## Relationships

The catalog may reference `legacy-codebase-validation`,
`legacy-baseline-regression-artifacts`, and `legacy-sample-evidence-pack` by
safe schema name, neutral artifact ID, claim level, and validation status only.
Raw scan manifests, facts, SQLite indexes, reports, analyzer logs, raw baseline
manifests, raw evidence packs, SQL/config values, remotes, local paths, snippets,
credentials, and private names must stay out of tracked files.

Public-facing claims should be based on promoted public-safe proof such as
evidence packs or docs generated from them. This catalog alone is a maintainer
validation inventory.
