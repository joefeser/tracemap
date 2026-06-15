# Legacy Data Metadata Extraction Implementation State

Status: ready-for-implementation
Branch: codex/legacy-data-metadata-extraction-spec
Public claim level: hidden

## Why This Spec Exists

WCF metadata normalization and WebForms event flow are now implemented on `dev`.
The next missing static layer for older .NET codebases is design-time data
metadata: LINQ to SQL DBML, Entity Framework EDMX, typed DataSet/XSD,
TableAdapter metadata, config provider/connection metadata, and generated data
code linkage.

Old repositories frequently fail local project load because dependencies,
toolsets, SDKs, or Visual Studio design-time generators are unavailable. The
scanner should still preserve useful deterministic evidence from checked-in
metadata and clearly label reduced coverage when it cannot prove a link.

## Scope Decisions

- This branch is spec-only. It does not implement scanner code.
- Static checked-in metadata only; no runtime database connections, SQL
  execution, service calls, EF model loading, or config transform execution.
- DBML and EDMX are included anywhere in the repository because their extensions
  are specific data metadata formats.
- Typed DataSet `.xsd` extraction is gated by deterministic typed DataSet or
  TableAdapter indicators so unrelated schemas do not become data facts.
- TableAdapter command text uses existing SQL hash/shape conventions only when
  complete static text is visible; raw SQL is never stored.
- Config provider and connection metadata can explain names and provider
  declarations but must not reveal raw connection strings or imply runtime
  environment selection.
- Generated-code linkage can be semantic, structural, syntax/textual, or unknown;
  ambiguity produces gaps.
- New facts should support existing reducer/report surfaces without changing
  current reducer semantics.
- Public claim level stays hidden until redacted validation artifacts are
  intentionally reviewed.

## Review State

Initial spec drafted for Kiro Opus and Sonnet review. This should not be marked
ready-for-implementation until Medium+ and blocking review findings are resolved.

Review outcomes:

- Sonnet spec review completed with full coverage. Blocking findings patched:
  fact type selection rules, validation test ambiguity, parser safety test
  detail, typed DataSet `.xsd` gating, safe identifier examples, and additional
  missing tests.
- Opus spec review timed out after the 10 minute wrapper limit and produced
  reduced coverage. Partial findings patched: exact gap classifications,
  rule-to-fact/tier mapping, config extractor relationship, tier ceilings,
  determinism, extractor version naming, committed scope decisions, and PR
  slicing guidance.
- Sonnet re-review completed with reduced coverage because Kiro reported denied
  shell access after reading files. Remaining blockers patched: copy-ready rule
  catalog entries and XSD-intrinsic typed DataSet gating.
- Final Sonnet re-review completed with reduced coverage because Kiro reported
  denied shell access after reading files. No blocking or important issues
  remain. Spec is ready for implementation.
- The six `legacy.data.*` rule catalog entries remain an implementation task;
  this spec-only import does not change `rules/rule-catalog.yml`.
- PR review loop addressed Gemini's actionable note about config fact ownership:
  `legacy.data.config.v1` no longer lists `ConfigKeyDeclared` as an emitted fact;
  generic config-key evidence remains under existing config rules.
- PR review loop addressed Qodo's actionable note about typed DataSet `.xsd`
  gating: requirements now require XSD-intrinsic indicators first and treat
  `.designer.cs` or generated-code linkage as corroborating evidence only.

## Suggested PR Boundaries

- PR 1: Tasks 1-4, covering rule catalog, fact model, extractor version,
  inventory, parser safety, and safe identifier policy.
- PR 2: Task 5, covering DBML extraction.
- PR 3: Task 8, covering config provider and connection metadata.
- PR 4: Task 6, covering EDMX extraction.
- PR 5: Tasks 7 and 9, covering typed DataSet/TableAdapter extraction and
  generated-code linkage.
- PR 6: Tasks 10 and 11, covering docs, validation, compatibility, and final
  implementation validation.

## Validation Commands For Spec Delivery

```bash
node scripts/kiro-review.mjs --phase legacy-data-metadata-extraction --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000
node scripts/kiro-review.mjs --phase legacy-data-metadata-extraction --kind spec --model claude-sonnet-4.5 --fresh --timeout-ms 600000
node scripts/kiro-review.mjs --phase legacy-data-metadata-extraction --kind re-review --model claude-sonnet-4.5 --fresh --timeout-ms 600000
./scripts/check-private-paths.sh
git diff --check
```

No .NET implementation validation is required for this spec-only branch unless
review patches touch source code, docs outside the spec, or validation scripts.

## Implementation Validation

Not started. `tasks.md` is intentionally unchecked and implementation-ready.

## Follow-Ups To Keep Out Of This Slice

- Scanner implementation.
- Site copy or public site claims.
- Runtime data access proof.
- EF runtime model loading or query evaluation.
- Arbitrary ORM DSL support without a deterministic parser spec.
- Committed local sample names, private paths, raw SQL, connection strings,
  config values, remotes, snippets, or generated smoke artifacts.
