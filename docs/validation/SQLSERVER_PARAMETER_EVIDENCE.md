# Offline SQL Server synthetic parameter evidence

This bounded continuation of #908 and the parameter portion of #870 adds native
admission and parameter comparison. It does not complete either issue or establish
private SQL-A acceptance. PR #909's inventory, scalar-version and deterministic
receipt guards remain intact. The real DNN selected reader route, owner/object
qualifiers, concrete connection identity and consumer-column join remain unavailable.

The separate [bounded DDL route](SQLSERVER_DDL_PARAMETER_EVIDENCE.md) adds a distinct
opt-in origin after #910; this authored JSON route and its 52-case denominator remain
unchanged. DDL does not admit the real DNN route or activate the harness.

## Supported origin and declaration contract

`sqlserver-declarations/1` is an **independently authored synthetic declaration**
format, not SQL DDL extraction, a live catalog, metadata description or runtime
observation. No SQL parser dependency was present in Core; this slice needs no
SQL parser because it does not admit SQL text. There is no general SQL interpreter.
Unsupported origins and types fail admission. Only public/synthetic input is
supported; the CLI is not a private-input sanitizer.

The complete minimal authored example is
[`declarations-base.json`](../../samples/sqlserver-parameter-evidence/declarations-base.json).
All fields shown there are required except optional `provenance` (null for authored
inputs). Required nullable fields must be present. Unknown fields, duplicate JSON
properties, null records, malformed values and extra/missing/unsupported versions
are refused. JSON depth is at most 32, each CLI input at most 4 MiB, declarations at
most 256 procedures and 128 parameters per procedure. Repo is a safe single path
component and revision is an exact lowercase 40-digit Git SHA. Identifiers use the
bounded ASCII letter/underscore followed by letter/digit/underscore policy (128
characters maximum). No implicit aliases, quoting or normalization is implemented.

Each procedure has a four-part logical identity (`server`, `database`, `schema`,
`procedure`), an authored source path/span and parameters with name, SQL type,
size/units, direction, default flag, precision/scale/collation and declaration line.
These identities are synthetic labels, not observations of an installed server.
Source locations assert where the synthetic declaration was authored; they do not
assert an extracted SQL span or deployment correspondence. The fixture base example
points at its own declaration lines. The smoke producer emits one-line declaration
inputs with matching line 1 locations and explicit generator provenance.

Supported type spellings are `Int`, `BigInt`, `Bit`, `NVarChar`, `VarChar`, `Decimal`.
These explicit spellings are the synthetic format vocabulary, not SQL aliases.
Duplicate parameter names are rejected case-insensitively; duplicate exact object
identities are rejected. Exact ordinal object identities remain distinct; case-only
variants are never joined under an inferred server collation.

`sqlserver-schema/1` contains the validated declarations, exact **Core assembly
bytes SHA-256**, canonical public declaration projection SHA-256, extractor version
`sqlserver-synthetic-declarations/1`, rule
`database.sqlserver.synthetic-schema.v1` and Tier2Structural. The producer is the
Core assembly, not a hash of its version label. Admission requires the separate
expected declaration input and expected repo/revision, regenerates the export with
the current assembly and compares the complete canonical object. Payload, generator,
input hash, version or expected-identity substitutions fail closed. A different
Core binary requires regeneration. Self-consistent hashes provide integrity, not
authentication, authority, source truth or installed/deployed correspondence.

Optional generated-input `provenance` contains `generatorSha256` and
`boundedInputSha256`; both must be lowercase SHA-256. The smoke binds exact script
bytes and its public template/selected-call projections. This input producer claim
is recorded, not authenticated. Native export integrity is independently recomputed.

## Source command subset and binding

Fresh scans with C# `csharp-semantic/0.22.7` or VB `vb-semantic/0.10.10` add
`sqlServerParameterState` to semantic DatabaseOperationCandidate facts. Existing
command-level witness summaries and closed path/receipt projections are unchanged.
The independent `sqlserver-call/1` state retains per-parameter names, SQL type,
size, units, direction, Add span and explicit Direction assignment span at the particular execution site. It is
not reconstructed from the aggregate type/direction lists.

Supported forms are a fresh compiler-resolved signed System.Data.SqlClient or
Microsoft.Data.SqlClient SqlCommand local, explicit constant CommandType and
constant CommandText (constructor or assignment), and
`command.Parameters.Add(constantName, SqlDbType.Member[, constantSize])`.
An Add result may be stored in a fresh parameter local and have one or more
constant Direction assignments. Input is the documented provider default for
these exact Add overloads; a null direction span denotes that default, not an
assignment witness. Two independent commands keep independent lists;
unsupported statements cannot borrow the other command's state. No fixture method
is invoked. Other provider/profile/API forms are outside this acceptance matrix.

Branching, loops, try/catch, closures, invalid operations, dynamic calls, command
or parameter aliases (including collection/array aliases), constructor escapes,
helpers, indexed/indirect mutation, reassignments, other parameter/property changes,
unsupported Add overloads and prior execution on the same command remain typed
unknown. The whole method is bounded to 20,000 operations and a call to 128
parameters; exhaustion is an explicit gap. Unsupported syntax/provider identity
never becomes compiler-resolved parameter evidence. Text commands remain outside
procedure comparison even if explicitly bound.

`sqlserver-bindings/1` has repo/revision and a `calls` list. Each entry requires
`factId`, exact two-part `commandText` (`schema.procedure`), full four-part `identity`
and `sourceEvidenceSha256`. The latter is Core's `CallDigest`: canonical JSON in
this order: factId, repo, commitSha, ruleId, evidenceTier, filePath, startLine,
endLine, extractorId, extractorVersion, parsed sqlServerParameterState as `state`.
No raw SQL, snippets, arbitrary properties, connection strings or scan clock enters
that public projection. The native library exposes `CallDigest` for binding tools;
the public fixture smoke mirrors it for ASCII-only synthetic inputs.

Bindings must name a retained call and its exact projected evidence digest; stale
fact IDs/revisions, duplicate bindings, malformed identities or substituted source
state fail admission. A missing binding, absent exact schema identity, unsupported
source extractor/tier/rule or a conflicting command hash produces unknown. Bare
names/hashes cannot join calls. No DNN substitutions or connection identity are
inferred. Input integrity does not turn explicit author-supplied identity into a
runtime observation.

## Comparison policy and limitations

Every row has `database.sqlserver.parameter-comparison.v1`, a conclusion tier,
source rule/tier/extractor/location/revision, admitted schema location and binding.
Bounded comparisons are Tier2Structural; unresolved comparisons are Tier4Unknown.
Source Tier1 evidence is retained separately. An unknown facet alongside a proved
difference keeps the row unknown and preserves the difference reason. Reports are
always `partial` / `no-aggregate-clean-verdict`; zero calls cannot certify a clean
application. Reachability, scanner build/coverage gaps and path truncation remain
separate evidence and are never cleared by a parameter match.

| Subject | Supported policy |
| --- | --- |
| Names/types | Exact ordinal names and explicit vocabulary only. Case-only parameter equivalence stays unknown. Unsupported schema aliases/types fail admission. |
| Required missing/extra parameters | Declared metadata differences with independent reason codes. |
| Defaults | Supplied parameters can be compared; omitted defaulted parameters remain explicitly unknown. No default-expression evaluation. |
| Direction | Input and InputOutput are distinct. SQL OUTPUT admits InputOutput capability, never a pure output-only declaration. Output call versus Input declaration differs; Output call versus InputOutput declaration has unknown input-value behavior. These are bounded metadata comparisons, not execution incompatibility claims. |
| ReturnValue | Retained separately in call state, excluded from procedure parameter matching. Never equated to ExecuteNonQuery affected rows or scalar/result sets. |
| nvarchar | Schema size in UTF-16 code units (byte-pairs), 1–4000 or MAX=-1. Source SqlParameter size in UTF-16 code units; absent/zero/unsupported sizes are unknown. Supplementary characters can occupy two units. |
| varchar | Schema size in bytes, 1–8000 or MAX=-1; this slice does not resolve provider ANSI size/encoding policy and returns unknown. No silent character-to-byte conversion. |
| Fixed-size primitives | Size must be null/not-applicable in declarations. An irrelevant source Size does not become SQL length evidence. |
| Precision/scale/collation | Malformed facets fail admission; supplied supported-shape facets and Decimal comparison remain unknown. Independently proven type/direction differences remain in the reason list. No silent rounding/collation/alias inference. |
| Length differences | Report declared-size differences only; do not establish runtime truncation, encoding behavior or incompatibility. |
| Results | No result-set shape, consumer-column, runtime value, SQL execution, deployment or migration readiness claim. |

Policy references: Microsoft's [SqlParameter.Size](https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlparameter.size),
[nchar/nvarchar length semantics](https://learn.microsoft.com/en-us/sql/t-sql/data-types/nchar-and-nvarchar-transact-sql),
[char/varchar byte lengths](https://learn.microsoft.com/en-us/sql/t-sql/data-types/char-and-varchar-transact-sql)
and [stored-procedure return channels](https://learn.microsoft.com/en-us/sql/relational-databases/stored-procedures/return-data-from-a-stored-procedure).
The ANSI policy is deliberately unsupported because provider/type/encoding details
must be resolved before claiming comparable lengths.

## Native commands and reproduction

All output destinations must be new. No default admission occurs during ordinary
scan or through PostgreSQL validation/harness receipt ingestion.
Schema files are claimed with exclusive CreateNew semantics; comparison reports
are staged beside the destination and promoted without replacement. Concurrent
writers cannot overwrite an earlier winner. Failed report writes
remove their owned staging directory rather than publishing a partial report.
Writers flush encoded bytes and request a disk flush before success/promotion.
This does not guarantee directory-entry durability or recovery from abrupt process
termination/power loss. Cleanup failures are attached to the original exception
under SqlServerOutputCleanupFailure rather than replacing its diagnostic.
Command object initializers and implicit-receiver mutations in VB `With` blocks
remain unsupported and yield unknown parameter state, never stale constructor
command identity. Execution arguments admit only inert literal/default/local/parameter
reads and built-in conversions; helpers, getters, constructors and user conversions
yield unsupported-execution-argument. Extractor versions are C# 0.22.9 and VB 0.10.12.
The later result-column slice bumps them to C# 0.22.10 / VB 0.10.13 for the additive,
independent `sqlServerReaderState`; parameter state and its digest projection are unchanged
([result-column evidence](SQLSERVER_RESULT_COLUMN_EVIDENCE.md)).

```sh
dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll sqlserver-schema produce \
  --declarations samples/sqlserver-parameter-evidence/declarations-base.json \
  --out /tmp/sqlserver-schema-new.json
# For a real retained synthetic scan, first supply its exact repo/revision and
# create matching public declarations and explicit bindings. No short-name join:
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll sqlserver-schema compare \
  --declarations /tmp/public-declarations.json --schema /tmp/public-schema.json \
  --bindings /tmp/public-bindings.json --facts /tmp/public-scan/facts.ndjson \
  --repo synthetic --revision 1111111111111111111111111111111111111111 \
  --out /tmp/sqlserver-comparison-new
python3 scripts/sqlserver-parameter-evidence/smoke.py --output /tmp/sqlserver-parameter-smoke-new
```

The smoke compiles/scans only synthetic fixture projects through the native CLI;
it does not run their application methods. It writes generated input provenance,
checks current Git revision, all five scan artifacts and native admission/reporting,
checks every expected status and compares repeated JSON bytes. Its receipt binds
exact script bytes, only the public template/call projection, report hashes and
explicit SQL NOT RUN. Comparison JSON binds exact Core generator, canonical
public schema/binding/call projection, schema input/generator and source/schema
extractor identities. Markdown is a presentation of that report, not a second
verdict or a retrofit into closed validation receipts.

## Exact synthetic matrix

New independently authored fixtures: **25 methods, 26 execution sites per language**
(52 total). TwoCommands contains two calls. Declaration variants in the tests/smoke
are independently specified per case: MAX, ANSI, Decimal facets, InputOutput/Channels
and omitted-default declarations differ explicitly from the base ReadItem input.
Every smoke report retains all 26 source calls; exactly one is explicitly bound for
that case, and the other 25 stay unknown/missing-binding. Across the 52 explicitly
selected cases: **8 bounded matches, 12 mismatches, 32 unknowns**. Those totals do
not count the intentionally unbound rows as validated comparisons.

| Cases, each in C# and VB | Expected selected row |
| --- | --- |
| Good, Max, InputOutput, TwoCommands size 20 | bounded-match |
| SizeMismatch, TypeMismatch, DirectionMismatch, Extra, Missing, TwoCommands size 40 | mismatch |
| MissingSize, Ansi, Decimal, CaseName, Channels, DefaultOmitted | unknown |
| DynamicName, Conditional, Alias, ParameterAlias, Mutation, ParameterReassignment, HelperBoundary, Text, DynamicCommand, Duplicate | unknown |

The direct Roslyn matrix additionally tests indexed/inline mutation, assigned Add,
collection/array aliases, constructor escapes, clears, wrappers, loops and both
work bounds. Admission tests cover tampered versions/generator/input hashes,
payload substitution, duplicate/null/malformed declarations, duplicate JSON keys,
wrong expected revision, wrong/missing server/database/schema/procedure identity,
stale/duplicate bindings, substituted source-evidence hashes, unsupported facets,
public projection hash isolation and serialization round trips.

The existing DNN synthetic denominator remains unchanged at 12 sites per language,
with retained source paths, SQLite/grouped handoff readback and truncation gaps.
Its existing selected public DNN reader limitation is not cleared by this new corpus.

## Validation gates

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror \
  --filter 'FullyQualifiedName~SqlServerParameterEvidenceTests|FullyQualifiedName~DnnSqlValidationTests'
python3 -m unittest discover -s scripts/validation -p test_dnn_sql_harness.py -v
dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror
TRACEMAP_SKIP_BUILD=1 TRACEMAP_OSS_SMOKE_REPOS=ProjectExtensions.Azure.ServiceBus,community-visual-basic \
  scripts/smoke-open-source-repos.sh /tmp/sqlserver-oss-cache-new /tmp/sqlserver-oss-output-new
./scripts/check-private-paths.sh
git diff --check
```

Actual results and explicit skips are recorded in the spec implementation state.
No private/work-machine inputs, Docker/SSH/image pull/EULA, SQL, IIS or deployment
were used. Live activation and result-column comparison remain separate future
decisions, not required work-machine round trips for this phase.
