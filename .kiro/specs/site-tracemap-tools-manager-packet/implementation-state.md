# Implementation State

Status: spec reviewed
Branch: codex/site-manager-packet
Public claim level: demo

## Summary

This phase adds a manager-facing public page at `/manager-packet/` that explains
what TraceMap solves for teams using deterministic static evidence. The page is
intended to connect higher-level readers to current public proof paths without
upgrading demo evidence into runtime, production, release-safety, or AI impact
analysis claims.

## Branch

Implementation branch: `codex/site-manager-packet`
Target PR base: `main`

## Scope Decisions

- Keep this site-only under `site/` and `.kiro/specs/site-tracemap-tools-manager-packet/`.
- Use `/manager-packet/` because it is explicit, short, and distinct from the
  existing detailed `/packets/` guide.
- Treat the public claim level as `demo` because the page summarizes checked-in
  public demo proof paths and existing public-safe outputs, not a hidden or
  production-only capability.
- Do not add the page to the top navigation in this phase; use contextual
  cross-links from pages where managers or reviewers are already reading.
- Use existing static layout classes unless the implementation reveals a small
  reusable style need.

## Claim Boundaries

Safe to say:

- TraceMap helps teams have a concrete static-evidence conversation before
  claiming impact or safety.
- Static dependencies, coverage labels, rule IDs, generated summaries, gaps,
  and limitations can orient review questions.
- Public demo pages provide checked-in, public-safe proof paths for the
  manager-facing summary.

Not safe to say:

- TraceMap proves runtime behavior, production traffic, deployment state,
  endpoint performance, production dependency understanding, release safety,
  incident root cause, or AI impact analysis.
- TraceMap replaces Dynatrace, production telemetry, incident response, code
  ownership, tests, human review, or release approval.
- TraceMap proves a P1 root cause, proves an endpoint is bad, or validates a
  release.

## Artifact Safety

Safe to publish:

- Existing public routes and checked-in source paths.
- Public-safe generated summary names and report families.
- Rule/status framing, evidence tiers, coverage labels, gap counts, and
  limitations already visible in current public demo pages.

Do not publish:

- Raw source snippets, raw SQL, config values, secrets, local absolute paths,
  raw repo remotes, raw `facts.ndjson`, `index.sqlite`, combined SQLite files,
  generated scan directories, analyzer logs, or private sample identities.

## Validation Plan

- Passed: Kiro/Sonnet spec review with
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-packet --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000`.
- Patched the Medium+ review findings before implementation:
  - added explicit canonical navigation conformance guidance;
  - pinned the home-page cross-link to the existing `#packets` callout.
- Commit spec files separately.
- Run `git diff --check`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run desktop and mobile browser sanity checks for `/manager-packet/`.

## Oddities

- `main` is checked out in another local worktree, so this branch was created
  from freshly fetched `origin/main`.
- An unrelated untracked `c-sharp-sample-repos/` directory existed before this
  work began and is intentionally left untouched.
- The Kiro wrapper emitted a post-review MCP-settings warning, but the review
  completed with full coverage and actionable findings.

## Follow-Up Items

- Update this state file after implementation with validation results and any
  review-loop findings.
