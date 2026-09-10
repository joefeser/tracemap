# Focused Web Forms review workflow

This folder documents the supported operator path for turning an existing
focused Web Forms packet into bounded, local source-review pages. The executable
entry points remain in `scripts/` for compatibility with existing work-machine
commands.

## Prerequisites

- PowerShell 7 and the repository's supported .NET SDK.
- An existing TraceMap `index.sqlite` and completed focused Web Forms
  `webforms-modernization.json` packet from the same scan and commit.
- A local checkout of the reviewed application source. Git is optional for the
  source review, and working-tree equality with the recorded commit is not
  inferred.
- Literal `IndexPath` and `OutputRoot` settings in
  `scripts/Run-FocusedWebFormsPageList.ps1`.

All batch inspections, source excerpts, paths, symbols, and human comments are
private. Keep the complete output folder on the authorized work machine. Only a
generated `*.shareable.html`, its adjacent `*.shareable.json`, or an explicitly
sanitized console summary is intended for sharing.

## Normal review-set workflow

From the TraceMap repository root:

```powershell
.\scripts\New-FocusedWebFormsBatchInspection.ps1
.\scripts\New-FocusedWebFormsCodePathReviewSet.ps1 -SourceRoot C:\path\to\authorized-source -TriggerContextLines 50 -IncludeRawSource
```

The first command reads existing scan evidence and creates a private batch
inspection. It does not rescan the application. The second command builds one
timestamped review-set folder and opens its `index.html`.

The index groups cases by retained event-binding file path, shows the handler for
each case, and opens reports in new tabs. Newly generated batch inspections assign
contiguous case IDs within each file group. Use `index.md` when a reviewer or an
authorized internal tool needs an editable verdict and comment queue.

Each set contains:

- `index.html` — browser entry point for every case;
- `index.md` — editable private verdict/comment queue;
- `inspection.snapshot.json` — immutable private input used for the set;
- `case-NNN.private.html` — private local review, source-bearing only when `-IncludeRawSource` was explicitly supplied;
- `case-NNN.shareable.html` — anonymous structural review; and
- `case-NNN.shareable.json` — anonymous machine-readable graph.

Deleting the original batch inspection does not invalidate an existing completed
set because the set retains `inspection.snapshot.json`. To create a new set after
deleting all inspections, rerun the first command. Neither operation requires a
new application scan.

## Focused options

Generate only selected cases:

```powershell
.\scripts\New-FocusedWebFormsCodePathReviewSet.ps1 `
  -SourceRoot C:\path\to\authorized-source `
  -CaseId case-001,case-004
```

Use `-TriggerContextLines 0..100` to control lines shown before and after the
retained event-binding span. This is display context only and does not widen the
underlying evidence.

Raw source excerpts are excluded by default. Add `-IncludeRawSource` only for an
authorized private work-machine review; anonymous artifacts never contain source.

For one report instead of a set, use
`scripts/New-FocusedWebFormsCodePathReview.ps1`. It defaults to `case-001` and
accepts the same source-root and trigger-context options.

## Diagnostic utilities

These are investigation tools rather than required steps in the normal review
workflow:

| Script | Purpose |
| --- | --- |
| `Test-FocusedWebFormsRawEvidence.ps1` | Read-only exact semantic call audit over the retained index. |
| `New-FocusedWebFormsLocalInspection.ps1` | Create the earlier single-handler private inspection. |
| `New-FocusedWebFormsMethodInspection.ps1` | Start a private inspection from one unique method hint. |
| `Test-FocusedWebFormsDatabaseEvidence.ps1` | Audit exact framework `DataAdapter.Fill` caller evidence. |
| `Summarize-FocusedWebFormsActionableGaps.ps1` | Print privacy-safe unresolved bucket counts. |
| `Triage-FocusedWebFormsUnresolvedChains.ps1` | Print retained unresolved-chain states and bounds. |
| `Triage-CompletedWebFormsPages.ps1` | Compare retained per-page terminal and gap states. |
| `Compare-CompletedWebFormsPageTriage.ps1` | Compare compatible completed depth packets. |

Do not infer runtime execution, branch feasibility, successful binding, database
results, or source absence from these static artifacts. Human verdicts are review
metadata and never rewrite scanner evidence.
