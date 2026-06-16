# Legacy Sample Smoke Catalog Review Prompts

Use these prompts after reading:

- `.kiro/specs/legacy-sample-smoke-catalog/requirements.md`
- `.kiro/specs/legacy-sample-smoke-catalog/design.md`
- `.kiro/specs/legacy-sample-smoke-catalog/tasks.md`
- Nearby specs:
  - `.kiro/specs/legacy-codebase-validation/`
  - `.kiro/specs/legacy-baseline-regression-artifacts/`
  - `.kiro/specs/legacy-sample-evidence-pack/`
  - `.kiro/specs/legacy-build-environment-diagnostics/`
  - `.kiro/specs/legacy-data-metadata-extraction/`
  - `.kiro/specs/legacy-remoting-detection/`
  - `.kiro/specs/legacy-webforms-event-flow/`
  - `.kiro/specs/legacy-wcf-service-reference-mapping/`

## Opus Review Prompt

Please review this TraceMap Kiro spec for a legacy sample smoke catalog.

This is a spec review, not an implementation review. Do not edit files.

Focus on:

- Whether the catalog is clearly metadata about sample validation expectations,
  not raw scan output, not a baseline, not an evidence pack, and not a site
  page.
- Whether the proposed tracked storage under
  `docs/validation/legacy-sample-smoke-catalog/` and ignored local storage under
  `.tmp/legacy-sample-smoke-catalog/` are safe and well-scoped.
- Whether safe sample labels, checked-out SHA or fixture identity, source
  classification, evidence family expectations, validation command templates,
  limitations, redaction rules, and claim levels are concrete enough.
- Whether public/private/local source boundaries prevent leaking local paths,
  private repo names, raw remotes, raw SQL/config/secrets/snippets, analyzer
  diagnostics, hostnames, usernames, branch names, or raw artifact contents.
- Whether `hidden`, `demo-safe`, and `public-safe` claim levels are conservative
  and tied to proof.
- Whether relationships to `legacy-codebase-validation`,
  `legacy-baseline-regression-artifacts`, and `legacy-sample-evidence-pack`
  compose without duplicating raw artifacts or proof packets.
- Whether tests and validation are strong enough to catch unsafe catalog
  promotion and overclaiming.

Return:

- Blocking issues.
- Medium+ or important non-blocking issues.
- Suggested concrete fixes.
- Missing tests or validation commands.
- Whether this is ready for implementation after fixes.

## Sonnet Review Prompt

Please review `.kiro/specs/legacy-sample-smoke-catalog/` as an implementation
planner for the current TraceMap repository.

This is a spec review, not an implementation review. Do not edit files.

Check:

- Are the schema, storage locations, command-template model, source
  classifications, commit identity rules, evidence family expectations, and
  claim levels concrete enough for an implementation PR?
- Are WCF, Remoting, WebForms, DBML/EDMX/typed DataSet, build diagnostics, huge
  repo stress, fallback syntax scan, and analysis-gap validation families
  represented without raw artifacts?
- Are redaction rules strict enough for local paths, private names, raw remotes,
  SQL/config/secrets/snippets, analyzer diagnostics, endpoint values, and unsafe
  Markdown?
- Does the spec preserve checked-out SHA proof while avoiding raw private SHA
  and raw remote leakage?
- Does it relate cleanly to `legacy-codebase-validation`,
  `legacy-baseline-regression-artifacts`, and `legacy-sample-evidence-pack`
  without duplicating their outputs?
- Are validation commands, tests, deterministic rendering, generated Markdown,
  and private-path gates specific enough?
- Is any site implementation, scanner behavior change, reducer conclusion, raw
  artifact storage, or AI-based classification accidentally requested?
- Are tasks sliced into reviewable implementation work and left unchecked?

Return blockers first, then important refinements, optional follow-ups,
recommended first implementation PR, and risky assumptions with file/section
references.

## Qodo/Gemini Review Prompt

Review this spec for correctness, privacy, and maintainability risks.

Look for:

- Unsafe tracked catalog fields or promotion gaps.
- Ambiguous claim levels or source classifications.
- Missing checked-out SHA or fixture identity rules.
- Missing evidence families, rule IDs, evidence tiers, coverage labels,
  limitations, extractor IDs, validation commands, or relationship metadata.
- Raw scan output leakage through relationships, hashes, paths, logs, generated
  Markdown, diagnostics, command templates, or artifact references.
- Overclaiming runtime behavior, service reachability, SQL execution, security
  posture, production usage, business impact, release readiness, or reducer
  impact.
- Unstable JSON schema, nondeterministic ordering, or stale generated Markdown.
- Duplication or conflict with legacy validation, baseline, or evidence-pack
  workflows.
- Tests that are too vague to catch leaks.

Please provide actionable findings with file/section references and suggested
fixes.
