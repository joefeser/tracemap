# Guided Local Review Workflow

Status: implementation runway for issue #666. Distribution publication and
cross-platform installation claims remain gated by `docs/LOCAL_DISTRIBUTION.md`.

The guided workflow composes existing TraceMap producers. It does not add
extraction rules or reinterpret scanner evidence.

## Command

```text
tracemap local-review run \
  --repo <repository> \
  --out <new-or-empty-output-directory> \
  [--solution <path>] \
  [--project <path>] \
  [--include <glob>] \
  [--exclude <glob>] \
  [--target-framework <tfm>] \
  [--webforms-modernization] \
  [--explorer]
```

The v1 safe path intentionally does not accept `--restore`. It does not upload
artifacts, call a hosted service, enable telemetry, or overwrite a nonempty
output directory.

## Output

```text
review-output/
  local-review-result.json
  README.md
  scan/
    scan-manifest.json
    scan-receipt.json
    facts.ndjson
    index.sqlite
    report.md
    logs/analyzer.log
  webforms/                 # when requested and compatible
  explorer/                 # when requested and compatible
```

`local-review-result.json` follows
`docs/contracts/local-review-result.v1.schema.json`. It binds the workflow to
the repository identity hash, commit SHA, scan ID, and source-snapshot digest;
records relative artifact paths and exact SHA-256 hashes; and reports typed
stage outcomes, gaps, the last proven safe state, cleanup, retryability, and a
bounded next action.

Absolute output paths are printed only to the interactive terminal. Portable
JSON and Markdown omit them.

## Safety Behavior

- Output inside the scanned repository, filesystem roots, `.git`, files,
  nonempty directories, and an existing output symlink/reparse point is
  rejected. Existing parent links are resolved to their final canonical target
  before the target is authorized; this supports normal host aliases such as
  macOS `/tmp` without placing evidence at an unverified location.
- Work is generated in a sibling staging directory and published by directory
  rename.
- Each downstream stage hashes the complete scan directory before it runs and
  verifies those bytes afterward. Mutation stops later work and records
  `LOCAL_REVIEW_INPUT_MUTATED`.
- A failed scan publishes a categorical failure result and any bounded
  producer receipt that exists. Raw exception text is not copied into the
  portable result.
- A downstream failure preserves the already verified scan and records the
  failed stage instead of presenting the workflow as successful.
- The Web Forms packet and ordinary explorer stages call the same producers as
  their standalone commands. #667 remains responsible for rendering the Web
  Forms packet itself in the explorer.

## Interpretation

The workflow provides deterministic local static evidence. It does not prove
runtime execution, application correctness, complete coverage, migration
safety, release approval, publisher identity, or production state. Reduced
scanner coverage remains reduced in the workflow result.
