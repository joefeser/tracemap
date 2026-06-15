# Legacy Flow Composition Reporting Review Prompts

Use these prompts after reading:

- `.kiro/specs/legacy-flow-composition-reporting/requirements.md`
- `.kiro/specs/legacy-flow-composition-reporting/design.md`
- `.kiro/specs/legacy-flow-composition-reporting/tasks.md`
- Related specs:
  - `.kiro/specs/legacy-webforms-event-flow/`
  - `.kiro/specs/legacy-wcf-metadata-normalization/`
  - `.kiro/specs/legacy-data-metadata-extraction/`
  - `.kiro/specs/combined-dependency-paths/`

## Prompt For Opus

Please review this TraceMap Kiro spec for a legacy static flow composition and
reporting layer.

Context:

- TraceMap is deterministic static analysis. No LLMs, embeddings, vector DBs,
  runtime tracing, prompt-based classification, service calls, or database calls
  belong in core scanner/reporter behavior.
- WCF metadata normalization is implemented.
- WebForms event flow extraction is implemented.
- Legacy build environment diagnostics is being implemented separately.
- Legacy data metadata extraction is queued as a ready spec and may not be
  implemented before this feature starts.
- This spec should compose existing evidence into conservative end-to-end views
  for old .NET applications without claiming runtime proof.

Review goals:

1. Identify any wording that overclaims runtime execution, proven impact,
   guaranteed backend reachability, or SQL/database usage.
2. Check whether extending `tracemap paths` with legacy roots is sufficiently
   clear and avoids command/schema divergence.
3. Verify that classifications are conservative enough and cannot turn
   syntax-only, ambiguous, missing-extractor, or reduced-coverage evidence into a
   strong path.
4. Verify that every conclusion has rule IDs, evidence tiers, supporting
   fact/edge IDs, coverage labels, limitations, commit SHA, and extractor
   identity.
5. Identify missing privacy/redaction requirements for public/demo artifacts.
6. Identify schema assumptions that may not hold for single or combined indexes.
7. Identify whether the tasks are implementable in reviewable PR slices.
8. Identify tests needed for older indexes, missing extractors, WCF normalized
   metadata, WebForms roots, SQL/query surfaces, and queued legacy data metadata.

Please return:

- Blocking issues.
- Important non-blocking issues.
- Suggested concrete fixes.
- Missing tests.
- Whether this is ready for implementation after fixes.

## Prompt For Sonnet

Please review this spec as an implementation planner.

Focus on:

1. Likely code locations in the current .NET solution.
2. Whether shared graph/report helpers should be extracted from existing
   combined report/path code before adding legacy flows.
3. Whether the proposed JSON and Markdown output contracts are specific enough
   for tests.
4. Whether selector parsing and bounded traversal semantics are deterministic.
5. Whether older-index compatibility and missing schema behavior are clear.
6. Whether optional legacy data metadata inputs are correctly handled.
7. Whether redaction requirements are testable.
8. Whether the task list can be implemented without touching scanner extractors.
9. Whether validation commands are appropriate for spec-only delivery and later
   implementation.

Return:

- Blockers.
- Important refinements.
- Optional follow-ups.
- Recommended first implementation PR.
- Any risky assumptions with file/section references.

## Prompt For Qodo/Gemini

Review this spec for correctness, privacy, and maintainability risks.

Look for:

- Non-deterministic output risks.
- Static path false positives.
- Missing reduced-coverage caveats.
- Unsafe rendering of raw SQL, URLs, snippets, config values, connection
  strings, remotes, local paths, or private labels.
- Traversal explosion or cycle risks.
- Ambiguous selector behavior.
- Accidental cross-source symbol stitching.
- Divergence from existing `report` or `paths` behavior.
- JSON schema instability.
- Any claim that sounds like runtime proof.

Please provide actionable findings with file/section references and suggested
fixes.
