# Workspace workflow consolidation state

Branch: `codex/workspace-workflow-consolidation`.

## Authorized scope

Local workflow inventory, consolidation story, runnable initial workflow, tests,
getting-started documentation and compiled-admission design/contract proof. No
push, PR, private-input collection, history rewrite, or retirement of existing
scripts in this slice. Existing collection runbook is a draft.

## Decisions

- Native .NET commands own configuration and execution; PowerShell is an optional
  development launcher, not another evidence implementation.
- Start with a complete multi-repository source collection path: init, verify,
  collect, combine and render. Preserve existing scan/combine/report contracts.
- Configuration is strict, local, editable and versioned. Init never overwrites
  user files. Optional repository selection is an explicit filter.
- Separate build configuration, SDK selection, target framework and tool identity.
  No floating latest SDK and no executable discovery through `Debug/net10.0`.
- Compiled publication admission and private SQL integration remain separately
  tracked capabilities. Do not flatten them into an unbound generic merge.
- The reported historical schema `webforms-compile-web-config.v1` has not been
  located in the current source. Do not silently migrate it as a native config.

## Inventory observations

See [workflow inventory](workflow-inventory.md). Current native attachment takes
one retained source parent. The legacy merger combines web and backend source
indexes. More investigation is required before declaring those workflows
equivalent or retiring wrappers.

## Validation

- Debug build with `-warnaserror`: zero warnings/errors.
- Focused `WorkspaceCommandTests`: 14 passed, zero failed/skipped. Includes real
  two-source scan/combine/report/Web Forms rendering, malformed configuration,
  selection refusal, output preservation and failed-stage retention.
- Release publish and the actual `scripts/tm.ps1` entry point succeeded. The
  first replay found PowerShell common-parameter capture of `--out`; the launcher
  now remains a simple script and forwards all CLI arguments unchanged after
  its optional build flags.
- `scripts/tests/Test-WorkspaceLauncher.ps1` passed on macOS with PowerShell 7:
  full two-repository collection and one-repository filtered collection. Both
  correctly remain partial because the fixtures have syntax-only source.
- Private local replay:
  `~/.local/share/tracemap-workflow-trials/20261103-launcher-replay-01/`.
- Full Debug `dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror` passed:
  **5,839 passed, 3 skipped, 0 failed**, duration 39m49s, exit 0. Includes the 14
  new workspace cases. Log: `/tmp/tracemap-workspace-full-suite.log`.
- Skips: optional full Oracle golden sweep, native HTTP receipt interop, and
  Windows ASP.NET publication chain/binding test. This does not establish those
  opt-in/platform lanes or fresh Windows publication acceptance.
- Document links and `git diff --check` passed.
- Follow-up compiled-admission contract proof: 38 focused tests passed with
  `-warnaserror` (workspace plus compiled-attachment combine lanes). The two new
  cases admit an additional framework source while retaining the web parent,
  and reject substituting the framework as parent before output creation.
  They use synthetic manifests and a public compiled assembly fixture, not a
  full Web Forms publication or a workspace compiled CLI run.

Windows publication and private SQL acceptance are not established by local
macOS testing. No scanner/reducer extraction behavior changed. No existing
wrapper was removed and no parity claim is made for legacy workbenches.

## Implemented entry points

`workspace init|verify|collect` uses `workspace.local.json` with optional
repository-ID scope filtering. Fresh source scans feed existing combine/report
commands; `webFormsReport` optionally adds the existing packet. Init refuses
overwrites; collection refuses existing/overlapping destinations, preserves stage
failures and records local CLI/input hashes. There is no resume/cache in this
initial source path. V1 remains source-only. Opt-in `tracemap-workspace.v2` now
accepts `compiledReview: { repositoryId, nativeConfigPath }` and consumes that
unchanged prepared native fresh config for the web source. Native checkpoints
select the raw scan and its report bundle; framework source is scanned separately.
The workspace combines raw indexes, rechecks consumed bytes and links both report
contexts from `START-HERE.md`. Native gaps remain authoritative and SQL is absent.

Start page: `docs/WORKSPACE_GETTING_STARTED.md`. Reference:
`docs/WORKSPACE_CONFIGURATION.md`. Synthetic trial:
`scripts/tests/Test-WorkspaceLauncher.ps1`. Optional development launcher:
`scripts/tm.ps1 -Build version` then ordinary commands without `-Build`.

## Next steps

2026-11-04 SQL slice authorized: reuse the pinned ScriptDom parser, not a new
regex SQL grammar. Implement a distinct local-only definition-set contract;
never mark a private dump publicSynthetic. First scope: bounded offline schema
definitions, procedure parameter declarations, AST references/dependencies,
supported single-result projections and explicit unknowns. No SQL execution,
connection, literal/body output or automatic source-call/deployment binding.
Application repo/commit is context only, not the database export revision.
Keep SQL sidecars distinct from existing source/compiled scan indexes.

2026-11-04: owner authorized implementation of the compiled milestone. Native
settings remain unchanged and authoritative for web scope; no inferred
attestation or config projection. Retained attach remains a separate follow-up.
At that compiled milestone SQL admission was deferred; the separately authorized SQL implementation below now supersedes that boundary.

Compiled validation so far:

- 113 focused Debug tests passed with `-warnaserror`: workspace, compiled
  admission, native preflight and attachment-combine lanes.
- Built synthetic trial passed after fixing a native PDB option bug: aggregate
  default 128 was forwarded as a PDB limit despite Core's maximum of 32. Native
  fresh and attach now use the smaller lane ceiling; preflight rejects excessive
  explicit PDB selections. Two option-bound tests and one refusal test pin this.
- Trial 02 completed both two-source and web-only collections. The web scan had
  semantic analysis, an admitted/bound primary, complete bounded PDB coverage,
  effective PDB cap 32 and seven exact source/metadata joins. Top-level outcome
  correctly remains partial; no authenticated build or runtime claim.
- Trial root: `~/.local/share/tracemap-workflow-trials/20261104-compiled-replay-02/`.
  Failed trial 01 and its bounded toy-only diagnostics are preserved separately.
- Final launcher replay from the sibling upgrade-authority working directory
  passed at `20261104-compiled-replay-03`: both two-source and web-only collections
  completed with expected partial coverage, linked native workbenches and raw
  compiled/PDB/IL evidence. Confirms current-directory independence.
- Full Debug `dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror` passed:
  **5,863 passed, 3 skipped, 0 failed**, duration 40m02s, exit 0. Log:
  `/tmp/tracemap-workspace-compiled-full-suite.log`. Skips remain optional Oracle
  goldens, native HTTP receipt interop and Windows ASP.NET publication. This
  validates the implementation worktree before the local checkpoint commits;
  it is not a fresh Windows or real-application acceptance result.
- Release tool published and tested from both the TraceMap checkout and the
  upgrade-authority working directory. The published tool bytes used by retained
  native runs are preserved; no post-checkpoint rebuild is needed for the owner
  synthetic trial. Current guide: `docs/WORKSPACE_COMPILED_GETTING_STARTED.md`.

Owner executed the synthetic launcher trial and acknowledged the result. Both
`owner-trial-01` collections had stage `completed` and expected partial coverage
when inspected locally. This is synthetic usability evidence, not real-application
or Windows publication acceptance. The shell copy/paste problem was resolved with
a zsh-compatible one-line command; the guide now separates shell examples and
explains existing completed output.

Owner's compiled synthetic trial also passed: pasted terminal output ends with
`Workspace launcher replay passed`. Persisted `compiled-trial-01` records were
checked: first collection stage completed with two sources (190 combined facts
reported); web-only collection stage completed with one source (172 facts).
Both retain compiled review data for `web` and expected partial outcome. This
establishes successful owner execution, not real-application or Windows acceptance.

The fresh compiled milestone is locally verified and owner-executed. Later
attach uses separate raw web/framework/compiled indexes
plus the existing explicit parent/child contract. Historical schema migration is
still unverified; the reader remains unlocated.

No push or PR. Native PDB correction is checkpoint `dfba22eb`; workspace
integration is the following separate local checkpoint (see Git HEAD). Recheck
the final head before a future merge. Real application/Windows acceptance remains
a separate follow-up; owner synthetic execution is complete.


## Offline SQL implementation and validation (2026-11-04)

- Implemented distinct `sqlserver-local-definitions-input/1` / `sqlserver-definitions verify|collect`
  over the retained ScriptDom 170.191.0 TSql160 parser. Existing public-synthetic
  input gates are unchanged. No private dump was inspected or relabeled.
- Added V3 workspace `sqlEvidence`; context repository ID/HEAD and explicit scope
  must agree. SQL is a separate local JSON/SQLite/report sidecar; it never enters
  generic combine and never automatically binds application calls. V1/V2 preserved.
- New 36 unit/integration cases cover declarations, ordered results, line spans,
  parameters, references, unknowns, mutation/context/scope/digest failures, strict
  manifests, literal/default omission, SQLite readback, output preservation,
  cancellation and symlink/late-drift publication refusal. A new path test exposed
  macOS /var ancestor aliases through symlinks; SQL overlap checks now resolve those
  recursively. The added tests plus retained DNN/catalog lane passed 42/42.
- Earlier broad focused SQL/workspace run passed 474/474. Final full Debug
  `-warnaserror` PASSED: 5,899 passed, 3 skipped, 0 failed; exit 0, 35.227 minutes.
  Log `/tmp/tracemap-workspace-sql-full-suite.log`.
- Release launcher published successfully. Actual `-WithSql` trial ran from
  upgrade-authority cwd at
  `~/.local/share/tracemap-workflow-trials/20261104-sql-replay-01/`.
  Both two-repository and web-only runs completed partial. SQL readback: four
  objects, three parameters, three result shapes, two references, eight gaps;
  ReadItems has Label,Id; filtered/dynamic procedures stay unknown. All SQL output
  hashes match the run record. This is an authored managed fixture, not IIS proof.
- Opt-in public corpus smoke passed four cases at `/tmp/tracemap-sql-public-corpus-01`:
  two pinned WWI procedures with declared parameters/references and unknown
  complex shapes, plus three expected refusals (DNN unchanged qualifier template,
  whole DNN install file over the limit, WWI table setup with top-level EXEC).
  Exact public source bytes and licenses retained; no substitution or SQL execution.
- DNN Python harness guards passed 20/20. All four retained CLI smokes PASSED
  at `/tmp/tracemap-workspace-sql-retained-01`: JSON and DDL parameter routes each
  52 cases (8 match / 12 mismatch / 32 unknown); result JSON 36/10/36, DDL 36/10/58
  with 20 refusals; table 64 cases/language, totals 44/10/88 with 24 refusals.
  The old script denominators and public admission contracts were unchanged.
- Full-suite unified exec session 20747 completed successfully. Source files stayed
  frozen throughout the run. Published runtime remains at `artifacts/workspace-cli/Release`;
  exact bytes are pinned by the retained synthetic/native/public receipts. Do not
  rebuild that runtime and expect old native runs to resume under new tool hashes.
  No push or PR authorized in this slice.
- Canonical SQL owner instructions: `docs/WORKSPACE_SQL_GETTING_STARTED.md`.
  Nothing pushed and no PR or public message created. No source/SQL-call binding,
  database export revision, live SQL or private application acceptance is claimed.

- Final documentation relative-link checks, PowerShell syntax parsing and
  `git diff --check` passed. The three skips remain optional Oracle golden sweep,
  native HTTP receipt interop and Windows ASP.NET publication acceptance; these
  are not established by the managed synthetic trial.
- Owner SQL trial PASSED on 2026-11-04, launched from upgrade-authority:
  `~/tracemap-reviews/sql-trial-01`. Both first (two sources,
  190 combined facts) and web-only (one source, 172 combined facts) reached
  stage completed with partial coverage. Read back both V3 run records and SQL
  SQLite indexes; all three SQL artifact hashes matched each run record. Each
  SQL sidecar contains four objects, three parameters, three shapes, two
  references and eight gaps. ReadItems is complete with Label,Id; ReadFiltered
  and ReadDynamic remain unknown. Source-call binding remains not-established.
  The pasted terminal output ends with Workspace launcher replay passed.
- Remaining capability work: broader dump/setup compatibility, explicit
  application-call-to-SQL bindings, retained native attachment orchestration and
  scan reuse/retention. The bounded offline collector is implemented, not a full
  database migration engine or an application/runtime acceptance claim.


## Real application trial preparation (2026-11-04)

Owner authorized the next bounded real-application trial after the synthetic SQL
trial passed. Input selection is pending: existing workspace configuration, or
web/framework repository roots, prepared native review config (or publication
folder) and schema-only SQL definition folder. The repository has no root
`configuration/` folder. No broader filesystem search, private source/SQL read,
collection, compilation, database connection or attestation has been performed
for this trial. If inputs live only in the work environment, prepare a handoff
for its authorized local agent rather than transferring their contents here.


## Bounded public DNN application trial (2026-11-04)

Owner chose DNN or TraceMap; selected public DNN because it covers the relevant
Web Forms/SQL Server combination. The prior pending input selection is resolved
for this bounded public trial; no private application paths are needed.

- Used retained pin 33b8fbe440ab665b8423599789db1f34d2a500fc via the existing
  public_slice.py helper. No full DNN project evaluation/build or runtime.
- Actual trial root `~/tracemap-reviews/dnn-trial-01/`.
  Source workspace collection completed partial: 6,031 facts, Level3SyntaxAnalysis,
  build NotRun. The unchanged independent DNN check passed twice (initial and
  retained-source recheck). Three incidental Tier3 ExecuteNonQuery candidates;
  the selected GetRoleGroups reader join remains unavailable.
- Actual V3 verification refused the 759,795-byte install file (input-size-limit)
  and unchanged bounded table/procedure projection (ddl-malformed-sql). No owner/
  qualifier substitution. No SQL collection, SQL connection or execution.
- Initial SQL config/input folder overlap was refused. Preserved first log/config;
  corrected to a dedicated input subfolder and reused the source scan. No rescan.
- No compiled publication was supplied or attested; no page event is in this slice.
  This does not satisfy full source/compiled/SQL DNN acceptance. Record the bounded
  trial as completed with explicit gaps, not the broader real-application gate.
- Public-safe results and next capability concept are in
  docs/validation/DNN_WORKSPACE_TRIAL.md; exact local generator/input/tool hashes
  and outputs are in trial-receipt.local.json. No Core/CLI implementation changed.
- Prioritize a provenance-tracked, explicit template/definition projection contract
  before DNN source-to-procedure binding. Do not silently strip statements, guess
  deployment qualifiers or turn syntax admission into a complete result claim.


## SQL definition selection implementation (2026-11-04, complete; final results below)

Owner authorized the next implementation after the bounded DNN trial. Add a
separate local-only selection-input contract to the existing sqlserver-definitions
verify/collect route. Whole-batch line ranges, expected object identities and
explicit DNN owner/qualifier settings are required; no guessed defaults. Source
originals are read-only, bounded separately from selected parser inputs, and
original/projected hashes plus exact generator/parser identities are retained.
Map output evidence back to original file lines; do not persist SQL bodies.
Selection omits script/session/migration context, so selected procedure result
shapes remain unknown. Table columns describe the selected declaration only.
Review-scenario values are distinguished from owner-supplied values; neither is
proof of deployment. No source-call binding, compiled attestation or SQL execution.
Preserve v1 local and all public-synthetic contracts; full Debug gate required.


### Selection implementation status (source frozen for final gate)

- Implemented SqlServerDefinitionSelection with separate input
  sqlserver-local-definition-selection-input/1 and output
  sqlserver-local-selected-definitions/1. Whole-batch boundaries use the existing
  pinned ScriptDom lexer; original limits 8 files/4 MiB each/16 MiB total/200000
  lines/1000000 tokens per file, while selected parser limits remain unchanged.
- Explicit ASCII owner, qualifier (including explicitly empty) and valuesOrigin;
  refuse literal/comment/ambiguous quoted-token macros, unsafe settings, identity
  mismatch, fragments, overlapping selections, extra definitions and inadmissible
  top-level statements.
  Keep originals read-only and projected SQL only in memory. Original lines,
  hashes, settings and replacement counts retained in Selection evidence.
- CLI verify/collect dispatches on the separate manifest version. Workspace V3
  now records actual SourceFiles hashes, not the virtual selected parser paths.
  Existing public/v1 local contracts remain separate. No application binding.
- Added 32 tests (30 selection plus two workspace cases). New/workspace lane
  passed 41/41; broad retained SQL/local/DNN/workspace selection lane passed
  475/475; Debug/Release builds passed with zero warnings/errors.
- Actual Release DNN selection trial at
  `~/tracemap-reviews/dnn-selection-trial-01` passed V3 verify and
  fresh collect. Reused the original pinned public source bytes from dnn-trial-01;
  one fresh source scan exercised the new selection route. Source 6031 facts,
  syntax-only/NotRun; independent source validator passed. SQL SQLite readback
  2 objects/1 parameter/1 unknown shape/1 declared-table reference/4 gaps.
  Review settings dbo + DnnTrial_, not a deployment assertion. Table has eight
  declared columns; proc @PortalID; original spans 7127 and 19151. All SQL output
  hashes matched run inputs. No compiled publication or runtime execution.
- Public corpus v2 smoke PASSED all five cases at
  `/tmp/tracemap-sql-selection-public-corpus-01`: added explicit DNN selection,
  retained the first four outcomes and three expected refusals. No SQL execution.
- First full Debug run (session 88134, log `/tmp/tracemap-sql-selection-full-suite.log`)
  was deliberately interrupted before completion to fix a review finding: the
  ScriptDom lexer recursively handles nested block comments, so the larger original
  input bound also needs a pre-lexing depth ceiling. No pass is claimed for that
  incomplete run. Added a quote/identifier-aware depth guard (128) and six regression
  cases, including a 100000-depth input rejected before the lexer. Selection/local/
  workspace lane passed 74/74. New total tests: 38 added (36 selection + two workspace).
  Final runtime/trial and full gate are being rerun; final expected pass count 5937.
- All four retained parameter/DDL/result/table CLI smokes PASSED in session
  72157, log `/tmp/tracemap-sql-selection-retained.log`, outputs
  `/tmp/tracemap-sql-selection-retained-01`. JSON/DDL parameters each 52 cases
  (8/12/32); result JSON 36/10/36 and DDL 36/10/58 with 20 refusals; table totals
  44/10/88 across 64 cases/language with 24 refusals. Current Debug runtime;
  no public gate or retained denominator was weakened.
- Current published Release runtime is pinned by the new DNN/native output and
  public corpus receipts. Leave those bytes unchanged; commit only after gates.
- Docs: SQL_DEFINITION_SELECTION.md, updated SQL guide/config/validation, new
  rule catalog entry, and DNN_WORKSPACE_TRIAL.md follow-up results. Final doc links,
  diff checks and gate results remain to record. No push or PR authorized.

- Public v2 receipt generator/bounded-input hashes and all new documentation relative
  links were verified. `git diff --check` passed. Only full-suite completion and
  final local state/commit remain for this selection implementation milestone.


### Final selection runtime and gate after depth fix

- Final code is frozen. Full Debug -warnaserror runs in session 41221, log
  `/tmp/tracemap-sql-selection-full-suite-final.log`. Expected 5937 passed plus
  three retained skips. Session 88134 was interrupted for the depth fix; never
  report its incomplete result as the final gate.
- Final focused selection/local/workspace lane passed 74/74, zero warnings/errors;
  38 newly added cases total (36 selection + two workspace). Review discovered
  ScriptDom's recursive multiline-comment lexer rule in upstream TSql160.g;
  the new original input bound now has a quote-aware depth guard at 128 before
  lexing. Six regressions include 100000-depth rejection and escaped quoted text.
- Final Release runtime published. Fresh actual DNN run now at
  `~/tracemap-reviews/dnn-selection-trial-02/runs/first/` completed
  partial with 6031 source facts and explicit SQL selection. Trial 01 is retained
  as pre-depth-fix evidence, not the final runtime receipt.
- Final public v2 corpus PASSED five cases at
  `/tmp/tracemap-sql-selection-public-corpus-02`; log
  `/tmp/tracemap-sql-selection-public-corpus-final.log`.
- All four final retained CLI smokes PASSED at `/tmp/tracemap-sql-selection-retained-02`,
  log `/tmp/tracemap-sql-selection-retained-final.log`; unchanged denominators and
  refusals (parameter JSON/DDL 8/12/32 each; results 36/10/36 and 36/10/58 plus
  20 refusals; table 44/10/88 plus 24 refusals). Do not rebuild Debug/Release
  or commit until full gate finishes. Docs/state can be finalized meanwhile.
- Final DNN SQL readback/hashes, source-check log and docs links verified: two
  objects, one parameter, one unknown shape, one reference, four gaps; current
  index generator matches the published CLI. Remaining: finish full gate, update
  tasks/results and make a local commit. No push/PR.


### Final line-format refusal regression

- Found and closed one remaining line-map ambiguity: selection splitting uses LF
  while ScriptDom also recognizes lone CR line breaks. The larger-original route
  now explicitly accepts LF/CRLF and refuses lone CR before lexing. Two tests
  cover CR-only and mixed-line input refusal; CRLF/BOM positive cases remain green.
- Focused final selection/local/workspace lane PASSED 76/76, zero warnings/errors.
  Total new cases now 40 (38 selection + two workspace), expected full 5939+3 skips.
- Session 41221 was deliberately interrupted for this fix; its incomplete log is
  `/tmp/tracemap-sql-selection-full-suite-final.log`. No pass claimed. Earlier
  session 88134 was interrupted for the comment-depth fix. Both are preserved.
- Final code is now frozen. Runtime publication, final DNN trial03 and final
  public/retained/full gates are restarting. Use new log/session paths recorded
  below; do not rely on prior trial01/02 generator hashes for this final code.


### Authoritative final gate/run pointers (after both refusal regressions)

- Full Debug -warnaserror PASSED in session 57292: 5,939 passed, three skipped,
  zero failed; exit 0, 38.8664 minutes. Authoritative log
  `/tmp/tracemap-sql-selection-full-suite-complete.log`. All 40 added cases passed;
  C# and runtime bytes stayed frozen throughout this final gate. Earlier sessions 88134 and 41221 were interrupted for
  comment-depth and CR-only line-format fixes; no final pass claimed for those.
- Final focused lane 76/76 passed. Final Release published. Final DNN selection
  trial is now `~/tracemap-reviews/dnn-selection-trial-03/runs/first`:
  V3 verify/collect completed partial, source 6031 facts; final public v2 corpus
  PASSED five cases at `/tmp/tracemap-sql-selection-public-corpus-03`, log
  `/tmp/tracemap-sql-selection-public-corpus-complete.log`.
- Final retained four smokes PASSED at `/tmp/tracemap-sql-selection-retained-03`,
  log `/tmp/tracemap-sql-selection-retained-complete.log` (session 49509). All
  original counts/refusals unchanged: parameter JSON/DDL each 8/12/32, results
  JSON 36/10/36 and DDL 36/10/58 plus 20 refusals, table 44/10/88 plus 24 refusals.
  Source-check log for trial03 is beside its config. Prior trial01/02 retained;
  they pin earlier code bytes and are not the final trial for line-format guard.
- Remaining: inspect final source-check and SQL readback/output hashes, finish
  retained/full gates, update docs/state/tasks to final pointers and exact results,
  run diff/doc checks, then commit locally. No push/PR or external messages.

- Final trial03 source check, SQLite counts (2/1/1/1/4), original/output hashes,
  index generator hash, public corpus v2 receipt and docs relative links verified.
  Full suite 57292 completed successfully; implementation and validation complete.


## Final selection milestone result (2026-11-04)

- Completed 40 new tests (38 selection, two workspace). Focused final lane 76/76;
  broader pre-guard retained SQL/local/DNN lane 475/475; final full Debug solution
  `-warnaserror`: 5,939 passed / three skipped / zero failed, exit 0, 38.8664 minutes.
- Three skips remain optional Oracle golden sweep, native HTTP receipt interop and
  Windows ASP.NET publication acceptance. Neither interrupted earlier gate is
  reported as a pass. Both fixes are pinned by regression tests in the final gate.
- Final public corpus v2 five cases and all four retained CLI smokes passed with
  original counts/refusals. Final DNN trial03 and its generator/output hashes are
  the authoritative current runtime example; trials01/02 remain historical.
- Canonical selection guide: docs/SQL_DEFINITION_SELECTION.md. Updated SQL
  getting-started/configuration/validation, DNN trial results, rule catalog and
  task checkboxes. Final links and diff checks passed. No Core/CLI changes after
  final validation. Keep the published runtime bytes pinned by trial03 receipts.
- This completes explicit bounded SQL definition selection and substitution only.
  Application-call binding, full installed-schema interpretation, native compiled
  DNN correspondence and full real-application acceptance remain separate work.
  No SQL executed, private input inspected, original source rewritten, push or
  PR/public message created.


## Source-to-procedure candidate implementation (2026-11-04, complete)

Owner authorized one DNN GetRoleGroups operation. The public slice has no compiler
project or PetaPoco implementation. Implement a distinct local-only DNN-style
reader wrapper profile using Roslyn syntax ASTs and admitted selected SQL. Five
explicit hashed method selectors: entry, provider, connection, helper, formatter.
Require explicit owner/qualifier values matching SQL selection and explicit
review-scenario/owner-supplied dispatch origin. Output only a conditional profile
candidate, not a compiler-resolved call, final ADO CommandType or runtime binding.
Map one built-in int entry argument to SQL formal ordinal zero only when all
forwarding/formatter shapes match; retain external terminal, virtual/configured
dispatch, symbol, hydration and result gaps. No raw bodies or source strings beyond
procedure/parameter identifiers, hashes and spans. No raw scan/index mutation or
implicit global scanner changes. New local CLI sidecar; full Debug gate required.


### Source-binding implementation status (frozen for final gate)

- Added SqlServerSourceBinding and narrow AST profile helpers. Five explicit
  source method selectors, raw source hashes, full type/signature selectors and
  admitted SQL selection with exact context/template/manifest hash. All route and
  parameter associations are conditional syntax candidates; no terminal fact or
  existing semantic binding is manufactured. SourceParser CSharp14/default symbols.
- Added sqlserver-source-binding compare CLI. Exact source bytes must match the
  declared Git commit blobs; binary hashing preserves BOM. Git HEAD/source/SQL
  rechecked inside atomic publication. Reject overlaps, linked files, dirty source,
  malformed/ambiguous metadata and existing output. Writes binding.local.json,
  source-sql-bindings.sqlite and report.md. The Core report embeds regenerated SQL
  evidence and its digest. CLI index binds its own generator/report hash and
  source_commit_verified flag. No original/index modification or database access.
- Added 29 tests: five source observations, conditional ordinal association,
  argument/composition/dispatch/formatter mutations, aliases/shadows, unavailable/
  ambiguous/nonprimitive entries, strict hashes/context/report admission, BOM-safe
  Git source checks, queryable index, output preservation, source links/dirty
  files and overlap refusal. Focused new/local/selection/workspace/DNN lane PASSED
  109/109 with zero warnings/errors. Earlier 25-case lane passed; final adds four.
- Final Release published. Real DNN trial02 completed at
  `~/tracemap-reviews/dnn-binding-trial-02/collected/`.
  All five profile shapes supported at original lines 1917,72,51,143,18; one
  conditional portalId -> @PortalID ordinal-zero row, nine gaps. Embedded SQL
  evidence digest, five observation/one parameter SQLite counts and commit flag
  verified. Review-scenario values only; compiler/dispatch/source property/terminal/
  preprocessor/runtime/hydration/result boundaries remain unknown.
- No fresh source scan or SQL-index rewrite was needed; reads the five committed
  C# files and regenerates selected SQL metadata. Prior source/SQL indexes remain.
- Full Debug -warnaserror PASSED in unified exec session 63891; log
  `/tmp/tracemap-source-binding-full-suite.log`: 5,968 passed, three skipped,
  zero failed, exit 0, 40.2074 minutes. C# and runtime bytes stayed frozen
  throughout the gate. All 29 added candidate tests passed.
- All four retained SQL CLI smokes PASSED in session 98622; log
  `/tmp/tracemap-source-binding-retained.log`, output
  `/tmp/tracemap-source-binding-retained-01`. Counts/refusals unchanged: parameter
  JSON/DDL 8/12/32 each; result JSON 36/10/36 and DDL 36/10/58 with 20 refusals;
  table 44/10/88 with 24 refusals. Public corpus v2 PASSED all five cases in session
  64260, log `/tmp/tracemap-source-binding-public-corpus.log`, output
  `/tmp/tracemap-source-binding-public-corpus-01`.
- Docs/rule entry added: docs/SQL_SOURCE_BINDING.md, SQL guide/validation link,
  DNN trial follow-up, database.sqlserver.source-procedure-candidate.v1.
- Final full/retained/public results, artifact/unchanged-index hashes and docs
  links verified; implementation milestone complete. No push/PR authorized.

- Retained DNN selection-trial03 raw source, combined and SQL artifact hashes
  still match the original run record; no input/index mutation. Documentation
  relative links and diff checks passed. Full suite completed; final local state
  and task checkboxes updated. Keep the published runtime pinned by trial02.


## Final source-candidate milestone result (2026-11-04)

- 29 new tests, 109-case focused lane and full Debug solution -warnaserror passed:
  5,968 passed / three skipped / zero failed, exit 0, 40.2074 minutes.
- Three skips remain optional Oracle golden sweep, native HTTP receipt interop
  and Windows ASP.NET publication acceptance. No new compiled DNN/runtime proof.
- Real DNN binding-trial02: five syntax shapes, one conditional portalId ->
  @PortalID ordinal-zero association under review-scenario owner/qualifier and
  dispatch assumptions, nine explicit gaps. Both raw/BOM Git source bytes and
  embedded regenerated SQL parent digest verified; SQLite header records source
  commit verification. Existing source/combined/SQL artifacts remain byte-identical.
- Five-case public SQL corpus and four retained CLI smokes passed unchanged
  outcomes/denominators. Final docs links and git diff checks passed.
- Remaining capability boundaries: compiler semantic/overload and partial-file
  resolution, actual configured dispatch and owner properties, external PetaPoco
  transport/ADO parameter binding, event/cache/hydration callers and result/type
  contracts, compiled correspondence and runtime invocation. The new artifact is
  a conditional syntax profile candidate, never a terminal fact or complete path.
- No C# edits after the full gate, no source/SQL execution, no private input,
  no rewritten original/index, no push, PR or public message. Save a local feature
  commit only; owner can inspect docs/SQL_SOURCE_BINDING.md and trial02 report.


## Story and PR packaging (2026-11-04)

- Owner authorized the consolidation story/PR after the local candidate milestone.
- Created public story https://github.com/joefeser/tracemap/issues/937.
- Reconciled V3 config fields, completed owner-trial status, SQL selection/candidate
  links and remaining follow-ups. Added milestone.md as the bounded review scope.
- Read back the final implementation gate log: 5,968 passed, three skipped, zero
  failed on 42bbccbe4; packaging edits are documentation only.
- Verified 132 relative documentation links, workspace CLI help and diff whitespace.
- Fetched origin/dev; it remains the branch ancestor with no target-only commits.
- ACK 0.5.9 stable has the lane-required version/capabilities; PR review state
  will be recorded separately after publication. No merge is authorized.

- Published PR https://github.com/joefeser/tracemap/pull/938 against dev, linked to
  story #937. Publication is complete; review/CI readiness remains pending a live
  ACK gate. No merge performed. PR-loop receipts live under the ignored local
  `.agent-control/pr-loop-handoffs/` path to avoid review-only commit churn.

- Initial PR gate failed the private-path guard because tracked documentation
  retained machine-specific home/checkout paths. Replaced evidence locations
  with home-relative references and executable examples with checkout-relative
  paths; local private-path guard passes. No implementation/runtime change.

## PR #938 acceptance history pin repair

- CI public-static-validation failed before native SQL gates because the public
  acceptance script still required an ancestor from before the history rewrite.
- Matched the original Phase 6 step 6.7 commit by subject and timestamp to its
  sanitized reachable counterpart 34a7418d84a0e050745d33eb16e0592a7699cd17.
  Updated only the acceptance parent; ancestry and exact committed-byte checks
  remain enforced. Historical evidence documents retain their original identities.
- Added a regression requiring the configured parent to be an ancestor of HEAD,
  which catches stale local-only objects even when cat-file can still read them.
- All 28 public acceptance Python guards pass; private-path and whitespace guards
  pass. Replay the actual native compatibility command from this clean commit.

- Native compatibility replay PASSED all four parameter/reader/helper/field
  producer-and-consumer gates from clean commit 45529bf69; no SQL execution.
  Log: `/tmp/tracemap-pr938-native-sha-fix.log`; outputs:
  `/tmp/tracemap-pr938-native-sha-fix-01`.
- The added ancestry regression exposed a shallow checkout in the separate DNN
  offline-guards workflow. Configured full history there, matching the existing
  SQL compatibility workflow, and pinned both lane prerequisites with a test.
- Final Python validation: 29 acceptance guards and 20 DNN harness guards pass;
  private-path and whitespace checks pass. No C# or runtime changes in this repair.

## Scope preflight hardening — 2026-11-04 (validation in progress)

Branch: codex/workspace-scope-preflight, independent of the owner-test docs branch.
Workspace verify/collect now print local operational scope: selected/excluded
repository IDs, explicit selection mode/paths/globs/target, and admitted native
source-folder/page/publication/assembly/PDB/map selection. Root-name hints examine
at most 257 immediate entries, omit the list above 256 and do not recurse or infer
semantic compilation. Warnings do not rewrite scope. Obvious project/solution
extension mismatches refuse before collection; the native contract retains its own
admission rules. Existing schemas, budgets, scanner semantics and receipt rules
are unchanged; no new derived machine artifact is introduced.

Nine new pins failed against the original implementation as expected. Two additional
bounds/nested-scope controls were then added. Initial workspace lane: 53 passed,
0 failed. Final expanded lane, public rehearsal and exact-head full Debug gate
are pending; do not treat this checkpoint as completed validation or merge readiness.
No private inputs used. No PR or merge requested.


### Scope hardening validation completed

Exact code/test head: 9d189373ff6ed0c84520c79e810ad9145006df81.
Final workspace lane: 55 passed, 0 failed/skipped, Debug -warnaserror.
Release launcher preparation succeeded. Public Test-WorkspaceLauncher -WithSql
rehearsal passed full and filtered source/compiled/SQL collection with scope preview.
Isolated remote full Debug -warnaserror: 5,979 passed, 3 skipped, 0 failed;
5,982 total, 14.4429 minutes, successful process exit. No private inputs used.
Final follow-up changes are documentation/prompt only; code/tests match that head.
Private-path and whitespace guards passed. The verification-only owner prompt is
prompts/verify-workspace-scope-before-collection.md. It does not authorize collection.
Windows work-configuration validation remains pending. No PR opened or merge done;
the separate codex/work-scan-setup-guide branch remains unchanged.


### Wizard source-commit prompt clarification — 2026-11-04

Owner testing exposed confusion between application source Git HEAD, the TraceMap
build commit and DLL/configuration SHA-256 values. The ready-step prompt now names
the 40-character application source Git commit, displays its selected source folder,
and prints a single-quoted PowerShell git -C ... rev-parse HEAD command (embedded
single quotes escaped). It explains what entering the value attests and offers
later if correspondence is uncertain. Attestation validation, preparation, scan
execution and all stored contracts are unchanged. Native workflow docs updated.
The added wording pin failed on the previous prompt; all 18 WizardCommand/Execution
cases pass Debug -warnaserror. This is a leaf prompt-text follow-up; the prior
full-suite result above applies to 9d189373f, not a fresh full run of this wording
change. No private scan, application build or PR performed.


### Wizard dependency path list — 2026-11-04

Dependency input now accepts comma or semicolon separators, spaces in unquoted
paths, and double-quoted paths containing separators (doubled internal quotes).
Malformed/empty entries refuse; the existing 128-dependency ceiling is enforced
before filesystem checks. Each normalized relative/absolute path must exist;
a specific DEPENDENCY_DLL_MISSING diagnostic names the failed path. Core's existing
link, duplicate, managed-metadata, byte/hash and state-save checks remain unchanged.
No assembly loading or application execution added. The native workflow docs now
explain list grammar, publication-relative resolution and --continue correction.
Existing stored configuration paths are unchanged; newly typed separator-bearing
paths must be quoted under the new terminal grammar.

Before implementation, comma cases failed while the semicolon control passed.
New terminal cases cover comma/semicolon lists, quoted separator paths, a mixed
valid/missing list with no partial save and successful correction, and malformed
lists. Final entire WebFormsWizard lane: 123 passed, 0 failed/skipped, Debug
-warnaserror. This CLI input-adapter follow-up did not change shared Core; no fresh
full-suite result is claimed for this follow-up. Prior exact-head full gate above
remains historical. No private input, scan, PR or merge performed.


## V3 initialization from existing wizard setup — 2026-11-04

Owner testing identified excessive manual copying and a prepared-config trap:
wizard native.config.json can be unprepared, while the selected wizard attempt's
evidence/review-config.local.json holds the admitted receipts. Added the explicit
workspace init-from-wizard command. It resolves only that saved run, validates the
original wizard references and prepared scope, uses the existing SQL manifest's
context and checks framework selection. A single website can be selected without
retyping an ID; multiple sites require --website-id. No newest-folder discovery,
implicit attestation, preparation, scanner execution or completed-scan import.
Existing config, wizard data and receipts are preserved; creation is atomic and
refuses any existing workspace config. Generated local-only V3 configuration
records exact CLI generator SHA-256 and bounded setup-input SHA-256 in existing
templateProvenance. Config must live outside Git; no private values enter this repo.
Source/compiled/SQL collection still performs fresh collection later if dispatched;
retained scan reuse is not implemented by this setup helper.

Three positive setup cases fail as intended with init-from-wizard dispatch disabled.
Tests cover project/solution/discovery selection, loose SQL, no scan invocation,
prepared-scope drift, missing receipts, SQL hash/context drift, missing framework,
wrong target kind, missing options, source-repo output, ambiguous site and overwrite
refusal. Expanded workspace lane passed 147/147 Debug -warnaserror. Final public
entry-point assertions and exact-head full Debug gate remain pending at this
checkpoint. New getting-started guide: docs/WORKSPACE_FROM_WIZARD.md; existing SQL,
compiled and configuration guides point to it. No PR or merge performed.


### Wizard-to-workspace validation completed — 2026-11-05

Exact code/test head f1eeb732ccd4b7bdf20457ab3b8d3c3b403ae04a. Final 13 setup
cases pass Debug -warnaserror; expanded workspace lane passed 147/147. Remote
isolated full Debug -warnaserror: 6,001 passed, 3 skipped, 0 failed, 6,004 total,
13.9913 minutes, successful process exit. No source/CLI/script changes after that
head; this final checkpoint is documentation only.

Cross-version public rehearsal: an unchanged copied 9d189373f Release tool completed
a synthetic projectless wizard run with classic compiled maps. The new f1eeb732c
Release tool initialized V3 config from that saved wizard state and an authored
loose-SQL manifest; independent workspace verify succeeded. The unchanged old tool
still verified its original completed reports afterward. Setup performed no scan,
source build, SQL execution or attestation. The initial unrelated project-backed
toy-publication attempt lacked classic compiled-map admission and was refused;
its failed output was preserved, not normalized or used as proof. The valid
classic-map fixture replaced that rehearsal input without a code/policy change.

Setup guide PowerShell syntax/local links, whitespace and private-path checks pass.
The separate docs branch correction bc3fbcbb7 points to the prepared receipt config
and this helper instead of initial wizard native.config.json. Windows work inputs
remain owner validation; no private acceptance claim, PR or merge. Existing source
and compiled collection will still be fresh if later dispatched; retained scan
reuse remains separate from this configuration helper.

### Interactive continuation — 2026-11-05

Branch codex/workspace-scope-preflight. User correction: removing manual JSON
copying must not replace the wizard with a long switch list. Existing wizard
--continue now offers workspace setup from a saved native attempt, including
older-tool completed state, without resuming its scan/reports. The adapter asks
only missing framework-source/target and existing SQL-manifest/folder choices,
saves each answer locally under the existing website folder, supports pause and
individual correction, and delegates same-root creation to init-from-wizard.
Existing workspace configuration is offered verification without replacement.
Original wizard scope/receipts and SQL admission remain authoritative. No Core
extractor, scanner or shared-budget policy changes.

New local-only answer envelope pins exact CLI generator SHA-256 and bounded
answer SHA-256, website ID and original native-config hash; integrity is not
authentication. Project preview is root-only and bounded to 256 files; nested
selection needs an explicit path. Saved paths must remain available. Setup does
not import completed scan results or change fresh collection behavior.

Initial focused command/execution/import/UI lane passed 48/48 Debug -warnaserror.
Public rehearsal used unchanged saved state from an older tool: pause after
framework choices, restart the same wizard, answer only remaining SQL choices,
and create verified config in the original root. Both expected exits (2 then 0)
and absence of repeated framework prompts were checked. No scan was dispatched.
Documentation now leads with the interactive flow; noninteractive switches remain
available for automation. Full Debug gate at f1eeb732c belongs to the preceding
backend change, not this UI head. No PR or merge performed.

Final touched-lane command: dotnet test src/dotnet/tests/TraceMap.Tests -c Debug
-warnaserror --nologo --filter 'FullyQualifiedName~WebFormsWizard|FullyQualifiedName~Workspace'.
Result: 278 passed, 0 failed, 0 skipped (2m54s). Whitespace and private-path
guards passed. Work-machine guide branch dae854eca now leads with continuation.
