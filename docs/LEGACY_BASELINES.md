# Legacy Baseline Artifacts

Legacy baseline artifacts preserve deterministic aggregate scanner behavior for
old or large samples without committing raw scan outputs. They are measurement
artifacts only: comparisons report static evidence count movement and coverage
label changes, not reducer conclusions.

## Create A Local Candidate

Use ignored `.tmp/legacy-baselines/` storage for local candidates:

```sh
tracemap baseline create \
  --scan-output samples/synthetic-legacy-scan \
  --label synthetic-alpha \
  --purpose original-parser-snapshot \
  --out .tmp/legacy-baselines/synthetic-alpha__original-parser-snapshot__2026-06 \
  --created-at 2026-06
```

Use `--dry-run` to report the safety classification without writing outputs.
Use `--local-only` when a baseline carries local comparison metadata that must
remain ignored.

## Validate Before Promotion

Public redacted baselines live under `.kiro/baselines/legacy/<baseline-id>/`.
Before promoting a local candidate, run:

```sh
tracemap baseline validate --manifest .tmp/legacy-baselines/<baseline-id>/baseline-manifest.json
./scripts/check-private-paths.sh
git diff --check
```

Committed baseline manifests must contain only neutral labels, aggregate counts,
rule IDs, evidence tiers, extractor versions, coverage labels, known gap
categories, limitations, and validator-backed classification fields.

## Compare Baselines

Comparison output remains local-only until separately validated:

```sh
tracemap baseline compare \
  --baseline .kiro/baselines/legacy/synthetic-alpha__original-parser-snapshot__2026-06/baseline-manifest.json \
  --candidate .tmp/legacy-baselines/synthetic-alpha__candidate__2026-07/baseline-manifest.json \
  --out .tmp/legacy-baselines/comparisons/synthetic-alpha \
  --generated-at 2026-07
```

Use a `legacy-baseline-migration-map.v1` file only when rule IDs or fact types
were renamed without changing the meaning of the aggregate measurement.
