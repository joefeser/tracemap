# site-tracemap-tools-legacy-validation-concept

## Status

Implemented.

## Public claim level

concept

## Summary

Add a public concept page for TraceMap's legacy codebase validation plan. The
page should explain why messy old .NET repositories matter, what the validation
will measure, how local-only inputs and redacted summaries keep evidence safe,
and which claims remain hidden until redacted validation results exist.

## Source Material

- `.kiro/specs/legacy-codebase-validation/requirements.md`
- `.kiro/specs/legacy-codebase-validation/design.md`
- `.kiro/specs/legacy-codebase-validation/implementation-state.md`
- `.kiro/specs/legacy-codebase-validation/tasks.md`

## Requirements

### Requirement 1: Legacy validation concept page

The site shall publish `/legacy-validation/` as a concept page.

Acceptance criteria:

- The page uses `Public claim level: concept`.
- The page states that the underlying validation spec is hidden until a redacted
  validation summary exists.
- The page describes the validation goal without claiming current legacy support
  results.
- The page links to the public spec source.

### Requirement 2: Validation dimensions

The page shall explain the validation dimensions from the legacy spec.

Acceptance criteria:

- The page covers local-only legacy inputs.
- The page covers scan resilience when old projects fail to build or load.
- The page covers WinForms/WebForms event evidence probes.
- The page covers large repository bounds and summary reporting.
- The page covers public-safe redacted summary requirements.

### Requirement 3: Boundaries

The page shall avoid overstating support.

Acceptance criteria:

- The page does not claim TraceMap already supports arbitrary legacy codebases.
- The page does not claim runtime behavior, UI reachability, production traffic,
  deployment state, endpoint performance, or release safety.
- The page does not mention local sample paths, private repository names, raw
  remotes, source snippets, raw SQL, config values, or secrets.
- The page states that results must remain hidden/local until redaction checks
  pass.

### Requirement 4: Discovery

The page shall be discoverable from existing site surfaces.

Acceptance criteria:

- `/use-cases/`, `/demo/result/`, `/capabilities/`, `/validation/`, and
  `/packets/` link to `/legacy-validation/`.
- `/legacy-validation/` is included in sitemap metadata.
- The implementation-state note records validation and follow-up items.

