# Interface, Override, and DI Approximation Review Prompts

Use these prompts to review the spec before implementation starts.

## Merge-Readiness Review

Review the TraceMap `interface-override-di-approximation` Kiro spec on branch
`codex/spec-interface-override-di-approximation` for merge readiness.

This is a spec-only PR for issue 35. It should only add spec artifacts under
`.kiro/specs/interface-override-di-approximation/`. It must not implement
product code.

Please inspect:

- `.kiro/specs/interface-override-di-approximation/requirements.md`
- `.kiro/specs/interface-override-di-approximation/design.md`
- `.kiro/specs/interface-override-di-approximation/tasks.md`
- `.kiro/specs/interface-override-di-approximation/implementation-state.md`

Focus on:

1. Whether the spec preserves TraceMap's deterministic evidence model.
2. Whether interface, override, inheritance, and DI registration evidence is
   clearly distinguished from runtime dispatch or runtime container proof.
3. Whether route-flow, paths, reverse, report, impact/include-paths, and export
   integration points are specified without creating duplicate graph models.
4. Whether classifications and gaps prevent overclaiming under reduced
   coverage, high fan-out, syntax-only evidence, or unknown commit/source
   identity.
5. Whether public artifact safety forbids raw snippets, raw config/SQL values,
   secrets, URLs, hostnames, raw remotes, local absolute paths, and private
   labels.
6. Whether implementation tasks are reviewable and future product-code tasks
   remain unchecked.

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher issue and say whether the spec is ready to merge after fixes.

## Implementation-Planning Review

Review the same spec as an implementation planner for the current TraceMap
codebase.

Focus on:

1. Whether implementation slices are ordered safely.
2. Whether rule IDs and fact/table contracts can remain additive and backward
   compatible.
3. Whether DI registration extraction is scoped small enough for v1.
4. Whether candidate edge derivation has enough evidence inputs, gaps, and
   deterministic ordering.
5. Whether the test plan is sufficient for scanner, combine, graph consumers,
   export, safety, and byte stability.

Return blockers and suggested edits only. Do not recommend runtime container
execution, dynamic proxy expansion, LLM calls, embeddings, vector databases, or
prompt-based classification.
