# Implementation State

Status: not-started
Last verified: 2026-06-14
Branch: codex/site-examples-and-blog-runway
Source of truth: planning note only

## Summary

This spec captures future blog/editorial ideas for `tracemap.tools`. No blog
pages have been implemented in this slice.

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

- Decide whether to create a `/blog/` index page.
- Decide whether to add a tiny template/layout build step before publishing
  repeated blog pages.
- Add sitemap entries only when pages exist.
