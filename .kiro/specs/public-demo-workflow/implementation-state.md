# Public Demo Workflow Implementation State

## Current Branch

`codex/implement-public-demo-workflow`

## Spec Status

Implementation branch for PR 1a. This branch adds the public demo script, assertion helper, ignore rule, and documentation for the first public-demo slice.

## Scope Decisions

- Default demo uses checked-in fixtures and does not clone repositories, fetch external sample data, or call external analysis services.
- First-run package restore for local toolchains may require network; docs must treat this as a prerequisite reality rather than analysis behavior.
- Python scanning is opt-in for the first implementation through `--include-python`.
- Python opt-in mode must use a fresh virtual environment under the demo output root to avoid stale installed code.
- JVM scanning runs only when Java 21 is available or explicitly required.
- If Java 21 is unavailable and not required, the JVM section is `unavailable`.
- Combined dependency report is the default endpoint assertion target.
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
- Python marked `deferred` when explicitly requested in this first slice; it does not create a virtual environment until Python scanning is implemented
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

## Follow-Up Boundary

Later PRs can add:

- combine/report assertions,
- path/reverse assertions,
- portfolio generated manifest,
- concrete before/after fixture pair,
- diff/impact/release-review availability,
- optional OSS mode,
- CI wiring.
