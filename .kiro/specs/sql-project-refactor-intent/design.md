# SQL Project Refactor Intent Design

## Boundary

This slice observes declared refactor intent in repository XML. It does not
evaluate MSBuild properties, build a SQL project, open a `.dacpac`, generate a
deployment plan, connect to SQL Server, or inspect deployment history.

## Inputs

`FileInventory` admits `.sqlproj` and `.refactorlog`. The extractor receives the
scan root, manifest, and inventory. A `.sqlproj` is the project provenance anchor.
Only literal `RefactorLog Include` values are eligible for resolution.

XML is read through `XmlReader` with:

- `DtdProcessing.Prohibit`;
- `XmlResolver = null`;
- bounded file size;
- bounded operation count.

References containing MSBuild expressions, wildcards, rooted paths, or paths
escaping the scan root are rejected with a gap. Inventory matching is
case-insensitive for Windows project compatibility, but multiple matches are an
ambiguity gap.

## Facts and rules

Rules:

- `database.sql-project.refactor-intent.v1`
- `database.sql-project.refactor-intent.gap.v1`

Fact types:

- `SqlProjectRefactorLogDeclared`
- `SqlProjectRefactorOperation`
- `AnalysisGap`

Supported v0 operations:

- Rename Refactor for bounded table and column identities.
- Move Schema for bounded table identities.

Names are accepted only as bracketed or bare SQL identifiers composed of letters,
digits, `_`, `$`, `#`, and `@`, with a valid non-digit first character. The
extractor stores decomposed source/target names and never stores the raw XML
operation. Unsupported operation or element kinds produce gaps.

The operation `Key`, when present, is represented only as a stable SHA-256 prefix.
File line spans are calculated from XML line information. All facts carry
`sql-project-refactor/0.1.0`.

## Composition

The existing SQL evidence reader admits the new rule IDs.

Database design review:

- project/log declarations and supported operations are global evidence;
- supported operations are classified `ReviewRecommended`;
- gaps retain upstream provenance and become `DatabaseDesignGap`;
- no SQL Server intent is inserted into PostgreSQL table groups.

Release review:

- supported operations become `sql-project-refactor` findings with
  `ReviewRecommended`;
- declarations may provide contextual no-actionable-evidence;
- extractor gaps become `ReleaseReviewGap`;
- safe metadata is allow-listed.

## Limits

- maximum input size: 1 MiB per project/refactor log;
- maximum operations: 1,000 per refactor log;
- standalone unreferenced refactor logs produce a reduced-coverage gap;
- duplicate project references are deduplicated deterministically.

## Security posture

No raw XML, SQL, connection strings, local absolute paths, private infrastructure
identities, or arbitrary element values are rendered. Output includes only bounded
identifiers, safe classifications/counts, hashes, repository-relative paths, and
documented limitations.
