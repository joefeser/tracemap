# Spec State Convention

This directory contains Kiro specs for shipped features, active implementation work, and future backlog.

## Status Files

Every spec folder should contain `implementation-state.md` once work begins or once a spec is intentionally deferred. Agents should read that file before counting task checkboxes.

Recommended status values:

- `implemented`: shipped on `dev`; unchecked boxes should not remain in `tasks.md`.
- `implemented MVP with post-MVP backlog`: the first useful slice shipped; future work should be plain backlog bullets or a new spec.
- `active`: currently being implemented in a branch or child worktree.
- `not-started`: ready or proposed, but not implemented.
- `superseded`: replaced by another spec or implementation.

## Checkbox Rule

Unchecked task boxes mean current ready implementation work. Do not use unchecked boxes for deferred ideas, post-MVP backlog, or future language/framework wishlist items. Use plain bullets under `Deferred Follow-Ups` or `Post-MVP Backlog` instead.

When implementing a spec:

- Check off completed tasks as they land.
- Add or update `implementation-state.md`.
- Keep deferred work as plain bullets unless it is the current branch's explicit scope.
- Avoid editing active child-thread spec folders from another branch.

## Currently Active Child Specs

These folders may intentionally contain unchecked implementation tasks until their child worktree PRs land:

- `sql-schema-change-impact`
- `public-combined-path-validation`
- `multi-index-portfolio-report`
