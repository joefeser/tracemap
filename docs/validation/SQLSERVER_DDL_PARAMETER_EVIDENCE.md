# Bounded offline SQL Server DDL parameter evidence

This is a separately admitted **public synthetic T-SQL DDL** origin after #910.
It supports structural parameter declarations and the existing native C#/VB
comparison machinery without SQL Server. No procedure, application method,
container, remote host or private schema is executed or accessed. Neither #908
nor #870 is completed. Real DNN selected-reader, wrapper, qualifier and concrete
connection identity gaps remain; synthetic success does not establish DNN binding.

## Parser and rule

Core previously had no T-SQL AST parser. Existing SqlFileExtractor text/shape
facts are not parameter contracts. The new route uses Microsoft’s
[ScriptDom 170.191.0 package](https://www.nuget.org/packages/Microsoft.SqlServer.TransactSql.ScriptDom/170.191.0),
pinned in Core, with TSql160Parser (SQL Server 2022 grammar), quoted identifiers
on. This is a bounded extractor over an AST, not regex SQL grammar or an interpreter.
Official [MIT license](https://github.com/microsoft/SqlScriptDOM/blob/main/LICENSE)
and downloaded package nuspec were inspected. Nuspec has MIT expression, net8.0 /
netstandard2.0 / netstandard2.1 / net472 targets, no dependencies, and upstream
commit `0a5e0d21aa9136c8cbe90e484b76fdd0fcc1eac9`. Restore confirmed net10.0
compatibility and selects net8.0. It adds no connectivity, DacFx or deployment API.
It follows the existing pinned PackageReference policy. This version pin is for
reproducibility, not a claim of newest-version or general SQL support.

Rule `database.sqlserver.ddl-parameters.v1`, extractor `sqlserver-ddl-parameters/1`:
Tier2Structural declarations, Tier4Unknown unsupported facets. Parser package/mode
label and exact parser assembly SHA-256 are recorded independently from the exact
Core generator assembly SHA-256. Source rule/tier/extractor and the existing
`database.sqlserver.parameter-comparison.v1` conclusions remain separate. Parameter
spans inherit the enclosing DDL rule/tier/extractor; each unsupported-facet record
also carries its rule and Tier4 explicitly. No Roslyn tier is assigned to DDL.

## Explicit input, schemas and admission

`sqlserver-ddl-input/1` is an authored manifest, separate from SQL text. All fields
below are required except optional `provenance` (null/absent for authored input):

```json
{
  "version": "sqlserver-ddl-input/1",
  "repo": "synthetic",
  "revision": "1111111111111111111111111111111111111111",
  "server": "SyntheticServer",
  "database": "SyntheticDatabase",
  "publicSynthetic": true,
  "files": [{ "path": "base.sql", "sha256": "<exact-lowercase-SHA-256-of-public-UTF8-file>" }]
}
```

The placeholder hash must be replaced with the actual file hash. Logical identity
is supplied by the author, never inferred from USE, connection strings or deployment.
`publicSynthetic: true` is an input-scope assertion, not sanitization or authentication.
Only independently authored public synthetic files are authorized for this route.
It must not be used to hash private schema text into a shareable artifact.

CLI reads only the manifest’s explicit relative `.sql` list under `--source-root`.
No globbing, recursive discovery, SQLCMD include expansion, network or file writes
occur during parsing. Paths use the existing bounded safe relative vocabulary;
links at or below the explicitly supplied root are refused. System ancestors of
that trusted root may be symlinks (macOS temporary-directory aliases). This is a
local trusted-input tool, not a race-proof hostile filesystem sandbox. UTF-8 is
strict; exact decoded/reencoded bytes are hashed, including an admitted UTF-8 BOM.
No source snippets, default expressions or body text are stored in the export.

`sqlserver-ddl-schema/1` includes the manifest/file hashes, public bounded input
SHA-256 (canonical manifest), generator hash, parser label/hash, DDL rule/tier/
extractor, declaration projection, parameter line spans and unsupported evidence.
Its projection is `sqlserver-ddl-declarations/1` with origin
`public-synthetic-tsql-ddl`, never `independent-synthetic-declarations`.
Optional generated-input provenance has exact producer bytes SHA-256 and public
bounded-input SHA-256, both validated as lowercase digests. This is a recorded
producer claim, not authenticated authorship.

Admission requires the separately supplied expected manifest **and actual files**.
It validates manifest fields, paths, identities, versions, file digests and bounds,
reparses the inputs, and compares the complete regenerated export. Substituted
payloads, versions, origins, extractor/parser/generator bytes, facets, source sets,
revision or digests fail admission. Integrity is not authenticity or installed truth.
The existing JSON `sqlserver-schema produce/compare` route and `/1` schemas remain
unchanged and refuse DDL projections. Library comparison cannot bypass DDL admission.

`sqlserver-ddl-comparison/1` wraps admitted DDL evidence and the existing comparison
row model under `sqlserver-ddl-parameter-comparison/1`. Both report layers bind exact
Core generator bytes and public projected inputs; the inner input hash includes
DDL/parser provenance and the shared projected schema/binding/call-input digest.
The outer input hash binds the admitted DDL and complete inner report. Raw facts,
connection properties, scan clocks and unprojected source properties are excluded.
No new machine-readable output hashes a private artifact. Markdown presents the
same origin, parser/provenance, unsupported evidence, row reasons and limitations.
No closed harness/validation receipt, grouped path or HTML schema is changed.

## Supported grammar, refusal and partial policy

Top-level statements must be CREATE PROCEDURE/PROC, ALTER PROCEDURE/PROC or
CREATE OR ALTER PROCEDURE/PROC. Two-part explicit schema/object identity is required.
Bracketed/double-quoted identifiers, comments, multiline parameters and plain GO
batch separators are supported. After AST unquoting, identifiers must satisfy the
existing ASCII letter/underscore then letter/digit/underscore vocabulary, max 128.
Quoted spaces/punctuation, temp procedures and inferred identifier equivalence are
not supported. Source spelling/case is preserved, never normalized for joins.

Supported built-ins: int, bigint, bit, nvarchar(n/MAX), varchar(n/MAX), decimal.
Length is mandatory for character types; nvarchar 1–4000 UTF-16 code units, varchar
1–8000 bytes, MAX=-1. OUTPUT/OUT becomes InputOutput capability. Names, declared
sizes, type, default presence, decimal precision/scale and parameter/procedure
spans are retained. Bodies are syntax-checked only; they are never traversed for
contracts, result shapes, OUTPUT assignments, RETURN values or execution effects.
Fake declarations inside strings/comments do not create procedures.

| Input | Policy |
| --- | --- |
| Duplicate/conflicting exact or case-only schema/object identities, in one or multiple files | Reject entire input as ambiguous. No last-file-wins or migration ordering. |
| Case/schema/server/database differences at binding | Exact ordinal identity only; unknown when no exact declaration. No short-name fallback. |
| Malformed syntax (including body), unavailable body, implicit schema, numbered/CLR/encrypted procedures, declaration options/replication | Reject entire input; no partial export. |
| Alias, CLR, table-valued/read-only, qualified type references, unsupported built-ins, VARYING/nullability modifiers | Reject entire input; no guessed builtin mapping. Numeric synonym and other type extensions are outside the explicit vocabulary. |
| SQLCMD variables/includes/directives | Reject entire input, conservatively even inside strings/comments. No substitution. |
| Defaults | Retain presence and unsupported-facet span, omit expression text. Any bound row for that procedure stays unknown, including supplied arguments; no default evaluation. |
| Decimal precision/scale, including implicit facets | Retain declared numbers and explicit unsupported-facet evidence; bound rows unknown. Proven type/direction differences remain reasons. |
| Parameter COLLATE syntax | Rejected by the selected grammar; never infer a collation. Body collation is not interpreted. |
| Dynamic SQL, result sets, RETURN and OUTPUT assignment behavior in bodies | Syntax only, no contract interpretation. RETURN call parameters remain separate and excluded from parameter matching. |
| Bounds exhausted | Reject entire input; no truncation masquerading as successful admission. |

Bounds: 32 files, 256 KiB per file, 1 MiB total UTF-8; 100,000 lexical tokens per
file before AST parsing; conservative maximum 128 combined opening parentheses/
BEGIN/CASE/IF/WHILE tokens per file (a complexity budget, not flow analysis);
256 procedures and 128 parameters per procedure. Manifest/export/binding/facts
CLI inputs are each limited to 4 MiB, strict JSON depth 32; shared comparison
limits remain one million facts / 4,096 bindings. CLI bounded reads also refuse
files that grow beyond the byte ceiling during reading. Exports larger than the
4 MiB admission ceiling reject before publication so every successful export can
be read by the native admission route.

Exact source fact ID, public source-evidence digest, repo/revision, command spelling
and all four logical server/database/schema/procedure components must be bound
through existing `sqlserver-bindings/1`. Connection strings, DNN owner/qualifiers,
short names and inferred deployment order never establish a join. Every report
remains `partial` / `no-aggregate-clean-verdict`; unbound calls remain unknown.
Source build/coverage, reachability gaps and truncation stay separately owned and
are not cleared by a parameter match. No new scanner extraction or language
adapter behavior/version is introduced. Existing `/1` ANSI, length, direction and
ReturnValue policies are retained, with stricter explicit DDL-default unknowns.

## Independent denominator and regressions

DDL and JSON matrices independently specify the same 52 selected source cases:
26 sites in 25 methods per C#/VB language. The TwoCommands order is independently
specified (size-40 second command executes first, size-20 original executes second),
never derived from extractor values. Each case binds exactly one call and retains
25 other calls as unknown with `binding-missing`.

| Selected cases, each C# and VB | Independent expected outcome |
| --- | --- |
| Good, Max, InputOutput, TwoCommands size 20 | 8 bounded matches total |
| SizeMismatch, TypeMismatch, DirectionMismatch, Extra, Missing, TwoCommands size 40 | 12 mismatches total |
| MissingSize, Ansi, Decimal, CaseName, Channels, DefaultOmitted; DynamicName, Conditional, Alias, ParameterAlias, Mutation, ParameterReassignment, HelperBoundary, Text, DynamicCommand, Duplicate | 32 unknowns total |

The original JSON matrix and its per-report unbound denominator are retained.
New tests cover all three grammar forms/PROC, quotes/comments/multiline/MAX/OUTPUT,
fake declarations, batch separators, independent spans, no migration ordering,
wrong logical identity/case, stale digests/revisions, substitution of every envelope
component, default/facet/mismatch coexistence, RETURN/OUTPUT/result controls and
bounds. Native C#/VB CLI tests pin complete reports, byte-identical JSON/Markdown,
no-overwrite and malformed/link rejection. Existing #910 publication tests retain
concurrent winners, failed-write cleanup/retry, durable byte flush and preservation
of the primary exception; the new CLI directly reuses those unchanged helpers.
Original initializer/VB With and execution-argument mutation controls still run.

## Reproduction and validation

All destinations must be new. Authored SQL templates are under
`samples/sqlserver-ddl-parameter-evidence`; they are scan/parse-only.

```sh
dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
# Supply the authored manifest with exact public file hashes and repo/revision.
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll sqlserver-ddl produce \
  --input /tmp/public-ddl-input.json --source-root samples/sqlserver-ddl-parameter-evidence \
  --out /tmp/public-ddl-export-new.json
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll sqlserver-ddl compare \
  --input /tmp/public-ddl-input.json --source-root samples/sqlserver-ddl-parameter-evidence \
  --schema /tmp/public-ddl-export-new.json --bindings /tmp/public-bindings.json \
  --facts /tmp/public-scan/facts.ndjson --repo synthetic \
  --revision 1111111111111111111111111111111111111111 --out /tmp/public-ddl-report-new

dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror \
  --filter 'FullyQualifiedName~SqlServerDdlEvidenceTests|FullyQualifiedName~SqlServerParameterEvidenceTests|FullyQualifiedName~DnnSqlValidationTests'
python3 -m unittest discover -s scripts/validation -p test_dnn_sql_harness.py -v
# Commit implementation before this full gate; freeze revision/source/build outputs.
dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror
# Run sequentially only after the full suite completes; no competing rebuild/smoke.
python3 scripts/sqlserver-parameter-evidence/smoke.py --output /tmp/json-origin-smoke-new
python3 scripts/sqlserver-ddl-parameter-evidence/smoke.py \
  --json-smoke /tmp/json-origin-smoke-new --output /tmp/ddl-origin-smoke-new
./scripts/check-private-paths.sh
git diff --check
```

The DDL smoke reuses only same-head public native scans from the original smoke;
it independently specifies DDL expectations and binds exact smoke-script bytes,
public template hashes and projected call evidence. Each generated manifest and
binding carries producer provenance. Exports/report parser/Core bytes, source
revision, partial status, unbound reasons and both repeated report formats are
checked. The receipt has an exact generator and public bounded-input digest.
The reused JSON smoke helper source is independently hashed by its original receipt;
core output and parser binary digests are additionally verified.

Actual gate results and validated implementation SHA belong in the spec’s
implementation-state.md. Fresh pinned C#/VB OSS adapter smokes are explicitly
deferred: this adds only opt-in SQL parsing/admission/CLI and changes no C#/VB
extractor. The native synthetic C#/VB scans and retained semantic regressions
exercise the unchanged source route. Non-.NET and Windows private/application
checks are also deferred. Full shared-core Debug -warnaserror remains required.

No further work-machine round trip is a blocker. Result-column/consumer comparison,
live/catalog evidence, private SQL-A, application builds and migration readiness
are distinct future work. Publication has #910’s exclusive new-file / staged
new-directory, disk-flush and primary-error-preservation contracts; it does not
promise atomic file visibility during writing, directory fsync, abrupt-termination
recovery or hostile concurrent filesystem mutation isolation.
