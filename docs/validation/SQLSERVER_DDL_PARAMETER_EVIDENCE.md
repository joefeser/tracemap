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
One leading BOM is excluded only from lexing (it precedes line 1, so spans are
unchanged); a repeated or embedded U+FEFF remains `ddl-malformed-sql`.
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
Only these canonical spellings are admitted (case-insensitive; bracket or double-quote
delimiters as emitted by SSMS scripting are allowed). ANSI synonyms that ScriptDom
folds into the same type (`integer`, `dec`, `char varying`, `character varying`,
`nchar varying`, `national char[acter] varying`) reject the whole input with
`ddl-type-spelling-unsupported` rather than being silently normalized.
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
256 procedures and 128 parameters per procedure. The paren budget is per file, not
per depth, so a procedure with many sized character parameters can exhaust it
before reaching 128 parameters; that input rejects rather than truncating.
ScriptDom still recurses on constructs the budget does not count (prefix unary
and JOIN/APPLY chains), so lexing and parsing run on a dedicated 64 MiB-stack
thread independent of the caller. Of the shapes measured at 1 MiB (prefix `~`,
`-`, `-~`, JOIN/ON, CROSS APPLY, IF chains), the worst needed about 200 bytes of
stack per token, so 100,000 tokens fit with >3x margin. Unmeasured grammar shapes
could cost more; this is a measured margin, not a proof.
Before this, such inputs inside every bound aborted the process (exit 134).
Manifest/export/binding/facts
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

Actual gate results and the validated implementation SHA are recorded below and
in the spec’s implementation-state.md. Fresh pinned C#/VB OSS adapter smokes are explicitly
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

## Current validated head — independent review round, 2026-10-25

Validated implementation head: **`7d20c1ec600916f02dd4f3e65092230bba859ba3`**.
It supersedes the earlier `7a05e13e9` receipt below, which stays as history.
A re-verification of the delivered head against the original brief found three
defects inside the documented bounds, each fixed with a regression test:

- `84a70b0a2`: prefix unary (`-~` x 45,000) and JOIN chains inside the token/file
  bounds crashed CLI and test host with an uncatchable stack overflow (exit 134,
  reproduced through the CLI and by reverting the fix under the new test).
  Parsing now runs on a fixed 64 MiB-stack thread; parser exceptions rethrow.
- `63318851c`: ANSI synonyms (`integer`, `dec`, `char`/`character varying`,
  `nchar varying`, `national char[acter] varying`) were silently admitted as the
  six documented types. Non-canonical spellings now reject; case and delimiters
  (`[nvarchar](20)`) remain admitted.
- `1624cf592`: a leading UTF-8 BOM was documented as admitted but rejected as
  malformed. One leading mark is now excluded from lexing only; digests still
  cover exact bytes; spans are unchanged.
- `7d20c1ec6`: LF pinned for the DDL smoke script and hashed SQL templates,
  matching the JSON-origin smoke policy.

Gates at `7d20c1ec6`, with source, HEAD and shared outputs frozen and no
competing build, smoke or commit during the full suite:

- Focused Debug -warnaserror (DDL, original parameter and DNN tests): **151
  passed, 0 failed, 0 skipped** (135 before this round plus 16 new cases).
  One earlier focused run during development reported 58 passed then "Test Run
  Aborted" without a recorded reason; seven later repeats passed in full. It
  is not counted as a pass and no cause was confirmed.
- Python harness guards: **17 passed**.
- Full Debug solution -warnaserror: **4,733 passed, 0 failed, 3 expected skips**
  (same three skips as below); 33m 13s tests, 1,999 s command wall-clock; zero
  compiler/analyzer warnings or errors.
- Sequential committed-head JSON then DDL native smokes in new directories:
  **52/52 each, 8 / 12 / 32** expected and observed, 25 `binding-missing` unbound
  calls per report, repeat bytes identical. Receipts (outside Git):
  `/tmp/tracemap-ddl-7d20c1e-json/smoke-receipt.json` (bounded input
  `c7d16c7c8adaca57a2096ddc73bd213d371c7beb07a02236c59e7bd7ab540c40`) and
  `/tmp/tracemap-ddl-7d20c1e-ddl/smoke-receipt.json` (bounded input
  `126d7c7c981b1a27e090dd5a7e40c8e52b0f865653b6ba033b60ce23071aad37`).
  Smoke generators are unchanged from the earlier receipt.
- Core generator SHA-256 `a9a368ffd71180138db37809edc01ae4dc55d5b9a6b9ee493d5096a3b9a01098`;
  ScriptDom parser SHA-256 unchanged `2dcf224afac347fa1788eeaa6f8db78a17805b82338590b7e51448ab356a54ce`.
- Private-path guard and `git diff --check` over `560fa2421..HEAD` passed.

Deferrals, limits and future scope below are unchanged. #908 and #870 were read
live and remain open; origin/dev is still `560fa2421`.

## Earlier implementation and delivery receipt — 2026-10-25 (superseded)

Validated implementation head: **`7a05e13e9e9db6c1020288597cb5e890cf2bd2bf`**,
on `codex/sqlserver-ddl-parameter-evidence`. Initial clean branch head was
`70e57e329a9954de15712012d97d3c195d84cf6d`, directly above requested #910 merge
`560fa24216516b17f75882d7d10011361b3f9c11`; fetched dev matched that merge.
This results reconciliation is documentation-only after the implementation head.
The full suite and both native smokes below ran at the implementation SHA, not
at the later documentation reconciliation SHA. No code change follows those gates.

Implemented grammar, identities, six built-in type vocabulary, spans, explicit
unknown facets, origin/version/digest admission and provenance guarantees are
specified above. The authored JSON origin is retained unchanged; DDL is separately
identified as `public-synthetic-tsql-ddl`. Exact Core and parser bytes checked in
committed-head smoke:

- Core generator SHA-256: `3bca2009f8ab0395bba3575111766dc4ee2bc3048572f2b2136d4bc8f9709ceb`.
- ScriptDom parser SHA-256: `2dcf224afac347fa1788eeaa6f8db78a17805b82338590b7e51448ab356a54ce`.

The comparison and producer verify actual expected input/source identity and
regenerated payload at admission. Hashes bind public projected inputs and provide
integrity/reproducibility, not authenticity, installed catalog truth or execution.

| Origin / independently selected cases | Expected match / mismatch / unknown | Observed match / mismatch / unknown |
| --- | --- | --- |
| Original authored JSON, 52 | 8 / 12 / 32 | 8 / 12 / 32 |
| Public synthetic DDL, 52 | 8 / 12 / 32 | 8 / 12 / 32 |

These are two comparisons over the same 26 C# and 26 VB execution sites, not
104 distinct source sites. Each case report also retained 25 other unbound calls
as unknown with `binding-missing`; those rows are excluded from the selected-case
denominators. Case-level independent expectations are listed above and authored
in the tests/smokes before inspecting parser output. Both origins retained partial
coverage and no aggregate clean verdict. Both synthetic projects built successfully;
this is fixture compilation, not application or migration readiness.

Validation results:

- Focused Debug -warnaserror (DDL, original parameter and DNN tests): **133 passed,
  0 failed, 0 skipped**, final repeat 16 seconds. Additional final DDL-only repeat
  after oversize-export regression: **48 passed**, no failures/skips, 18 seconds.
- Existing offline Python harness guards: **17 passed**; no activation/fetch/SQL.
- Full Debug solution -warnaserror at the committed implementation head:
  **4,715 passed, 0 failed, 3 expected skips**; test duration **32m 20s**, command
  wall-clock **32m 30.78s**. No compiler/analyzer warning or error was emitted.
  Skips: opt-in full golden sweep; Python receipt interop environment unset;
  Windows-only deep projectless ASP.NET publish. Source, HEAD and shared outputs
  stayed stable during the gate; no competing smoke, rebuild or commit occurred.
- Sequential committed-head native JSON and DDL CLI smokes: **52/52 each**, exact
  independent 8/12/32 matrices, all five scan artifacts and source revision,
  parser/Core provenance, public input digests and repeated bytes checked.
  JSON repeat bytes and DDL JSON/Markdown repeat bytes were identical.
- Retained #910 initializer/VB With, execution-argument mutation, concurrent
  publication, failed-write cleanup, disk-flush and primary-error regressions
  passed in the focused and full gates. Private-path and whitespace guards passed
  before implementation commit and again before delivery publication.

Development attempts are distinguished in the spec state: an initial nullable
warning and root-ancestor symlink regression were corrected before the committed
implementation. No successful gate is claimed for those failed attempts. Early
smokes used dirty code at the initial revision and are not the acceptance runs.

Fresh pinned OSS C#/VB adapter smokes, non-.NET gates and Windows/application
acceptance remain explicitly deferred. No source-language extractor/version change
was made. The full suite and synthetic C#/VB native route passed; neither supplies
cross-platform, public DNN reader/wrapper/qualifier binding or private acceptance.

There is **no remaining blocker for this bounded offline slice** and no required
work-machine round trip. The supported grammar remains deliberately limited;
default/decimal semantics are unknown, unsupported input rejects, and bodies/
result columns are not interpreted. Result-column/consumer comparison, catalog/
live evidence, private SQL-A, application builds and migration readiness remain
separate future scope. #908/#870 remain open. No PR, review spend, agents, merge,
Docker/image operation, remote host access, SQL execution or deployment occurred.
Git fetch/push and issue reads are the authorized repository delivery operations.

One follow-up prompt: [continue-sqlserver-ddl-parameter-evidence.md](../../prompts/continue-sqlserver-ddl-parameter-evidence.md).

Local public smoke receipts (outside Git): `/tmp/tracemap-ddl-7a05e13-json/smoke-receipt.json`
and `/tmp/tracemap-ddl-7a05e13-ddl/smoke-receipt.json`. Recorded smoke generators:
JSON `5a4f3bd77de59becbb64b757c1455fe86245292bb4d351c463b4171d0bde6ada`; DDL `8b9dd8843f027c60135d5c00e8beb18fa3c9b1928b338f088b76154187d67dff`.
Their bounded-input digests respectively are `d5f38908241dd9f59f9a6ebbd0efd9c734b75cafe0be51fabb3dad90d1a01dda`
and `f119125014c547bd6bd3550e57c5c771e0d4382c6f813bbee042d50e5ff79644`. These receipts bind script bytes and
public input projections; native outputs additionally bind Core/parser bytes.
