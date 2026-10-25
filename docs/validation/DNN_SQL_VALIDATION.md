# Bounded DNN SQL validation (#908)

This is a static public corpus and a prepared, **NOT RUN** optional SQL Server
lane. It is a first slice of #870, not schema-comparison implementation or private
application acceptance. No DNN application, IIS, container or SQL was executed.

## Reproducible inputs and licensing

Canonical upstream: https://github.com/dnnsoftware/Dnn.Platform
Release tag `v9.13.8`, exact commit
`33b8fbe440ab665b8423599789db1f34d2a500fc` (tag resolved and fetched on
2026-10-24). The version is a reproducibility choice, not a recommendation for
running this older CMS. No upstream implementation file is committed here.
The static verifier uses short source expectation markers and retains their
upstream MIT copyright/permission notice in its source.

At that revision, root `LICENSE` is MIT, copyright .NET Foundation and
Contributors. `NOTICE.md` identifies third-party source (Client Dependency and
Log4Net) and library licenses. The selected source files carry .NET Foundation
MIT headers. The opt-in fetch retains LICENSE and NOTICE beside all selected
files; do not redistribute an expanded slice without another license review.
No third-party library code or binaries are copied. The committed C#/VB/SQL
fixtures are independently authored; they are not DNN source copies.

```sh
python3 scripts/dnn-sql-validation/public_slice.py                  # offline; no writes
python3 scripts/dnn-sql-validation/public_slice.py --fetch --destination /tmp/dnn-908-slice
# Destination must be new. No upstream project evaluation, restore or execution.
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan \
  --repo /tmp/dnn-908-slice --out /tmp/dnn-908-static
python3 scripts/dnn-sql-validation/validate_slice.py \
  --slice /tmp/dnn-908-slice --scan /tmp/dnn-908-static
```

The slice has nine complete C# files, one complete consolidated SQL install file,
and LICENSE/NOTICE (12 upstream files). Its sparse Git metadata retains the exact
upstream commit; it never commits reconstructed source as if it were upstream.
`public-slice.json` binds the generator bytes and public path/file-hash projection.
The extra receipt is untracked setup metadata, not an upstream source change.
Default unit tests do not fetch DNN or access the network.
The offline `validate_slice.py` check verifies setup provenance, independent
source markers and the documented native partial observations. It passes only
after exact file-set verification (excluding only root `.git` metadata and
accounting for `public-slice.json`). Links and extra files are rejected. The
unique native `RepoScanned` inventory must contain 10 files: nine C# files and
the setup JSON; LICENSE, NOTICE and `.SqlDataProvider` are outside that inventory.
This check passes only
this bounded static baseline; missing inputs fail, and a changed extractor result
requires source inspection before updating the baseline. It is not authentication
of an upstream checkout or a complete route/SQL validation verdict.

## Independent source expectation (before comparison)

Denominator: **one selected logical operation**, `GetRoleGroups(portalId)`.
One earlier candidate, `GetRoleGroup`, was rejected as a direct-procedure route:
`DNNRoleProvider.GetRoleGroup` (lines 269–272) filters the cached collection, not
`DataProvider.GetRoleGroup`. A matching procedure name would have produced a
wrong join. No third candidate or whole-CMS investigation was undertaken.

All paths below are under `DNN Platform/` at the exact upstream pin. Links use
that revision, never a moving branch.

| Stage | Independently inspected source expectation | Unresolved boundary |
| --- | --- | --- |
| Caller/consumer wiring | `Library/Security/Roles/DNNRoleProvider.cs:285–288,327–336`: public GetRoleGroups uses GetRoleGroupsInternal; cache callback passes dataProvider.GetRoleGroups(portalId) to CBO.FillCollection\<RoleGroupInfo\> | Cache miss and configured provider are runtime choices; no static guaranteed execution |
| Operation wrapper | `Library/Data/DataProvider.cs:1917–1919`: ExecuteReader("GetRoleGroups", portalId) | Virtual dispatch; object-array argument is not an admitted named SQL parameter |
| SQL provider | `Library/Data/SqlDataProvider.cs:72–75`: CommandType.StoredProcedure; DatabaseOwner + ObjectQualifier + procedureName | Both substitutions remain explicit unknowns; no assumed dbo/empty qualifier binding |
| Connection wrapper | `Library/Data/SqlDatabaseConnectionProvider.cs:55–58`: delegates reader to PetaPocoHelper | Configured DatabaseConnectionProvider identity remains separate |
| SQL translation | `Library/Data/PetaPoco/PetaPocoHelper.cs:143–159`; `Library/Data/DataUtil.cs:18–48`: StoredProcedure intent becomes generated `;Exec` text with positional placeholders, passed to external Database.ExecuteReader | Underlying PetaPoco concrete command state is unavailable in the slice. Library project references PetaPoco.Compiled 6.0.524. No claim that the final SqlCommand has StoredProcedure CommandType |
| Procedure DDL | `Website/Providers/DataProviders/SqlDataProvider/DotNetNuke.Schema.SqlDataProvider:19151–19158`: one @PortalID int input; SELECT * from qualified RoleGroups, filter PortalId, order RoleGroupName | This is source setup, not an installed schema or deployment witness |
| Table DDL | Same consolidated file:7127–7138 declares RoleGroupID/PortalID int, RoleGroupName nvarchar(50), nullable Description nvarchar(1000), four nullable audit columns | SELECT * needs admitted table version; migration history not executed |
| Result consumer | `Library/Common/Utilities/CBO.cs:189–192,745–755` generic fill/hydration; `Library/Security/Roles/RoleGroupInfo.cs:131–140` reads RoleGroupId, PortalID, RoleGroupName, Description and invokes FillInternal; `Library/Entities/BaseEntityInfo.cs:91–96` reads audit columns | IHydratable dispatch and SQL identifier/collation behavior not runtime-proven |

No explicit OUTPUT or RETURN channel appears in this selected operation. No
parameter size/direction mismatch is attributed to DNN. The source route and DDL
are independently readable, but the scanner does not prove the complete join.

[Browse the pinned source](https://github.com/dnnsoftware/Dnn.Platform/tree/33b8fbe440ab665b8423599789db1f34d2a500fc/DNN%20Platform).

## Observed native scan versus expectation

Observed on macOS, SDK 10.0.302, TraceMap base
`b4494b01ef7b8b06a5b1ed10b787033469fdd0b9`, with the opt-in sparse slice:

| Cell | Result | Evidence/limit |
| --- | --- | --- |
| Source inventory | supported, partial | 6,031 native facts; Level3SyntaxAnalysis; build NotRun |
| Semantic build/provider binding | unavailable | No projects/references deliberately admitted; csharp.semantic.workspace.v1 gap and manifest gap retained; not a clean build |
| Database operation candidates | supported, bounded | Three Tier3 candidates: PetaPocoHelper.cs:80,125 and SqlDatabaseConnectionProvider.cs:28, all ExecuteNonQuery; rule database.operation.call-pattern.v1 |
| Selected GetRoleGroups reader route | unavailable | No retained database terminal for this operation; missing external Database identity and wrapper/virtual dispatch are not solved by source names |
| SQL install-file admission | unavailable | Original .SqlDataProvider extension is outside SQL inventory; no auto-renaming or qualifier substitution. SQL text seen in C# is not procedure-schema admission |
| Gap preservation | supported | 532 database.operation.call-pattern.v1 gaps, five database.sql.text.v1 gaps, one manifest and one C# workspace gap |
| Parameter/result comparison | unavailable | Existing local enum witness summary is not a per-procedure catalog or consumer-column comparator |
| SQL execution, binary/deployment correspondence | NOT RUN / untested | No server, DNN binaries, historical identity, deployment or private inputs |

Native facts retain rule/tier/path/line/revision/extractor versions. The report
retains partial coverage and its limits. Counts include incidental facts in the
selected complete files; **three candidates are not three validated DNN routes**.
No scanner defect requiring a production change was demonstrated in this slice.

## Synthetic denominator and expectations

Each language has 12 recognized command execution sites: ten reader controls,
one nonquery channel control and one scalar wrapper terminal; wrapper callers
and UnknownWrapper are separate call/unknown-dispatch controls. The ten reader
controls are listed below. Tests run scanner → SQLite and Markdown → retained
wrapper paths → grouped JSON/HTML; no fixture method executes. SQL is scanned
separately because explicit project scoping excludes sibling SQL inputs.

| Cases (both C# and VB) | Independent contract expectation | Current asserted observation |
| --- | --- | --- |
| ReadGood | dbo.ReadItem @Name nvarchar(20) input; Id/Name rows | Constant hash, StoredProcedure, NVarChar/Input witnesses; no schema validation verdict |
| TextControl | Explicit Text command over dbo.Items | Text witness remains distinct from StoredProcedure |
| SizeMismatch | Source size 40 versus DDL size 20 | Same procedure hash; exact size comparison unavailable, never silently a match |
| TypeMismatch | Int versus nvarchar | Int witness retained; per-parameter DDL correlation unavailable |
| DirectionMismatch | Output versus DDL input | Output witness retained; direction comparison unavailable |
| MissingColumn | Consumer reads Missing from Id/Name | Consumer/DDL comparison unavailable |
| RenamedColumn | Consumer reads Name; DDL aliases DisplayName | Comparison unavailable; no false clean result |
| UnknownSchema | other.ReadItem has no admitted schema | Distinct command hash; no procedure-name-only join |
| ConditionalShape / DynamicShape | Conditional column sets / dynamic SQL | StoredProcedure intent only; no inferred stable result shape |
| Channels | affected-row return, SQL RETURN 7, @Count OUTPUT and two rowsets are distinct | Output and ReturnValue summaries retained; no invented equivalence to ExecuteNonQuery's integer |
| Wrapper / Execute / UnknownWrapper | Intermethod string input and unknown provider dispatch | Wrapper path retained; dynamic text stays dynamic; no database terminal invented for UnknownWrapper |

The fixture uses a legacy provider only to exercise existing public API identity;
scoped CS0618/BC40000 suppressions document the intentional static compatibility
fixture. VB uses an explicit CommandText assignment: its constructor-only form
observed unavailable text in the bounded witness walker. This is documented
unsupported constructor state, not procedure-schema evidence. Parameters carry
no runtime values. Conditional/dynamic SQL is not interpreted by static tests.

### Missing production seam, deferred to a scope decision

`DatabaseCommandState` provides bounded method-local witness summaries;
`DatabaseCallEvidence` is an allowlisted projection. Neither admits per-parameter
names/sizes or SQL Server catalogs. `SqlFileExtractor` produces SQL text/shape
facts, not procedure contracts. Existing schema-change impact operates on SQL
shapes; PostgreSQL `TraceMap.SqlValidation` and summary-ingestion contracts do not
supply this SQL Server comparison. Do not retrofit synthetic logs into those
closed contracts or upgrade evidence tiers.

Proposed bounded follow-up: an explicit versioned public/sanitized schema export
with repo/revision, exact generator and projected bounded-input hashes; exact
server/database/schema/object identity, separate placeholder substitution
receipt, parameter names/types/length units/directions/defaults, versioned table
columns and result-set ordinal/column metadata. Constant declarations,
metadata-described results, conditional alternatives, unknown/dynamic results
and observed execution must be separate states with rule/tier/span/extractor
limits. Exact admitted identity is required before comparing a call/consumer;
missing, ambiguous or stale inputs produce explicit gaps. No general SQL
interpreter or source-name framework binding is proposed by this harness.

## Optional SQL Server lane: prepared, NOT RUN

Microsoft supports SQL Server Linux containers on native Intel/AMD x86-64 Linux
hosts; ARM emulation is outside this baseline. See the official
[container quickstart](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/quickstart-install-docker?view=sql-server-linux-ver16).
The static guard workflow runs the Python guards on Linux and Windows with
no Docker service or network fetch in the test commands. The resolver has
explicit LF checkout attributes because its recorded provenance binds exact bytes.

The versioned `2022-CU20-ubuntu-22.04` registry manifest and config were read and
SHA-256 verified without fetching layers. `image-resolution.json` records the
exact resolver generator and public metadata-projection hash. **Tested server
version remains null.** Re-resolution is opt-in and must be reviewed if it differs.

```sh
python3 scripts/dnn-sql-validation/resolve_image.py                 # offline
python3 scripts/dnn-sql-validation/resolve_image.py --resolve       # registry metadata only
python3 scripts/dnn-sql-validation/harness.py                       # offline plan, NOT RUN
python3 -m unittest discover -s scripts/validation -p test_dnn_sql_harness.py -v
```

Immutable reference prepared (not pulled):
`mcr.microsoft.com/mssql/server@sha256:7c29dfbac885ad7519e219c7fe4aee0e67283e21a10e9c252d13b0fbde1866f8`.

### Consolidated prerequisites and later operator runbook

`dockercache` is only a candidate name: no SSH, host probe, Docker daemon call,
pull, provisioning or SQL activation is authorized by preparing this document.
The human must approve all of these together before a later live run:

- Exact host, Docker context and endpoint; authorized access and native Linux
  x86-64 CPU, >=2 available CPUs, >=8 GiB host memory, >=10 GiB free disk.
- Nonproduction Developer edition and EULA; operator-held disposable SA password
  entered at a prompt, never committed, printed, mounted or passed as argv text.
  Docker administrators can inspect container environment; use a throwaway secret.
- Container limits: 2 CPUs, 6 GiB memory/swap ceiling, SQL memory 2 GiB,
  2 GiB owned ephemeral tmpfs. Host disk/image budget is an operator prerequisite,
  not enforceable by this script on a remote Docker root.
- Network none, no published ports, no privileged mode, no source mounts,
  no persistent volume; isolated TraceMapSynthetic908 database in a new container.
- Approval to pull this exact digest separately and to destroy only the newly
  created labeled container and its ephemeral data. No prune or existing-volume
  cleanup. Unknown resources are preserved.
- Separate scope decision before implementing schema admission/comparison above.
  This does not block static validation or future disposable SQL tests.

Only after that approval, the operator can inspect the chosen context (substitute
actual reviewed values; these examples are not executable default configuration):

```sh
# Read-only preflight, only after host access is authorized:
docker context inspect "${REVIEWED_CONTEXT:?Set the explicitly approved context}" --format '{{.Endpoints.docker.Host}}'
docker --context "${REVIEWED_CONTEXT:?Set the explicitly approved context}" info --format '{{json .}}'
# Also inspect free disk on the approved host using its authorized access route.
# Separately approved pull; harness itself never pulls:
docker --context "${REVIEWED_CONTEXT:?Set the explicitly approved context}" pull \
  mcr.microsoft.com/mssql/server@sha256:7c29dfbac885ad7519e219c7fe4aee0e67283e21a10e9c252d13b0fbde1866f8
docker --context "${REVIEWED_CONTEXT:?Set the explicitly approved context}" image inspect \
  mcr.microsoft.com/mssql/server@sha256:7c29dfbac885ad7519e219c7fe4aee0e67283e21a10e9c252d13b0fbde1866f8 \
  --format '{{json .RepoDigests}} {{.Os}}/{{.Architecture}}'
# New local output path; actual context AND endpoint mandatory; no default context:
python3 scripts/dnn-sql-validation/harness.py --activate \
  --context "${REVIEWED_CONTEXT:?Set the explicitly approved context}" --expected-endpoint "${REVIEWED_ENDPOINT:?Set the exact approved endpoint}" \
  --approve-host --approve-resources --accept-eula --approve-disposable \
  --output /tmp/dnn-908-live-new
```

The harness rechecks endpoint, daemon architecture/resources, enforceable memory/swap/CPU/PID limits and local RepoDigests
before asking for a password or creating anything. It never starts IIS/DNN.
Twelve readiness attempts have a 10-second command timeout plus 2-second retry
delay. SQL commands use bounded login/query timeouts. Failures retain bounded,
credential-redacted container logs when ownership is verified; raw daemon error
text is not printed. Process termination/daemon failure can interrupt cleanup;
retain the output directory and inspect the recorded owner label before manual
removal. Never remove an unknown container by name or prune the host.

`schema.sql` and `seed.sql` are synthetic setup only. `metadata.sql` describes
parameters and first result metadata; it is separate from `execution.sql`, which
asserts a synthetic row, SQL RETURN and OUTPUT, and captures two Channels rowsets.
Result-set ordinal/type verification by an ADO.NET client and execution of the
negative mismatch cases remain deferred; sqlcmd output is not a schema comparator.
The run produces distinct logs plus a receipt with observed version/image ID,
fixed digest, generator hash, public fixture input hashes, log hashes and explicit
limits. These harness-local receipts are not a native accepted schema export or
SQL validation summary. No historical binary or deployment correspondence follows.

## Static validation commands

```sh
dotnet restore samples/dnn-sql-validation/csharp/Contracts.csproj
dotnet restore samples/dnn-sql-validation/vb/Contracts.vbproj
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror \
  --filter FullyQualifiedName~DnnSqlValidationTests
python3 -m unittest discover -s scripts/validation -p test_dnn_sql_harness.py -v
# Full lane: adding compile-only fixture references touches TraceMap.Tests.csproj.
dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan \
  --repo samples/dnn-sql-validation --project csharp/Contracts.csproj \
  --out /tmp/dnn-908-synthetic-smoke
```

Unit test execution is offline after ordinary SDK/package restore. First-time
restore may use NuGet; the tests do not download DNN or require SQL, IIS or private
inputs. The fixture ProjectReferences select the full Debug suite even though no
production scanner/reporting code changed; final merge policy still applies. Windows/pinned language-adapter smokes and live
SQL remain NOT RUN until explicitly performed. See the spec implementation state
for the actual local results, and the follow-up prompt for the single handoff.
