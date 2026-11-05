# DNN workspace trial: bounded source completed, SQL refused

Trial date: 2026-11-04. This replays the current workspace entry point against
real public DNN source and install definitions. It is a **bounded partial trial**,
not a full DNN checkout/build, compiled publication acceptance or successful
source/compiled/SQL integration.

The selected upstream is DNN Platform v9.13.8 at
[`33b8fbe440ab665b8423599789db1f34d2a500fc`](https://github.com/dnnsoftware/Dnn.Platform/tree/33b8fbe440ab665b8423599789db1f34d2a500fc).
The pin is for reproducibility. Its nine complete C# files, one consolidated
SQL install file and LICENSE/NOTICE come from the existing
[bounded DNN corpus](DNN_SQL_VALIDATION.md). Original source bytes and upstream
Git revision were preserved. No upstream project was restored or built.

## Actual results

| Channel/check | Observed result | Meaning |
| --- | --- | --- |
| Workspace verify/collect | Source-only run reached `completed` with outcome `partial` | Current workspace entry point works against the real public source slice |
| Source scan | 6,031 facts; `Level3SyntaxAnalysis`; build `NotRun` | Projectless syntax evidence, without semantic/build acceptance |
| Independent source check | Passed unchanged pinned-source markers and native candidate checks | One selected logical operation; three incidental Tier3 ExecuteNonQuery candidates |
| Selected reader join | Unavailable | Those three candidates do not establish the GetRoleGroups reader path |
| Whole SQL install file | `input-size-limit` | Original file is 759,795 bytes, above the 256 KiB per-file admission limit |
| Selected table/procedure definitions | `ddl-malformed-sql` | Owner/qualifier installation placeholders remained unchanged; no substitutions were guessed |
| Compiled publication | Not supplied or attested | No compiled DNN input was admitted |
| Event entry | Not in the selected slice | This trial starts at the provider operation, not a page click |
| SQL/DNN execution | Not run | Static file collection only; no database connection or application runtime |

The SQL whole-file probe used an explicit `.sql` filename alias with identical
bytes and hash. The selected probe projected original lines 7127–7138 and
19151–19158 into a separate file, recording the original path, ranges and hashes.
It did not replace `{databaseOwner}` or `{objectQualifier}`. Both probes used
workspace V3 verification and were refused before collection.

## Compare the known operation

Source inspection and the retained independent checker confirm the same bounded
expectation documented in the DNN corpus:

| Source stage | Independently readable expectation | Remaining boundary |
| --- | --- | --- |
| DNNRoleProvider.cs:285–288,327–336 | GetRoleGroups delegates through the cached internal path to the data provider and collection hydration | Cache execution and dispatch are runtime choices |
| DataProvider.cs:1917–1919 | The wrapper passes the procedure name and portal argument to ExecuteReader | No admitted end-to-end parameter/reader binding |
| SqlDataProvider.cs:72–75 and connection/helper wrappers | Owner/qualifier composition and provider translation intervene | Unresolved configured identities and external PetaPoco terminal |
| Install SQL:19151–19158 | Procedure template declares a portal parameter and selects from RoleGroups | Template is not admitted as an installed schema; star/filter/order remain outside complete shape support |
| RoleGroupInfo/CBO hydration | Consumer reads named fields through hydration helpers | No complete automatic source-to-result contract join |

This is a comparison of inspected source with bounded native evidence, not a
scanner-proven complete path. See the linked corpus document for pinned paths,
line spans and the prior independent expectations.

## Workflow friction and next implementation

The initial local driver placed a SQL probe's workspace config directly in its
SQL input root. Workspace verification refused that overlapping layout. The
corrected probes used a dedicated `input/` subdirectory; the original failure
log/config were retained, and the existing source scan was reused without a
rescan. Keep SQL input roots distinct from the workspace config folder itself.

The next SQL capability should be an explicit, provenance-tracked template
projection contract: select bounded definitions from a larger original, require
owner/qualifier values when they are actually known, and bind both original and
projected bytes plus the projection settings. No implicit default schema or empty
qualifier. Result-shape and source-binding gaps must remain after admission;
accepting syntax alone must not manufacture a complete join.

A matching compiled publication and its actual correspondence evidence are a
separate prerequisite for a three-channel DNN trial. No release binary or a
version label was treated as an exact-source attestation here.

## Retained local evidence

Trial root: `/Users/josephfeser/tracemap-reviews/dnn-trial-01/`.

- `START-HERE.md`: trial outcome and links to each result.
- `workspace/runs/source-only/`: source workspace record, raw index, combined
  index and reports.
- `independent-source-check.log` and `retained-source-recheck.log`: unchanged
  bounded DNN source validation.
- `sql-full-install.log` and `sql-selected-definitions.log`: actual V3 refusals.
- `sql-full-install-initial-layout.log` and `sql-probes/initial-layout-full-install/`:
  retained first-attempt layout failure.
- `trial-receipt.local.json`: generator SHA-256, bounded-input SHA-256, exact
  CLI/Core/parser and helper hashes, public source receipt, projection ranges,
  scan/index/run hashes and observed outcomes.
- `prepare-trial.py` and `finish-trial.py`: exact local generators. The second
  continues the SQL probes from retained source evidence without rescanning.

No private work data was inspected. No scanner/Core implementation changed,
repository history was rewritten, or public communication was sent for this trial.

## Explicit selection follow-up (2026-11-04)

The separate [definition-selection input](../SQL_DEFINITION_SELECTION.md) now
admits the same original file and the two documented whole-batch ranges using
explicit **review-scenario** values: owner `dbo`, qualifier `DnnTrial_`. These
values are hypothetical settings, not a DNN deployment claim. The prior no-settings
refusals above remain expected on the original input route.

Fresh V3 run:
`/Users/josephfeser/tracemap-reviews/dnn-selection-trial-03/runs/first/START-HERE.md`.

| Check | Actual follow-up result |
| --- | --- |
| Source | Same pinned public slice; 6,031 facts, Level3SyntaxAnalysis, build NotRun; unchanged independent source check passed |
| Selected table | `dbo.DnnTrial_RoleGroups`, original line 7127, eight declared columns |
| Selected procedure | `dbo.DnnTrial_GetRoleGroups`, original line 19151, one `@PortalID` input |
| Procedure result | Unknown; no result columns inferred |
| SQL reference | One reference to the separately selected table declaration; no execution/reachability claim |
| SQL SQLite readback | Two objects, one parameter, one result shape, one reference, four gaps |
| Provenance | Original file hash and both projected hashes/settings retained; all three SQL artifact hashes matched the V3 run record |
| Application-call binding | Not established |
| Compiled publication/runtime | Not supplied or executed |

The original file was read in place from the first pinned DNN slice. No original
was modified or silently renamed for this route, and projected SQL bodies were
not persisted. `prepare-selection.py` in the new trial root retains the exact
local configuration generator; its output manifests carry generator/input hashes.
One fresh source scan was explicitly performed to exercise the new V3 selection
collection end to end. This resolves the bounded SQL admission gap, not the
remaining reader binding, compiled correspondence or full application trial.

The final run uses the quote-aware pre-lexing comment-depth guard. The earlier
`dnn-selection-trial-01` remains available as pre-guard trial evidence; the new
`dnn-selection-trial-02` is the depth-guard trial;
`dnn-selection-trial-03` records the final runtime including LF/CRLF line-format validation.

## Conditional source-to-procedure follow-up (2026-11-04)

The separate [source candidate command](../SQL_SOURCE_BINDING.md) now recognizes
five selected wrapper shapes from the pinned public files. It does not modify
previous scan/SQL indexes or promote their missing reader terminal.

Final local report:
`/Users/josephfeser/tracemap-reviews/dnn-binding-trial-02/collected/report.md`.

| Observation | Original source evidence |
| --- | --- |
| Entry | Data/DataProvider.cs:1917–1920, GetRoleGroups(int) forwards the literal procedure identifier and portal argument |
| Provider | Data/SqlDataProvider.cs:72–75, owner/qualifier composition and StoredProcedure intent with the same argument array |
| Connection | Data/SqlDatabaseConnectionProvider.cs:51–54, direct forwarding to selected helper name |
| Helper | Data/PetaPoco/PetaPocoHelper.cs:143–171, formatter translation and external ExecuteReader invocation syntax |
| Formatter | Data/DataUtil.cs:18–47, supported primitive positional formatter path |

Actual status: `conditional-profile-candidate`. Under explicit review-scenario
owner `dbo`, qualifier `DnnTrial_`, and selected provider assumptions, source
`portalId` ordinal zero is associated with selected SQL `@PortalID` ordinal zero
on `dbo.DnnTrial_GetRoleGroups`. Its status is
`conditional-positional-candidate`, with Tier3 syntax evidence.

Nine explicit gaps remain: compiler symbols/overloads; virtual provider dispatch;
configured connection-provider dispatch; actual source owner/qualifier property
values; external PetaPoco parameter/command transport; preprocessor configuration;
runtime invocation; caller cache/event/hydration path; result shapes/type coercion.
Provider StoredProcedure intent is translated to Exec text; no final SqlCommand
CommandType or actual ADO parameter binding is asserted.

The CLI checked all five source files against immutable Git commit blobs,
including their BOM bytes. SQLite readback verified five observations, one
parameter candidate and the source-commit verification flag. The report embeds
regenerated SQL evidence and its digest so the exact admitted parent remains
inspectable after generator changes. No raw source bodies or connection values
were emitted, and no DNN build, application invocation or SQL execution occurred.
