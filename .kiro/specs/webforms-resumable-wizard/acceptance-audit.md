# Web Forms wizard v1 acceptance audit

Audited 2026-10-01 against the nine requirements in `requirements.md`.
This human-readable implementation record is not a scanner finding, machine
receipt, customer acceptance report or merge approval.

## Delivered revision and validation

- [Implementation PR #799](https://github.com/joefeser/tracemap/pull/799) was owner-merged
  into `dev` as `98ce227f6e1031d5bc8a56e4a9c3ab1a3f744508`.
- Tested head `2f6975f573d23262b4acf6db058a109ce653e153` and that merge commit have
  the identical complete Git tree `a2adfc226edf7fb21c6c99bf0413e1738376b972`.
  Tests below ran on the PR head, not a separately rebuilt merge commit.
- [Full .NET CI](https://github.com/joefeser/tracemap/actions/runs/36921768115):
  3,138 passed, one platform skip, zero build warnings. Other adapters and the
  five-adapter combine job passed.
- [Windows public corpus](https://github.com/joefeser/tracemap/actions/runs/36921768266):
  140 passed, zero skips, two authentic ASP.NET publication cases and all three
  PowerShell chain-comparison/SQL-ledger/cap-shortcut checks passed.
- [Distribution checks](https://github.com/joefeser/tracemap/actions/runs/36921768039)
  and [public mutation matrix](https://github.com/joefeser/tracemap/actions/runs/36921768226)
  passed on Windows, Linux and macOS.
- Local `pwsh -NoProfile -File scripts/wlocal.ps1`: 138 passed, one Windows-only
  skip, all three report checks passed. Retained `validation.local.json` in the
  operator's `tracemap-wizard-budget4-final-20261001` output folder includes
  generator/bounded-input hashes and tested-input hashes. Local macOS results
  do not substitute for Windows publication evidence.

## Requirement matrix

Implementation files are under `src/dotnet/TraceMap.Core/` or
`src/dotnet/TraceMap.Cli/`; test classes are under `src/dotnet/tests/TraceMap.Tests/`.
The audit inspected implementation and assertions, not only names/pass totals.

| Requirement | Implementation and acceptance evidence | Result and boundary |
| --- | --- | --- |
| 1. Shared state, chosen root, nested configuration | `WebFormsWizardStore` owns versioned documents, hashed roster, per-file atomic writes and exclusive lease; `WebFormsWizardCommand` is the terminal adapter. Store tests exercise restart, independent project updates, concurrent opens and corruption. | Met. Atomic per file, not a multi-file transaction; mismatches fail closed. |
| 2. Folder, solution, C# and VB targets | `WebFormsWizardTarget` and its tests cover C#/VB tool candidates, explicit solution membership, projectless entries, ambiguity and wrong roots. | Met for documented local forms. No arbitrary MSBuild-import evaluation or URL-site support claim. |
| 3. Toolchain/build consent and manual publication | `WebFormsWizardBuild` hashes the tool/target, compares the displayed plan and advances only on successful probe/build. Build tests include a real SDK build, declined-runner exclusion, failures, changed inputs and selected-project-only commands. Terminal tests assert preview before consent and no invented manual build evidence. | Met. Projectless compilation remains external. Consented build tasks may execute/modify files; no website launch or authenticated build provenance. |
| 4. All/subset paths and resumable pause | `WebFormsWizardForms`/`Selection` normalize separators/in-root absolute paths and generate missing/blank selections. Forms, selection and command tests cover BOM/blank, comments-only rejection, restart, escapes, case ambiguity and no repeated setup questions. | Met. Empty never means all; bounded `.aspx` selection. |
| 5. Validation and explicit isolated repair | Store/selection/build/publication/native services recheck schema, digests, targets, inventories and staged bytes. Store/repair tests cover localized corruption, empty/missing configs, stale/declined previews and preserving other projects/native paths. Execution tests verify old reports after repair. | Met. Repair restarts selected-project setup after confirmation; root corruption needs manual correction/new root. |
| 6. Explicit projects and DLL dependencies | `--add-project` registers one selected website. Full terminal replay completes two fixtures and verifies unchanged first-project bytes. Publication/native tests cover managed metadata inspection, external dependencies, byte-identical deduplication and original-input mutation. | Met. No automatic registration/source acquisition; 128 assembly selections per category, 252 combined. |
| 7. Native static scan/combine/report and protected roots | `WebFormsWizardExecution` delegates start/resume/status and requires verified retained reports; store/native/preflight reject overlap. Real pipeline/terminal tests produce reports, resume without another scan, preserve completed state on output failure and reject invalid attestation. The corpus also tests compiled cross-DLL chains and combination/report behavior. | Met for native fresh static workflow. No website, database method or backend service execution. Attestation is operator evidence, not build authenticity. |
| 8. Reproducible regression/failure matrix and provenance | `wlocal.ps1` runs fresh/restart/subset/invalid/repair/build/publication/multi-project tests alongside compiled/source/DLL/report fixtures. State, repair and native documents carry generator/bounded-input hashes; native identity includes CLI and Core. Budget tests exercise project-backed/projectless ceilings and retained exact/over-boundary counts. | Met within the synthetic matrix. Not exhaustive filesystem-race, private-assembly, runtime or coverage proof. |
| 9. Docs, limitations, backlog and scoped PR | `docs/WEBFORMS_NATIVE_WORKFLOW.md` documents commands/layout, exit semantics, consent, publication, attestation, resume and repair. `rules/rule-catalog.yml` records wizard limitations. `requirements.md` defers discovery, publication automation, PDB/source servers and cross-service tracing. #799 delivered after #798 merged. | Met. This work did not modify or retag #798. |

## Review and residual limitations

Both final-head budget findings were fixed in `2f6975f5` and individually settled
with commit/test evidence. Before owner merge, ACK reported zero unresolved
threads/current actionable findings and green checks, but stopped at
`LOCAL_REVIEW_FALLBACK_ATTEMPTS_EXHAUSTED`; it did not grant merge approval.
The later owner merge is delivery, not retroactive reviewer approval.

An earlier Windows failure at `83b39433` remains unexplained because its original
temporary evidence was deleted. Later runs passed. Closed phase/category/HRESULT
diagnostics and failed public-fixture retention were added; no root-cause fix is
claimed. The real native acceptance case requires three independent fresh runs
and fails on the first failure, rather than retrying to green.

The original v1 implementation objective is satisfied by this bounded static
acceptance evidence. A Windows operator walkthrough on a separately chosen real
website is recommended next, not evidence already obtained or a claim of customer
compatibility. Preserve existing customer reports/config roots. Backend/service
onboarding and evidenced cross-service edges remain separate backlog work.
