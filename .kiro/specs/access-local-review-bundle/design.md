# Access Local Review Bundle Design

## Decision

Add a read-side `access-review create` command to the existing cross-platform
TraceMap CLI. It composes the shipped Access scan artifacts through
`ReleaseReviewReporter` and `StaticHtmlEvidenceExplorer`; it does not reference
the Access COM reader or worker.

## Command

```text
tracemap access-review create
  --scan-output <access-scan-directory>
  --out <new-bundle-directory>
  [--force]
```

The command validates the five standard scan artifacts and requires compatible
Access facts in `index.sqlite`. It then builds in a sibling staging directory:

```text
README.md
access-review-manifest.json
release-review/release-review.md
release-review/release-review.json
explorer/index.html
explorer/assets/explorer.css
explorer/assets/explorer.js
explorer/data/explorer-manifest.json
explorer/data/explorer-data.json
explorer/README.md
```

## Composition

- `ReleaseReviewReporter` receives the scan `index.sqlite` as both before and
  after, scope `access-evidence`, and no path, reverse, priority, contract,
  SQL, package, or identity-mismatch options.
- `StaticHtmlEvidenceExplorer` receives the scan directory with safety profile
  `hidden-local`.
- The bundle manifest projects only safe report status/counts and hashes the
  generated files after both components finish.
- The README links to the local HTML and Markdown artifacts and repeats the
  non-claims.

The repeated before/after index is an explicit after-snapshot design view. It
does not claim that a change occurred or did not occur.

## Input gate

Input validation requires regular readable standard artifacts, a readable
SQLite index, and at least one compatible Access fact. Before composition, the
command proves that the file manifest, every NDJSON fact, the SQLite manifest,
and every indexed fact share one scan/repository/commit identity and that the
NDJSON and index contain the same fact-ID inventory. The implementation uses
the existing Access composition read hook rather than adding another Access
fact projector. If artifacts disagree or Access evidence is absent, the command
fails with a categorical message before publishing output.

## Publication

All files are written under a randomly named sibling staging directory. A new
destination is created by directory move. `--force` may replace only a
directory whose `access-review-manifest.json` proves the expected schema and
`tracemapGenerated: true`; unrecognized destinations fail closed.
Existing Windows path segments are rejected when any segment is a reparse
point, preventing a junctioned parent from bypassing the overlap and ownership
guards.

If forced publication fails after moving the prior bundle aside, the prior
bundle is restored when the destination remains free. If another process
recreates the destination, the intact backup is retained rather than deleted.

The manifest does not hash itself. Relative paths use `/` separators and ordinal
ordering. JSON and Markdown use normalized deterministic formatting.

## Safety

The manifest and README never accept display values from Access facts. Their
data comes from closed statuses, counts, standard coverage labels, commit SHA,
fixed text, and content hashes. Existing release-review and explorer safety
paths remain authoritative for evidence rendering, including the required
repository-relative database evidence span and rejection of machine-local
absolute paths.

Unexpected I/O and parser failures are converted to categorical diagnostics;
framework exception text is not exposed by the command. Rooted machine-local
path patterns are denied without treating ordinary repository-relative
segments such as `databases/private/` or `src/home/` as absolute paths.

The implementation adds no Windows-only dependency and changes no Access
adapter, COM, fixture-generation, worker, or projector code.

## Validation

- command parsing, help, missing/overlapping/foreign inputs, and guarded force;
- Access single-index bundle generation;
- deterministic repeated output;
- standard files and relative-link integrity;
- manifest hash verification;
- explicit count-only gaps and provenance preservation;
- protected-value and absolute-path denial across the entire bundle;
- focused Access/release-review/explorer tests;
- full solution build/test, private-path guard, and diff check;
- unchanged-boundary synthetic Parallels smoke when available.
