# Spec State Convention

This directory contains Kiro specs for shipped features, active implementation work, and future backlog.

## Status Files

Every spec folder should contain `implementation-state.md` once work begins or once a spec is intentionally deferred. Agents should read that file before counting task checkboxes.

Recommended status values:

- `implemented`: shipped on `dev`; unchecked boxes should not remain in `tasks.md`.
- `implemented-mvp`: the first useful slice shipped; any unshipped work must be explicitly labeled follow-up/backlog scope.
- `implemented-partial`: a product slice shipped, but the original spec matrix still has open follow-up implementation tasks that are not blockers for the landed slice.
- `spec-ready`: spec authoring/review is complete enough for a future implementation branch; product work has not started.
- `ready-for-implementation`: the requested runway status for a spec whose
  requirements, design, unchecked implementation tasks, and implementation
  state are complete enough to begin product work; equivalent in lifecycle
  position to `spec-ready`, but preserved when a spec request or coordinating
  workflow requires this exact status token.
- `not-started`: ready or proposed, but not implemented.
- `needs-human-review`: status cannot be determined safely from repo evidence alone.
- `superseded`: replaced by another spec or implementation.

## Checkbox Rule

Unchecked task boxes normally mean current ready implementation work. In an
`implemented-mvp` or `implemented-partial` spec, unchecked boxes may remain only
when a nearby state note clearly labels them as follow-up/backlog scope for a
future slice and the shipped status is backed by `implementation-state.md`.
Pure wishlist items should still be plain bullets under `Deferred Follow-Ups`
or `Post-MVP Backlog`.

Do not mix plain deferred bullets under checked implementation parents. If only part of a task shipped, keep the shipped subtasks checked in the implementation section and move the deferred subtasks to a backlog section with enough context to find them later.

When implementing a spec:

- Check off completed tasks as they land.
- Add or update `implementation-state.md`.
- Keep deferred work as plain bullets unless it is the current branch's explicit scope.
- Avoid editing active child worktree spec folders from another branch.

## Currently Active Child Specs

These folders may intentionally contain unchecked implementation tasks until their child worktree PRs land:

- `sql-schema-change-impact`
- `public-combined-path-validation`
- `multi-index-portfolio-report`
