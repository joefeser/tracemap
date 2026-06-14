# Implementation State

Status: partially-implemented
Last verified: 2026-06-14
Branch: codex/site-manager-blog-phase
Source of truth: pending PR

## Summary

This spec captures blog/editorial ideas for `tracemap.tools`. The first blog
slice adds a plain static `/blog/` index and publishes the manager-facing article
`/blog/what-tracemap-solves-for-engineering-teams/`.

Origin-story and build-process articles remain planned and not implemented.

## Published Articles

### What TraceMap Solves for Engineering Teams

Status: implemented

URL: `/blog/what-tracemap-solves-for-engineering-teams/`

Purpose: explain the manager-level value in plain language. The article
describes how TraceMap turns manual dependency indexing and contract-impact
questions into auditable evidence packets that can be reviewed, handed off, and
revisited when scope or coverage changes.

## Planned Articles

### Why TraceMap Exists

Purpose: tell the practical origin story without workplace blame. The article
should explain that dependency and contract-impact questions often get pushed
onto individual reviewers as manual indexing work. TraceMap exists to turn that
work into deterministic, evidence-backed artifacts: facts, indexes, reports,
coverage labels, source spans, rule IDs, and reducer outputs.

Tone: candid, professional, problem-focused.

Avoid:

- naming or criticizing consultants, employers, coworkers, or teams
- implying TraceMap proves runtime usage or release safety
- framing TraceMap as AI impact analysis

### Building TraceMap With Codex, Kiro, and Qodo

Purpose: describe the collaboration workflow behind the repo and site. The
article should show how Codex worktrees, Kiro specs, implementation-state notes,
task checkboxes, GitHub PR loops, and Qodo review feedback help keep the project
reviewable and resumable.

Tone: appreciative and concrete. Qodo should be described as a useful PR review
agent that caught real issues in the site PRs. Codex and Kiro should be framed
as coordination and implementation tools around a deterministic scanner/reducer,
not as runtime analysis dependencies.

Avoid:

- claiming formal endorsement, partnership, sponsorship, or integration
- implying TraceMap core scanner/reducer uses LLM calls or prompt-based
  classification

## Follow-ups

- Draft `Why TraceMap Exists`.
- Draft `Building TraceMap With Codex, Kiro, and Qodo`.
- Consider a tiny template/layout build step before the blog grows beyond a few
  static pages.
