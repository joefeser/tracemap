# Legacy Remoting Flow Composition Review Prompts

Branch:

```text
codex/spec-legacy-remoting-flow-composition
```

Spec files:

- `.kiro/specs/legacy-remoting-flow-composition/requirements.md`
- `.kiro/specs/legacy-remoting-flow-composition/design.md`
- `.kiro/specs/legacy-remoting-flow-composition/tasks.md`
- `.kiro/specs/legacy-remoting-flow-composition/implementation-state.md`

Related specs:

- `.kiro/specs/legacy-remoting-detection/`
- `.kiro/specs/legacy-flow-composition-reporting/`
- `.kiro/specs/legacy-wcf-metadata-normalization/`
- `.kiro/specs/legacy-webforms-event-flow/`

## Sonnet Review Prompt

Review the `legacy-remoting-flow-composition` spec as an implementation planner.

Context:

- TraceMap is deterministic static analysis.
- Existing Remoting facts/rules are already implemented by
  `legacy-remoting-detection`.
- Legacy flow composition is already implemented through `tracemap paths
  --view legacy-flows` and `--include-legacy-roots`.
- This spec should integrate Remoting facts into that composition layer as
  sibling evidence to WCF.
- It must not add runtime Remoting calls, endpoint probing, process inspection,
  deployment proof, LLM calls, embeddings, vector databases, or prompt-based
  classification.

Review goals:

1. Verify the scope is limited to consuming existing Remoting facts and does not
   drift into scanner extraction or runtime proof.
2. Verify Remoting and WCF stay distinct sibling evidence families.
3. Check whether Remoting terminal/intermediate node kinds are clear enough for
   implementation and tests.
4. Check whether `tracemap paths --view legacy-flows` display and selector
   behavior is specific enough.
5. Verify classification caps are conservative, especially that Remoting cannot
   produce `StrongStaticPath`.
6. Identify missing gaps for runtime channel proof, object lifetime, process
   boundary, deployment, dynamic config, transforms, machine.config,
   reflection, factories, and dependency injection.
7. Check redaction requirements for raw URLs, object URIs, config values, local
   paths, private repo names, raw remotes, snippets, and secrets.
8. Identify likely code locations and minimum first PR boundary.
9. Identify tests most likely to catch false positives or leaks.

Return:

- Blocking issues with file/section references.
- Medium or important non-blocking issues.
- Suggested concrete spec edits.
- Missing tests.
- Recommended first implementation PR boundary.
- Whether this is ready for implementation after fixes.

## Opus Review Prompt

Review this TraceMap Kiro spec for correctness, conservatism, and merge
readiness.

Focus on:

- Whether every conclusion requires evidence and rule IDs.
- Whether Remoting facts are treated as static evidence only.
- Whether the spec avoids proving runtime channel setup, object activation,
  object lifetime, process boundaries, deployment, reachability, production
  usage, exploitability, or impact.
- Whether the relationship to WCF and existing legacy flow composition is clear.
- Whether classifications and gaps are strong enough to prevent false
  confidence.
- Whether redaction rules are sufficient for reviewed public artifacts.
- Whether tasks are reviewable and do not touch unrelated code.

Return blockers, important findings, suggested edits, missing tests, and a
ready/not-ready recommendation.

## Qodo/Gemini Review Prompt

Review the `legacy-remoting-flow-composition` spec for privacy, determinism, and
false-positive risks.

Look for:

- Runtime-overclaiming language.
- Remoting/WCF fact-family bleed.
- Unsafe stitching by URL hash, object URI hash, short type name, config value,
  or source label.
- Missing reduced-coverage or older-index availability gaps.
- Raw URL, object URI, config value, local path, private repo name, raw remote,
  source snippet, connection string, or secret leakage risks.
- Non-deterministic ordering or unstable IDs.
- Tests missing for mixed WCF/Remoting, selector behavior, redaction, and
  classification caps.

Return actionable findings with exact file/section references and suggested
fixes.
