# Public Demo Workflow Implementation State

## Current State

Implemented the first public-demo slice in `dev`.

Current follow-up branch: `codex/public-demo-workflow-followups`.

## Spec Status

The first public-demo slice added the public demo script, assertion helper, ignore rule, and documentation. The current follow-up slice promotes combine/report/path/reverse/portfolio demo sections to available while leaving before/after workflows deferred.

## Scope Decisions

- Default demo uses checked-in fixtures and does not clone repositories, fetch external sample data, or call external analysis services.
- First-run package restore for local toolchains may require network; docs must treat this as a prerequisite reality rather than analysis behavior.
- Python scanning is opt-in for the first implementation through `--include-python`.
- Python opt-in mode must use a fresh virtual environment under the demo output root to avoid stale installed code.
- JVM scanning runs only when Java 21 is available or explicitly required.
- If Java 21 is unavailable and not required, the JVM section is `unavailable`.
- Combined dependency report is the default endpoint assertion target.
- Default combined labels are deterministic and public: endpoint stack uses `public-ts-client` and `public-dotnet-server`; mixed stack uses `public-dotnet-modern`, `public-dotnet-server`, `public-ts-modern`, and `public-ts-client`.
- The path demo uses the checked-in endpoint selector `GET /api/admin/runner/get-by-id/{}` and targets `sql-query` evidence.
- The reverse demo selects `sql-query` surfaces and traces back to endpoints.
- Portfolio uses a generated `portfolio-manifest.json` under the demo output root that points at generated combined index paths.
- Diff, impact, and release-review are `deferred` in the first implementation until a concrete checked-in before/after fixture pair exists. This avoids a misleading zero-diff demo and is a deliberate scope decision from review, not an accidental omission.
- Portfolio must use a demo-run generated manifest that points at generated index paths; do not reuse `samples/portfolio.example.json` directly.
- Generated output roots are kept for inspection and printed at the end; the script does not auto-delete them.
- Public `demo-summary.*` artifacts must not contain absolute output roots. Use output-root hashes/labels and relative artifact paths.
- Caller-provided output directories inside the repository root should be refused unless already ignored by git.
- Public-report sentinel scanning covers `demo-summary.*` and generated report Markdown/JSON only. Local-only scan artifacts, SQLite files, facts, and logs are excluded from public-shareable sentinel scope.

## Review Notes

Initial Opus and Sonnet spec reviews both found useful blockers:

- unqualified "network-free" wording conflicted with build-time restore realities,
- Python/JVM default behavior was too ambiguous,
- diff/impact/release-review needed concrete before/after fixtures or explicit deferral,
- generated output privacy could not rely on `check-private-paths.sh` because that script checks tracked files only,
- generated public reports need their own sentinel scan,
- scan manifests/logs should be treated as local-only generated artifacts, not public-shareable reports.
- public summary artifacts cannot contain absolute output roots if they are also part of the sentinel scan,
- output directories inside the repo root need an explicit refusal/ignored-path policy.

Those findings were incorporated into the spec.

Local ignored scripts or developer-specific smoke helpers may exist in a checkout. They are outside the public demo scope and must not be referenced by the public demo workflow.

## Expected First Implementation Boundary

PR 1a scope:

- `scripts/demo-public.sh`
- `scripts/demo-public-assert.mjs`
- output-root handling and in-repo ignored-path guard
- tool checks with Homebrew guidance where useful
- .NET and TypeScript build/scans over checked-in samples
- summary skeleton with `demo-summary.md` and `demo-summary.json`
- generated public-report sentinel scan
- Python marked `not_requested` by default
- Python scan support is available when explicitly requested and uses a fresh output-root virtual environment
- JVM marked `unavailable` when Java 21 is absent
- combine/report, paths/reverse, portfolio, diff, impact, and release-review marked `deferred`

Validation run on this branch:

- `./scripts/demo-public.sh /tmp/tracemap-public-demo-test`
- rejected unignored in-repo output path
- generated sentinel scan caught a planted home-path leak
- `./scripts/demo-public.sh .tracemap-demo`
- `node scripts/demo-public-assert.mjs self-test`
- `dotnet test src/dotnet/TraceMap.sln`
- `npm run check --prefix src/typescript`
- `./scripts/check-private-paths.sh`
- `./scripts/smoke-combined-paths.sh /tmp/tracemap-demo-combined-smoke`
- `git diff --check`

## Shipped Follow-Up Scope

This branch adds:

- generated endpoint-stack and mixed-stack combined SQLite indexes,
- combined dependency reports for both combined indexes,
- semantic dependency-report assertions over labels, SHA-shaped commits, endpoint evidence, rule IDs, evidence tiers, surfaces, and edges,
- targeted `tracemap paths` and `tracemap reverse` runs with deterministic repeated JSON comparisons,
- semantic path/reverse assertions over rule IDs, evidence tiers, labels, supporting IDs, and gaps,
- generated portfolio manifest creation,
- `tracemap portfolio` over the generated manifest,
- portfolio assertions over source coverage, dependency surfaces, commit SHAs, rule IDs, evidence tiers, and supporting IDs,
- docs and task-state updates for the shipped slice.

The endpoint, path, reverse, and portfolio reports are currently `PartialAnalysis` in `demo-summary.*` because the checked-in endpoint samples intentionally include reduced coverage/gaps. This is expected and should not be upgraded to full coverage without stronger scanner evidence.

## Follow-Up Boundary

Later PRs can add:

- concrete before/after fixture pair,
- diff/impact/release-review availability,
- optional separate `tracemap endpoints` output,
- Python sample scan support behind `--include-python`,
- optional OSS mode,
- CI wiring.

## Validation Notes

Latest local validation on this branch:

- `node scripts/demo-public-assert.mjs self-test`
- `bash -n scripts/demo-public.sh`
- `node --check scripts/demo-public-assert.mjs`
- `./scripts/demo-public.sh /tmp/tracemap-public-demo-followups`
- `dotnet build src/dotnet/TraceMap.sln`
- `dotnet test src/dotnet/TraceMap.sln`
- `./scripts/check-private-paths.sh`
- `git diff --check`
