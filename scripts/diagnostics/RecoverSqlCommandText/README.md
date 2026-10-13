# Local-only SQL literal recovery

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

Bounds: 64 MiB report, 256 paths, 256 MiB binary, two million methods/instructions,
32768 UTF-16 units per literal. Overlength, symbolic, missing or nonunique matches
remain unavailable. Hash algorithm mirrors the scanner's exact UTF-16 code-unit
identity (including unpaired surrogates), not UTF-8 or replacement normalization.
The input report must be a supported native path report, not a grouped handoff.

Validation uses public SyntheticWebsite fixtures and the native retained Save
route via `scripts/validation/test_sql_command_text.py`. It tests disclosure
consent, hash mismatch, unmatched/dynamic values, escaping and overwrite refusal.
