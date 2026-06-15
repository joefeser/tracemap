# Implementation State

Status: implemented
Last verified: 2026-06-14
Branch: codex/site-build-process-blog
Source of truth: pending PR

## Summary

This spec captures blog/editorial ideas for `tracemap.tools`. The blog now has a
plain static `/blog/` index and the three initial runway articles:
`/blog/building-tracemap-with-codex-kiro-qodo/`,
`/blog/why-tracemap-exists/`, and
`/blog/what-tracemap-solves-for-engineering-teams/`.

## Published Articles

### Why TraceMap Exists

Status: implemented

URL: `/blog/why-tracemap-exists/`

Purpose: tell the practical origin story without workplace blame. The article
explains that dependency and contract-impact questions often turn into manual
indexing work and frames TraceMap as a deterministic static-evidence packet with
documented limits.

### What TraceMap Solves for Engineering Teams

Status: implemented

URL: `/blog/what-tracemap-solves-for-engineering-teams/`

Purpose: explain the manager-level value in plain language. The article
describes how TraceMap turns manual dependency indexing and contract-impact
questions into auditable evidence packets that can be reviewed, handed off, and
revisited when scope or coverage changes.

### Building TraceMap With Codex, Kiro, and Qodo

Status: implemented

URL: `/blog/building-tracemap-with-codex-kiro-qodo/`

Purpose: describe the collaboration workflow behind the repo and site. The
article shows how Codex worktrees, Kiro specs, implementation-state notes, task
checkboxes, GitHub PR loops, and Qodo review feedback help keep the project
reviewable and resumable while preserving the boundary that TraceMap core
analysis remains deterministic.

## Follow-ups

- Consider a tiny template/layout build step before the blog grows beyond a few
  static pages.
