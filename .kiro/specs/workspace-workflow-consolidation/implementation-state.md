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
  `/Users/josephfeser/.local/share/tracemap-workflow-trials/20261103-launcher-replay-01/`.
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
  `/Users/josephfeser/.local/share/tracemap-workflow-trials/20261104-sql-replay-01/`.
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
- Next owner trial: follow `docs/WORKSPACE_SQL_GETTING_STARTED.md`, build the
  launcher once and run `Test-WorkspaceLauncher.ps1 -WithSql` against a fresh
  `~/tracemap-reviews/sql-trial-01` folder. Owner SQL trial is pending.
- Remaining capability work: broader dump/setup compatibility, explicit
  application-call-to-SQL bindings, retained native attachment orchestration and
  scan reuse/retention. The bounded offline collector is implemented, not a full
  database migration engine or an application/runtime acceptance claim.
