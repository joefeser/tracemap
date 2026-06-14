# Implementation State

Status: not-started
Last verified: 2026-06-14
Branch: codex/site-demo-guided-path
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

### What TraceMap Solves for Engineering Teams

Purpose: explain the manager-level value in plain language. The article should
describe how TraceMap turns manual dependency indexing and contract-impact
questions into auditable evidence packets that can be reviewed, handed off, and
revisited when scope or coverage changes.

Tone: higher-level, practical, and delivery-focused.

Cover:

- manual indexing work and why it does not scale well
- auditable evidence for review and planning conversations
- coverage-aware decisions when repositories do not build cleanly
- handoff value for reviewers, managers, and external teams

Avoid:

- claiming runtime proof, production usage, or release safety
- implying TraceMap replaces engineering judgment
- using vague ROI claims that are not backed by project evidence

## Follow-ups

- Decide whether to create a `/blog/` index page.
- Decide whether to add a tiny template/layout build step before publishing
  repeated blog pages.
- Add sitemap entries only when pages exist.
