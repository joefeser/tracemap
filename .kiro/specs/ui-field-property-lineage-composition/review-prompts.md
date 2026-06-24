# Review Prompts

## Primary Review Prompt

Review the `ui-field-property-lineage-composition` Kiro spec for merge
readiness. This is a spec-only branch. Do not edit files.

Focus on:

- whether Angular template/component and Razor/cshtml form-ish field roots are
  conservative and evidence-backed;
- whether UI-to-backend model/DTO/property composition avoids same-name,
  endpoint-only, or proximity-only false positives;
- whether route-flow/service/data/dependency context can be attached only when
  property-specific static evidence supports it;
- whether every conclusion requires rule IDs, evidence tiers, file paths, line
  spans, supporting IDs, commit SHA, extractor versions, coverage labels, and
  limitations;
- whether safe output requirements cover Markdown, JSON, vault, RAG/docs-export
  chunks, evidence-pack, explorer, and evidence graph consumers;
- whether runtime/browser/computer-use ideas are clearly out of scope for core
  scanner behavior and cannot upgrade classifications;
- whether tasks are reviewable implementation slices with public-safe
  validation.

Report findings first, severity ordered. Call out Medium+ actionable issues
that must be patched before PR.

## Re-Review Prompt

Re-review the same spec after patches. Focus on whether previous Medium+
findings are resolved, whether any new blocker was introduced, and whether
`implementation-state.md` can honestly be marked `ready-for-implementation`.
