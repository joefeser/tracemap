# Workflow inventory and consolidation story

## Story

As an application owner, I can configure my source repositories once, verify the
configuration, collect explicitly selected evidence into a new run, and find the
combined report without remembering build-output paths or diagnostic scripts.
Compiled-site and database evidence must retain explicit admission boundaries.

## Current entry points

| Use case | Owning entry point | Configuration and outputs | Consolidation status |
| --- | --- | --- | --- |
| Multi-repository source workspace | `workspace init/verify/collect` v1 | `workspace.local.json`, repository filter, fresh raw scans, combined reports | Implemented; source owner trial completed |
| Source plus existing publication workspace | `workspace verify/collect` v2 | Unchanged prepared native fresh config, checkpoint-owned raw web scan, separate framework scan, combined reports and retained native bundle | Implemented; built synthetic replays and full Debug gate passed; owner trial pending |
| Single source review | `local-review run` | Existing CLI options; retained scan plus optional packet/explorer | Reuse implementation |
| Legacy source pipeline | `Invoke-FocusedWebFormsPipeline.ps1` | `config/webforms-review.jsonc`, run receipt, scan, packet, evidence docs, workbench | Preserve pending parity |
| Two source reviews | `Merge-FocusedWebFormsReview.ps1` | Verified web/backend scan receipts; combine then packet/docs/workbench | Establish simpler source entry point; not yet output parity |
| Source and compiled collection | `webforms-review start` fresh/attach | `webforms-compiled-review-config.v1`; checkpoints and selected report attempt | Preserve native admission |
| Retained compiled proof import | `wverify.ps1` -> `webforms-review import-proof` | `configuration/review-config.local.json` plus proof-import receipt | Preserve proof and configuration hashes |
| Historical compiled config | Owner reports `webforms-compile-web-config.v1` | Original local file not inspected; exact schema string not found in current scripts/CLI/docs | Investigate explicit migration; do not guess |
| Page/method inspection | Show/compare/diagnose/requery commands | Existing indexes, packets and run state | Distinguish query from collection; inventory incrementally |
| Legacy agent launchers | Start/continue Claude review wrappers | Still reference previously purged templates | Not a working starting path; author clean replacements from verified workflows, never restore private historical text |
| SQL definition evidence | Separate SQL Server producers | Bounded public synthetic contracts | Not a private dump admission route |

Hardcoded tool paths are confirmed in `wverify.ps1`, `wrefresh.ps1`,
`wstatus.ps1`, `Invoke-FocusedWebFormsPipeline.ps1` and
`Merge-FocusedWebFormsReview.ps1`. The new launcher avoids those assumptions for
the source-workspace entry point; it does not yet repair those legacy callers.
The status helper also locates a separate ReportProbe binary, so replacing its
CLI path alone would not consolidate its tool requirements.

## Initial source-workflow acceptance

- Init generates a strict editable template and never overwrites existing files.
- Verification runs before collection; empty/unknown scope selections fail.
- Paths resolve from configuration, independent of shell working directory.
- Every configured repository has an explicit identity and Git HEAD.
- Collection scans only selected repositories into a fresh destination, combines
  raw indexes, renders a human report, and retains failures without overwriting.
- New machine-readable run records include exact generator and bounded-input
  SHA-256 values. Local paths and fingerprints are local-only.
- No repository builds, restore, fetch, pushes or database execution are implicit.
- Documentation uses the same commands exercised by the synthetic replay.

## Follow-up acceptance: compiled publication

Identify the historical config reader/generator. Cover existing publications,
owner-authorized build/publish, retained-proof reuse, source correspondence and
multi-repository admission with synthetic tests before replacing the wrappers.
Keep native attachment joins distinct from generic multi-index combination.

## Backlog concept: offline database-definition evidence

Investigate how the work agent rendered the existing dump: tool importer, custom
parser or human/agent interpretation. Do not transfer private data into public
fixtures. Specify offline SQL Server definition admission for parameters, result
projections and dependent tables/views/types, identity bindings to application
calls, exact generator/input hashes and explicit unsupported/ambiguous shapes.
Dynamic SQL and runtime-selected branches remain unknown unless separately
supported. An agent-rendered diagram alone is not admitted scanner evidence.
Future agent instructions should name the selected SQL admission status and the
exact retained source/compiled artifacts. They must not silently turn an agent's
DDL interpretation into imported contracts or request another full scan when a
bounded query of retained evidence would answer the question.

## Documentation plan

One start page, one workspace configuration reference, task guides for compiled
publication and SQL, and a maintainer inventory of diagnostic scripts. Mark old
paths as historical only after replacement comparisons pass. Do not remove
useful tests or evidence just because their entry points are numerous.
