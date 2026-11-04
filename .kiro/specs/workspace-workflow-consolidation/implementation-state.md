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
initial path. Compiled inputs and SQL fields are deliberately not in this schema.

Start page: `docs/WORKSPACE_GETTING_STARTED.md`. Reference:
`docs/WORKSPACE_CONFIGURATION.md`. Synthetic trial:
`scripts/tests/Test-WorkspaceLauncher.ps1`. Optional development launcher:
`scripts/tm.ps1 -Build version` then ordinary commands without `-Build`.

## Next steps

Owner executed the synthetic launcher trial and acknowledged the result. Both
`owner-trial-01` collections had stage `completed` and expected partial coverage
when inspected locally. This is synthetic usability evidence, not real-application
or Windows publication acceptance. The shell copy/paste problem was resolved with
a zsh-compatible one-line command; the guide now separates shell examples and
explains existing completed output.

The next runnable milestone is web + framework + existing compiled publication.
See `compiled-admission-design.md`: native fresh can collect web source and
compiled inputs in one raw scan, followed by a separate framework scan and
combination. Later attach uses separate raw web/framework/compiled indexes plus
the existing explicit parent/child combine contract. No workspace compiled CLI
extension is implemented yet. The historical schema reader is still unlocated.

No push or PR. Recheck the final head before a future merge; the broad validation
above covers the source implementation before this test/documentation follow-up.
