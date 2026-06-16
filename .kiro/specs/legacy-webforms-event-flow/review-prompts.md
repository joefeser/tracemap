# Review Prompts

## Opus/Sonnet Spec Review Prompt

Review branch `codex/legacy-webforms-event-flow-spec` in `joefeser/tracemap`
for merge readiness. This is a spec-only review, not implementation.

Please inspect:

- `.kiro/specs/legacy-webforms-event-flow/requirements.md`
- `.kiro/specs/legacy-webforms-event-flow/design.md`
- `.kiro/specs/legacy-webforms-event-flow/tasks.md`
- `.kiro/specs/legacy-webforms-event-flow/implementation-state.md`
- `AGENTS.md`

Review questions:

1. Does the spec avoid overclaiming runtime WebForms behavior, event firing,
   service reachability, SQL execution, branch feasibility, or production usage?
2. Are markup event bindings, code-behind handlers, designer fields, WCF/service
   calls, SQL surfaces, and logic signals separated cleanly enough for
   implementation?
3. Are the proposed fact types/rules additive and compatible with existing
   TraceMap fact/report principles?
4. Are evidence tiers, rule IDs, supporting fact IDs, edge IDs, coverage labels,
   and limitations required wherever conclusions are emitted?
5. Are stale designer files, partial classes, duplicate handler names,
   auto-event-wireup, malformed markup, generated controls, event bubbling, and
   dynamic event wiring handled conservatively?
6. Does the spec preserve privacy: no local absolute paths, private repo names,
   raw SQL, source snippets, config values, raw URLs, remotes, or secrets?
7. Are tasks implementable in reviewable slices?
8. Are validation expectations sufficient for old codebases that may not build?
9. Should anything be cut from MVP to keep implementation small?
10. Are there missing tests that would prevent safe implementation?

Return:

- Blocking issues with exact file/section references.
- Important non-blocking issues.
- Suggested fixes.
- Whether this spec is ready to implement after fixes.
