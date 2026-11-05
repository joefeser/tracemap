# Check the configured application scope — verification only

Use branch codex/workspace-scope-preflight in a clean TraceMap checkout. Read
AGENTS.md and docs/WORKSPACE_CONFIGURATION.md, including Scope preview before
collection. Preserve the owner's application sources, publication, configuration,
receipts and old runs. Keep private paths and outputs local.

From PowerShell 7 in TraceMap, prepare the tool once:

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 -Build version
if ($LASTEXITCODE -ne 0) { throw 'Tool preparation failed' }
```

Use the actual existing workspace root selected by the owner (not a guessed path):

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 workspace verify --root $workspaceRoot
if ($LASTEXITCODE -ne 0) { throw 'Verification failed; preserve the exact diagnostic' }
```

If the owner's established workflow uses --scope, pass that SAME filter file.
Do not drop a filter to force verification success. Do not run workspace collect,
any lifecycle scan, the application, SQL, restore or an application build.

Compare the displayed selection against the owner's intended application:
- Are both intended repo IDs selected? Is anything unexpectedly excluded?
- Is the website intentionally projectless, or does it have a project file?
- Does project/solution selection point to the correct file type and actual file?
- Are native source folders and selected/all page scope correct?
- Are primary DLLs, dependency DLLs, PDBs and maps the intended publication inputs?
- Are include/exclude globs, target framework and SQL context intentional?

A root-only preview is not a nested project inventory. Project files can exist
below the root even when none are displayed. Warnings never repair or expand
scope automatically. Check the original configuration locally; do not switch
project types, edit receipts or re-attest correspondence merely to silence warnings.

Return at most 15 sanitized lines: tool SHA; verification status; selection modes;
expected repo count versus selected count; warnings by code; discrepancies yes/no
with categorical descriptions. Do not return private names, paths, hashes or source.
If verification fails, report the first exact diagnostic and STOP. If selection
matches the owner's intent, state that setup review passed; it is not collection
completion or full application coverage. No PR or merge in this check.
