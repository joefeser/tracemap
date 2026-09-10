# Focused Web Forms review workflow

This folder documents the supported operator path for turning an existing
focused Web Forms packet into bounded, local source-review pages. The executable
entry points remain in `scripts/` for compatibility with existing work-machine
commands.

## Pick the right starting point

| You have | Start with | Result |
| --- | --- | --- |
| Private source, but no TraceMap scan | `Invoke-FocusedWebFormsReview.ps1` | A bounded scan plus sanitized workspace, accuracy, evidence, and performance summaries. |
| An `index.sqlite` and a page list | `Invoke-FocusedWebFormsPageListReport.ps1` | A focused `webforms-modernization.json` and Markdown packet. |
| A locally configured page-list runner | `Run-AndTriage-FocusedWebFormsPageList.ps1` | A focused packet followed by exact-new-report page triage. |
| A completed focused packet | `New-FocusedWebFormsBatchInspection.ps1`, then `New-FocusedWebFormsCodePathReviewSet.ps1` | A private browser index, editable verdict queue, and one private/anonymous report pair per case. |

`Run-FocusedWebFormsPageList.ps1` reads its three workstation settings from the
ignored `scripts/Run-FocusedWebFormsPageList.json`: `indexPath`, `outputRoot`,
and `forms`. A pull cannot overwrite that local file. For automation or a clean
checkout, prefer `Invoke-FocusedWebFormsPageListReport.ps1` and pass every path
explicitly.

## Prerequisites

- PowerShell 7 and the repository's supported .NET SDK.
- An existing TraceMap `index.sqlite` and completed focused Web Forms
  `webforms-modernization.json` packet from the same scan and commit.
- A local checkout of the reviewed application source. Git is optional for the
  source review, and working-tree equality with the recorded commit is not
  inferred.
- A local `scripts/Run-FocusedWebFormsPageList.json`, created from the checked-in
  example as described below.

All batch inspections, source excerpts, paths, symbols, and human comments are
private. Keep the complete output folder on the authorized work machine. Only a
generated `*.shareable.html`, its adjacent `*.shareable.json`, or an explicitly
sanitized console summary is intended for sharing.

## Normal review-set workflow

From the TraceMap repository root:

```powershell
Copy-Item .\scripts\Run-FocusedWebFormsPageList.example.json .\scripts\Run-FocusedWebFormsPageList.json
notepad .\scripts\Run-FocusedWebFormsPageList.json
```

Do that setup once per work-machine checkout. Keep the JSON local: it is ignored
because the paths and form list may identify private source. The example uses
generic forward-slash paths, which PowerShell accepts on Windows; JSON
backslashes must be doubled.

Then run:

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

## Script map

The scripts stay at the `scripts/` root so existing copied commands and the
restricted-workstation workflow do not break. This directory is their
documentation namespace; moving the entry points would require compatibility
wrappers and would not improve the generated artifact layout.

### Supported operator path

| Script | Reads source? | Starts analysis? | Intended use |
| --- | --- | --- | --- |
| `Invoke-FocusedWebFormsReview.ps1` | Yes | Yes, one bounded focused scan | Initial restricted-workstation collection. |
| `Invoke-FocusedWebFormsPageListReport.ps1` | No | Existing-index report traversal only | Parameterized page-list packet generation. |
| `Run-FocusedWebFormsPageList.ps1` | No | Existing-index report traversal only | Runner backed by the ignored local JSON configuration. |
| `Run-AndTriage-FocusedWebFormsPageList.ps1` | No | Existing-index report traversal only | Run the configured packet and triage only its exact new artifact. |
| `New-FocusedWebFormsBatchInspection.ps1` | No | No scan; reads retained index evidence | Build the private case inventory used by review sets. |
| `New-FocusedWebFormsCodePathReviewSet.ps1` | Yes, bounded local reads; raw serialization is opt-in | No scan | Generate the normal multi-case HTML review set and `index.md` verdict queue. |
| `New-FocusedWebFormsCodePathReview.ps1` | Yes, bounded local reads; raw serialization is opt-in | No scan | Generate one case during focused investigation. |

### Read-only summaries and triage

| Script | Purpose |
| --- | --- |
| `Summarize-FocusedWebFormsPageList.ps1` | Compact counts for the latest or explicitly selected focused packet. |
| `Summarize-FocusedWebFormsActionableGaps.ps1` | Privacy-safe actionable unresolved buckets and aliases. |
| `Triage-FocusedWebFormsUnresolvedChains.ps1` | Retained stopping, traversal, and call-evidence classifications. |
| `Triage-CompletedWebFormsPages.ps1` | Per-page terminal and gap state from one completed report. |
| `Summarize-CompletedWebFormsDepths.ps1` | Small bounded comparison of already completed depth 8/10 artifacts. |
| `Compare-CompletedWebFormsPageTriage.ps1` | Provenance-gated comparison of completed depth reports. |
| `Compare-FocusedWebFormsDepth.ps1` | Compatibility reader for an explicit depth 8/10/12 set; it never launches deeper runs. |

### Collection summaries and narrow diagnostics

| Script family | Purpose |
| --- | --- |
| `Export-FocusedWebForms*Summary.ps1` | Create sanitized progress, performance, evidence, workspace, and accuracy readbacks from a focused collection. |
| `Test-FocusedWebFormsRawEvidence.ps1` | Shared bounded diagnostic engine for exact retained witnesses and private inspections. |
| `New-FocusedWebFormsLocalInspection.ps1` | Earlier single-handler private JSON inspection. |
| `New-FocusedWebFormsMethodInspection.ps1` | Private method-hint inspection across retained abstraction layers. |
| `Test-FocusedWebFormsDatabaseEvidence.ps1` | Exact framework `DataAdapter.Fill` caller census for a selected inspection. |

Files named `*.Tests.ps1` and scripts under `scripts/tests/` are repository
regressions, not operator commands.

## Validation for contributors

From the repository root, the focused synthetic checks are:

```powershell
pwsh -NoProfile -File .\scripts\Invoke-FocusedWebFormsReview.Tests.ps1
pwsh -NoProfile -File .\scripts\tests\Test-FocusedWebFormsConfiguration.ps1
pwsh -NoProfile -File .\scripts\tests\Test-FocusedWebFormsCodePathReviewSet.ps1
pwsh -NoProfile -File .\scripts\tests\Test-FocusedWebFormsActionableGaps.ps1
pwsh -NoProfile -File .\scripts\tests\Test-CompletedWebFormsPageTriage.ps1
```

These tests use synthetic data. They do not require or publish a private
application checkout.

Do not infer runtime execution, branch feasibility, successful binding, database
results, or source absence from these static artifacts. Human verdicts are review
metadata and never rewrite scanner evidence.

## Experimental WITS review overlay

Issue #724 begins the next, separate layer. Export a deterministic private draft
from a completed review set's inspection snapshot:

```powershell
.\scripts\webforms-review\Invoke-WitsModernizationReview.ps1 `
  -Mode Export `
  -InspectionPath C:\path\to\review-set\inspection.snapshot.json `
  -ReviewPath C:\path\to\review-set\wits-modernization-review.json
```

After an authorized reviewer or workflow edits only the review metadata, validate
the exact overlay against the same snapshot:

```powershell
.\scripts\webforms-review\Invoke-WitsModernizationReview.ps1 `
  -Mode Validate `
  -InspectionPath C:\path\to\review-set\inspection.snapshot.json `
  -ReviewPath C:\path\to\review-set\wits-modernization-review.json
```

The overlay is private. Validation rejects changed evidence references and does
not import the verdict into TraceMap facts. The contract is
`docs/contracts/wits-modernization-review.v1.schema.json`.
