# Local-only SQL literal recovery

Optional `--contract-catalog <private-local.json>` enriches exact recovered
StoredProcedure literals with operator-supplied procedure/parameter metadata.
See `samples/sql-database-matrix/public-contract-catalog.json` for the synthetic
input format (not a real deployed procedure contract). Catalog metadata is not
independently verified. Matching is ordinal and case-sensitive; no schema-prefix,
case or database guessing occurs. Multiple exact entries remain ambiguous even
if identical. Text/dynamic commands cannot acquire a procedure contract this way.
Empty parameter lists mean none supplied, not proof of a zero-parameter procedure.
Directions: Input, Output, InputOutput, ReturnValue, Unknown. The local output
records the catalog SHA-256 and keeps migration decisions undecided. Limits:
4 MiB catalog, 1024 entries, 256 parameters per entry, 4096 characters per field.
Use existing authorized local schema documentation or a human-maintained catalog;
this tool does not query a database or generate an authoritative schema export.

Explicit opt-in diagnostic, not a scanner default. This reads a retained native
`paths-report.json` and one explicitly admitted binary without loading it into
the runtime, resolving dependencies or executing code. It matches only constant
SQL command-origin hash/UTF-16 length identities to `ldstr` operands. It does not
evaluate symbolic expressions, extract procedure bodies or connect to a database.

```text
dotnet build scripts/diagnostics/RecoverSqlCommandText/RecoverSqlCommandText.csproj -warnaserror
dotnet scripts/diagnostics/RecoverSqlCommandText/bin/Debug/net10.0/RecoverSqlCommandText.dll --paths <native-path-report.json> --assembly <explicit-admitted-binary.dll> --expected-sha256 <recorded-admitted-binary-sha256> --out <fresh-private-local.md> --allow-local-command-text
```

Use a recorded admitted binary digest from retained evidence, not merely a fresh
hash of whichever binary happens to be available. Stop if provenance cannot be
reconciled. The tool checks the supplied digest but does not independently bind
it to the retained report. Output always states this limitation. A content match
does not upgrade route tier, binary/source provenance, runtime use or current-build
identity. Caller literals may live in the application binary rather than the
wrapper: use the origin-body evidence to select the admitted input, not directory
search or name guessing. Separate binaries require separate explicit invocations.

The output is **private and local-only** and may contain sensitive command text.
It must not be uploaded, committed or placed in a shared/public report. stdout and
stderr never contain recovered literals. Input identities and generator/component
hashes are recorded in the local Markdown. No machine-readable schema is added.
Markdown links/images are rendered inert; surrogate UTF-16 units and NUL use
visible Unicode escapes. This is a review representation, not executable SQL.
Existing output is refused. No consent or incorrect binary hash produces no output.

Original native query truncation flags and reason categories are preserved in
the output. Missing flags/reasons stay unknown; successful recovery never clears
truncation. Conflicting flags and reasons are retained rather than normalized away.

Repeat `--paths` to consolidate up to 32 native reports against the same explicitly
admitted binary. Input order is retained. Each input keeps its own hash, native
source provenance, query, coverage, limitations and truncation diagnostics. Rows
are keyed by input ordinal plus path ID, not deduplicated across roots or spliced
into new routes. Empty roots remain visible. Duplicate files/content are refused.
Repeated literals are not unique procedure counts. Parameters, connection and
transaction evidence must be inspected in the native evidence; this projection
does not recover them. Schema contracts and migration decisions remain undecided.

Bounds: 64 MiB aggregate report bytes, 256 paths per report, 1024 total rows,
256 MiB binary, two million methods/instructions,
32768 UTF-16 units per literal. Overlength, symbolic, missing or nonunique matches
remain unavailable. Hash algorithm mirrors the scanner's exact UTF-16 code-unit
identity (including unpaired surrogates), not UTF-8 or replacement normalization.
The input report must be a supported native path report, not a grouped handoff.

Validation uses public SyntheticWebsite fixtures and the native retained Save
route via `scripts/validation/test_sql_command_text.py`. It tests disclosure
consent, hash mismatch, unmatched/dynamic values, escaping and overwrite refusal.
