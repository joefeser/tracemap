# Static Dispatch Candidate Bridges Review Prompts

Use these prompts to review the spec before implementation starts.

## Merge-Readiness Review

Review the TraceMap `static-dispatch-candidate-bridges` Kiro spec on branch
`codex/spec-static-dispatch-candidate-bridges` for merge readiness.

This is a spec-only PR. It should only add spec artifacts under
`.kiro/specs/static-dispatch-candidate-bridges/`. It must not implement product
code.

Please inspect:

- `.kiro/specs/static-dispatch-candidate-bridges/requirements.md`
- `.kiro/specs/static-dispatch-candidate-bridges/design.md`
- `.kiro/specs/static-dispatch-candidate-bridges/tasks.md`
- `.kiro/specs/static-dispatch-candidate-bridges/implementation-state.md`

Focus on:

1. Whether the spec avoids runtime proof language for interface, override, and
   DI evidence.
2. Whether the internal candidate states and emitted consumer classifications
   are precise enough or should reuse better existing TraceMap vocabulary.
3. Whether rule IDs and limitations are exact enough, especially
   `combined.dispatch-candidate.v1`, `combined.dispatch-gap.v1`,
   `combined.route-flow.interface-bridge.v1`, reverse, impact, vault, and
   docs-export/RAG consumption.
4. Whether interface implementations, explicit interface implementations,
   overrides, generics/open generics, multiple candidates, high fan-out, and
   reduced semantic coverage are covered.
5. Whether DI registration support is safely scoped as explanatory candidate
   context and not runtime binding proof, including factories, scanning,
   keyed/named services, decorators, service locators, reflection, config, and
   dynamic branches.
6. Whether deterministic ordering, caps, stable IDs, output safety, and
   forbidden wording are specific enough for implementation.
7. Whether route-flow, reverse, impact/include-paths, report/portfolio, vault,
   and docs-export/RAG consumers are conservative and review-tier.
8. Whether tasks are reviewable and future product-code work remains unchecked.

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher issue and say whether the spec is ready to merge after fixes.

## Implementation-Planning Review

Review the same spec as an implementation planner for the current TraceMap
codebase.

Focus on:

1. Whether the shared candidate builder can be extracted from existing paths
   behavior without changing output first.
2. Whether DI registration-context annotations are implementable from current
   `DependencyRegistered` evidence or need a prerequisite fact/schema slice.
3. Whether route-flow, reverse, impact, report, vault, and docs-export
   integration are ordered safely.
4. Whether rule-catalog work happens before emitted product behavior changes.
5. Whether tests are focused enough to catch overclaiming, nondeterminism,
   high fan-out, generic caveats, reduced coverage, and unsafe output.

Return blockers and suggested edits only. Do not recommend runtime container
execution, dynamic proxy expansion, LLM calls, embeddings, vector databases, or
prompt-based classification.
