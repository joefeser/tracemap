# Bounded offline SQL Server result-column / reader evidence

This slice follows #911 (`730610582660fe32354479b35cf80eb2aa954a62`). It answers one
question inside an explicit static subset: **does the selected reader's column-name
or ordinal access agree with one admitted first-result-set projection?** Missing or
renamed columns and out-of-range ordinals are visible mismatches; uncertain
associations stay unknown. Every report is `partial` / `no-aggregate-clean-verdict`.
Nothing is executed: no SQL, procedure, application method, container or remote host.
#908 and #870 stay open; synthetic results are not DNN or private SQL-A acceptance.

## Contracts and routes

| Contract | Meaning |
| --- | --- |
| `sqlserver-reader/1` (`sqlServerReaderState` fact property) | Source consumer evidence on every ExecuteReader-family DatabaseOperationCandidate. Separate from `sqlServerParameterState`. C# `csharp-semantic/0.22.10`, VB `vb-semantic/0.10.13`. |
| `sqlserver-result-declarations/1` → `sqlserver-result-schema/1` | Independently authored public JSON (origin `independent-synthetic-result-declarations`), extractor `sqlserver-synthetic-result-declarations/1`, rule `database.sqlserver.synthetic-result-shape.v1`, Tier2Structural. |
| `sqlserver-ddl-result-input/1` → `sqlserver-ddl-result-schema/1` | Opt-in DDL projection (origin `public-synthetic-tsql-ddl-result`), extractor `sqlserver-ddl-result-shapes/1`, rule `database.sqlserver.ddl-result-shape.v1`; Tier2 complete shapes, Tier4 unsupported records. |
| `sqlserver-result-bindings/1` | Exact `factId`, two-part `commandText`, four-part `identity` and `readerEvidenceSha256`. |
| `sqlserver-result-comparison/1`, `sqlserver-ddl-result-comparison/1` | Rows under `database.sqlserver.result-column-comparison.v1` (Tier2Structural bounded match/mismatch, Tier4Unknown otherwise). The DDL wrapper's inner report is `sqlserver-ddl-result-column-comparison/1`. |

CLI spelling mirrors the existing parameter routes:

```sh
tracemap sqlserver-result-schema produce --declarations <public.json> --out <new.json>
tracemap sqlserver-result-schema compare --declarations <public.json> --schema <export.json> \
  --bindings <result-bindings.json> --facts <facts.ndjson> --repo <repo> --revision <sha> --out <new-dir>
tracemap sqlserver-ddl-result produce --input <manifest.json> --source-root <public-root> --out <new.json>
tracemap sqlserver-ddl-result compare --input <manifest.json> --source-root <public-root> --schema <export.json> \
  --bindings <result-bindings.json> --facts <facts.ndjson> --repo <repo> --revision <sha> --out <new-dir>
```

Outputs are `result-column-comparison.json` and `report.md`, published with #910's
exclusive new-file / staged new-directory helpers (now parameterized with the
required file names). All destinations must be new.

Backward compatibility: `sqlserver-schema/1`, `sqlserver-ddl-schema/1`,
`sqlserver-bindings/1`, `sqlserver-call/1`, their CLI routes and the retained
52-case receipts are unchanged. The new routes refuse parameter declarations,
parameter exports, DDL parameter manifests/exports and parameter bindings (strict
unknown-field/version/origin checks); the old routes refuse the new artifacts. An
older receipt is never reinterpreted as result-shape evidence. The only change to
existing facts is the additive `sqlServerReaderState` property on reader executions
and the corresponding extractor version bumps; `sqlServerParameterState` and its
`CallDigest` projection are unchanged in shape.

## Source subset (C# and VB, one shared IOperation walker)

Supported, all inside one method body:

- receiver is a local declared from `new SqlCommand(...)` (signed System.Data.SqlClient
  or Microsoft.Data.SqlClient; no object initializer; inert constructor arguments);
  constant CommandText (constructor or assignment) and constant CommandType;
  `Connection` assignment from a local/parameter and `Parameters.Add*` statements
  with inert arguments are tolerated; mutations after the execution are ignored;
- `ExecuteReader()` with no arguments, directly the initializer of one `SqlDataReader`
  local (plain, `using (...)`, `using var`, VB `Dim`/`Using`);
- accesses on that exact reader symbol: indexer with constant string or int
  (`reader["Name"]`, VB `reader("Name")`/`reader!Name`), one-int-argument
  `Get*`/`IsDBNull` getters with a constant ordinal, `GetOrdinal(constant)` used
  directly as a getter argument or through a fresh ordinal local declared from the
  same reader's `GetOrdinal` and never written again;
- one `Read()` that is the condition of an `if` or top-tested `while`/`Do While`;
  `Close()`/`Dispose()` statements.

Typed unknown (`coverage: unknown`, no accesses retained): async/APM or
CommandBehavior executions, non-SqlCommand receivers, non-local or helper-produced
commands, interface/converted/inline readers, prior execution on the same command,
command aliases/escapes/reassignment/other members, reader aliases, reassignment,
field/helper escapes, `NextResult`, `RecordsAffected`/`HasRows`/`FieldCount` and
other members, more than one `Read()` or a `Read()` outside a guard, and any other
method-wide control flow (other `if`/loops, do-while/Until, ternary, `?.`, `??`,
short-circuit operators, try, lambdas/local functions, VB `With`/implicit
receivers, dynamic, invalid code). Bounds: 20,000 operations and 256 accesses.
Access-level unknowns keep the reader bounded: `dynamic-column-name`,
`dynamic-ordinal`, `ordinal-local-reassigned`, `ordinal-from-other-reader`,
`column-name-outside-identifier-vocabulary` (only ASCII identifiers are retained).
Accesses are bound by symbol identity to the reader declared from that exact
invocation, so two commands/readers in one method keep independent states.
ExecuteScalar/ExecuteNonQuery facts carry no reader state and yield
`reader-state-unavailable`.

## Admission and comparison policy

JSON declarations: strict reader (unknown fields, duplicate properties, depth 32,
4 MiB), exact repo (single path component) and 40-hex revision, 1–256 shapes with
unique exact four-part identities, `resultSet: first`, `complete` (≥1 column, no
reason) or `unknown` (slug reason; listed columns are never used), `ordinal` equal
to position, ASCII identifier names, column lines inside the shape span and an
optional visible `declaredType`. Duplicate names are legal SQL and are admitted.
Admission regenerates the export from the expected declaration input with the
current Core bytes and compares the complete canonical object; generator, input
hash, version, rule, tier, payload, repo or revision substitution fails closed.

DDL: the result route runs the complete #911 DDL admission core (strict UTF-8,
exact file digests, raw-text SQLCMD guard, 64 MiB parser thread, token/complexity/
size bounds, identity ambiguity, the parameter vocabulary) with its own manifest
version, then inspects each admitted procedure body:

| Body | Shape |
| --- | --- |
| Exactly one `SELECT` (optionally the only statement of one `BEGIN/END`) whose elements are all `literal AS alias` / `alias = literal` / `CAST(literal AS builtin[(n[,m])]) AS alias`, unique (case-insensitive) identifier aliases, no FROM/WHERE/GROUP/HAVING/ORDER/TOP/OFFSET/DISTINCT/FOR/INTO/CTE/hints | `complete`, ordered aliases, element lines, CAST spelling as uncompared `declaredType`, plus a Tier4 `column-type-facets-not-compared` record |
| IF, WHILE, TRY/CATCH, EXEC / sp_executesql, RETURN, nested or extra blocks, several statements (including `SET NOCOUNT ON`), SELECT *, FROM/joins/views, UNION, SELECT assignment, missing or string aliases, non-identifier aliases, non-constant expressions (unary minus, variables, subqueries, CONVERT, COLLATE, alias types), duplicate/case-only aliases, more than 1,024 columns | `unknown` with a specific reason, **no columns**, and a Tier4 unsupported record at the offending span |

A supported-looking SELECT nested in an unsupported body never establishes a
shape. Parameter admission of the same procedures is unchanged (the parameter
route's ProduceCore runs with no visitor). No body text or literal value is stored.

Comparison, per bound access (one call-level unknown row otherwise):

| Situation | Status / reason |
| --- | --- |
| Exact-case unique declared name | bounded-match |
| Name absent from a complete shape (including renamed) | mismatch `missing-column:<name>` |
| Name differs only by case, or a case-only sibling exists | unknown `column-case-policy-unknown:<name>` |
| Name declared more than once | unknown `duplicate-declared-column:<name>` |
| Constant ordinal `0 ≤ n < count` | bounded-match (also with duplicate names) |
| Constant ordinal `n ≥ count` or `n < 0` | mismatch `ordinal-out-of-range:<n>` |
| GetOrdinal-derived ordinal | compared by its constant name |
| Unknown/incomplete shape | unknown `result-shape-unknown:<reason>` |
| Dynamic or unsupported access | unknown with the access gap |
| Missing binding, absent exact identity, command text conflict, unsupported extractor/tier, unknown or missing reader state, Text command, no accesses, invalid state | one call-level unknown row with the reason |

Typed getters (`getter-type-not-compared:GetInt32`), `IsDBNull`
(`nullability-not-compared:IsDBNull`) and declared types
(`declared-type-not-compared:int`) are listed in `uncomparedFacets`; they never
change the status. Rows carry the source span, source rule/tier/extractor, access
span, shape identity/file/span/completeness and the matched column line. The
report's bounded input hash covers only the admitted export, bindings and the
reader projections (fact identity, location, extractor and parsed reader state);
raw facts, other properties and scan clocks are excluded. The DDL wrapper binds
admitted DDL evidence and the complete inner report, as in #911.

## Public synthetic corpus and independent matrix

`samples/sqlserver-result-column-evidence` has 33 methods / **34 execution sites per
language** (TwoReaders has two), the authored `result-declarations-base.json`
(`dbo.ReadItems` = Id, Name, Total; `dbo.ReadOther` = Title) and 15 DDL templates.
Expectations below were written before extractor output was inspected and are
duplicated independently in `SqlServerResultColumnEvidenceTests` and the smoke.

| Case (each C# and VB) | Rows (source order) | Case | Rows |
| --- | --- | --- | --- |
| NameMatch | match | MissingName | mismatch |
| Renamed (DisplayName variant) | mismatch | OrdinalValid `GetInt32(0)` | match |
| OrdinalOutOfRange `GetString(3)` | mismatch | Reordered (Total, Name, Id) | match, match |
| GetOrdinalDirect | match, match | GetOrdinalLocal | match, match |
| GetOrdinalMissing (`Price`) | mismatch, mismatch | CaseOnly `name` | unknown |
| DuplicateDeclared | unknown | DynamicName / DynamicOrdinal | unknown / unknown |
| AbsentBinding (other database) | unknown | ReaderAlias / ReaderReassignment | unknown / unknown |
| CommandAlias | unknown | OrdinalAlias / OrdinalReassignment | match, unknown / match, unknown |
| HelperEscape | unknown | TwoReaders #0 (ReadItems) / #1 (ReadOther) | match / match |
| ReadLoop | match, match | ReadGuard / UsingLifetime | match / match |
| NextResult | unknown | ScalarChannel / NonQueryChannel / RecordsAffected | unknown ×3 |
| Incomplete (unknown shape) | unknown | TextCommand / Conditional | unknown / unknown |
| WithParameter / NullCheck `IsDBNull(1)` | match / match | | |

Per language: **41 rows = 18 bounded matches / 5 mismatches / 18 unknowns**.
The DDL origin replays the same 34 sites (Renamed/Reordered/DuplicateDeclared/
Incomplete use `renamed.sql`/`reordered.sql`/`duplicate.sql`/`if.sql`, others
`base.sql`) plus NameMatch against 11 unsupported bodies (`if`, `nested`, `dynamic`,
`multiple`, `star`, `from`, `union`, `assignment`, `settings`, `return`, `noalias`):
**52 rows = 18 / 5 / 29** per language. Both languages: JSON 36/10/36 (82 rows),
DDL 36/10/58 (104 rows). These counts are over rows of the one explicitly bound
execution per case; every report also keeps the other 33 executions as
`binding-missing` unknown rows, which are excluded from the denominators.

The native smoke (`scripts/sqlserver-result-column-evidence/smoke.py`) scans both
fixture projects through the CLI, binds the five scan artifacts with the #911
helper, admits/compares each case twice (byte-identical JSON and Markdown), checks
every row status/tier, that unknown and mismatch reasons appear in Markdown, Core/
parser/input digests and DDL origin, then runs 20 refusal checks (stale revision,
stale reader digest, mutated declaration/export/facts/DDL source/DDL export bytes,
parameter exports/bindings/manifest/DDL export in the new routes, JSON export as DDL
export, oversized JSON and SQL, malformed SQL, SQLCMD in a comment and a string,
symlinked source, case-only identity ambiguity, existing output preserved). A
missing, unexpected or duplicate case is recorded as an explicit gap and fails the
run. The receipt binds the exact smoke bytes, helper bytes, public base/template/
reader projections, Core/parser bytes and report hashes, and records SQL NOT RUN.

## Validation commands

```sh
dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror \
  --filter 'FullyQualifiedName~SqlServerResultColumnEvidenceTests|FullyQualifiedName~SqlServerDdlEvidenceTests|FullyQualifiedName~SqlServerParameterEvidenceTests|FullyQualifiedName~DnnSqlValidationTests'
python3 -m unittest discover -s scripts/validation -p 'test_sqlserver_*.py' -v
python3 -m unittest discover -s scripts/validation -p test_dnn_sql_harness.py -v
# Commit, then freeze source/HEAD/build outputs; no concurrent build/smoke/commit.
dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror
# Sequentially, at the committed head, in new directories:
python3 scripts/sqlserver-parameter-evidence/smoke.py --output /tmp/json-smoke-new
python3 scripts/sqlserver-ddl-parameter-evidence/smoke.py --json-smoke /tmp/json-smoke-new --output /tmp/ddl-smoke-new
python3 scripts/sqlserver-result-column-evidence/smoke.py --output /tmp/result-smoke-new
./scripts/check-private-paths.sh
git diff --check
```

## Non-goals and remaining unknowns

Not proven: CLR/SQL type conversion, nullability, row cardinality, values, execution
or runtime exceptions; later result sets; catalog/live/deployed correspondence.
Explicitly unknown or absent: SELECT *, FROM/table/view resolution, joins, UNION,
conditional/dynamic/multiple result sets, SELECT assignments, EXEC/nested procedures,
NextResult, reader aliases/reassignment/escape, helper or virtual dispatch, DNN
hydration/provider wrappers (`CBO.FillCollection`, `IHydratable`), DataTable/DataSet
inference, adapters, ORMs, RETURN/OUTPUT/affected-row/scalar channels. This is not a
general SQL interpreter.

## Validated implementation head — 2026-10-25

Implementation commit **`1660a5a8cb0107abb3792788f3eed637987ac30b`** on
`codex/sqlserver-result-column-evidence`, from fresh `origin/dev` = #911 merge
`730610582660fe32354479b35cf80eb2aa954a62`. These results are recorded by a separate
documentation-only commit; no implementation change follows the gates.

| Gate | Result |
| --- | --- |
| Focused Debug -warnaserror (new + DDL + parameter + DNN classes) | 348 passed, 0 failed/skipped (142 new) |
| Python guards | 9 `test_sqlserver_*` (5 new, 4 #911 reuse) + 17 DNN harness passed |
| Full Debug solution -warnaserror, frozen source/HEAD/outputs | 4,930 passed, 0 failed, 3 expected skips; 29m 37s (1,787 s wall); no warnings |
| JSON 52-case smoke (sequential, committed head) | 52/52, 8 / 12 / 32 |
| DDL 52-case smoke reusing those scans | 52/52, 8 / 12 / 32; five scan artifacts per language re-verified |
| Result smoke | JSON 36 / 10 / 36 (82 rows), DDL 36 / 10 / 58 (104 rows), expected = observed, 0 gaps, 20/20 refusals, repeat bytes identical |
| Pinned OSS C#/VB adapter smoke | exit 0; artifacts present; 0.22.10 / 0.10.13; reduced/partial as documented; no database calls in either repo |
| Private-path guard, `git diff --check` | passed |

Skips: Python receipt interop environment unset, opt-in Oracle full golden sweep,
Windows-only deep projectless ASP.NET publish. Receipts are local (outside Git):
`/tmp/tracemap-rc-1660a5a-{json,ddl,result}/smoke-receipt.json`. Result smoke
generator `af02e6722879edf4b8d0205d63297b9af87895ba2d6ca9bd2ed6810886aaf205`, bounded
input `2081b429d67aa09870c583513b2a3d3d96e8a579a8695f0468bfbee4698296e7`; Core
`430b0ee3151b3831fe72c6c3e77a4ec3ce955a87ba29fa3e88e62f10a15b9a88`; parser unchanged
`2dcf224afac347fa1788eeaa6f8db78a17805b82338590b7e51448ab356a54ce`.

Coverage honesty: the pinned OSS repos contain no database operations, so reader
extraction is exercised only by the synthetic corpus and direct Roslyn tests.
Non-.NET adapters, Windows and private/application checks were not run (no non-.NET
code changed). Synthetic results do not establish DNN, SQL-A, catalog or deployed
acceptance. Follow-up: [continue-sqlserver-result-column-evidence.md](../../prompts/continue-sqlserver-result-column-evidence.md).
