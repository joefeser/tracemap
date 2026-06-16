# Legacy WCF Metadata Normalization Review Prompts

Branch:

```text
codex/legacy-wcf-metadata-normalization-spec
```

Spec files:

- `.kiro/specs/legacy-wcf-metadata-normalization/requirements.md`
- `.kiro/specs/legacy-wcf-metadata-normalization/design.md`
- `.kiro/specs/legacy-wcf-metadata-normalization/tasks.md`
- `.kiro/specs/legacy-wcf-metadata-normalization/implementation-state.md`

## Opus Review Prompt

Review the TraceMap `legacy-wcf-metadata-normalization` spec on branch
`codex/legacy-wcf-metadata-normalization-spec` for merge readiness.

This spec builds on the implemented WCF/service-reference mapper. It should add
checked-in service-reference metadata extraction and conservative operation-name
normalization for old generated WCF clients. It must not add runtime service
calls, remote WSDL downloads, fuzzy matching, LLMs, embeddings, vector DBs, or
prompt-based classification.

Please inspect:

- `.kiro/specs/legacy-wcf-metadata-normalization/requirements.md`
- `.kiro/specs/legacy-wcf-metadata-normalization/design.md`
- `.kiro/specs/legacy-wcf-metadata-normalization/tasks.md`
- `.kiro/specs/legacy-wcf-metadata-normalization/implementation-state.md`
- `.kiro/specs/legacy-wcf-service-reference-mapping/*`
- `src/dotnet/TraceMap.Core/LegacyWcfExtractor.cs`
- `src/dotnet/TraceMap.Core/Models.cs`
- `src/dotnet/tests/TraceMap.Tests/LegacyWcfExtractorTests.cs`
- `scripts/legacy_codebase_validation.py`
- `rules/rule-catalog.yml`
- `docs/VALIDATION.md`
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Do not add LLM calls, embeddings, vector DBs, or prompt-based classification.
- Do not display raw endpoint addresses, raw WSDL URLs, raw schemas, snippets,
  config values, secrets, raw remotes, or local absolute paths.

Review questions:

1. Is this scope narrow enough, or does it mix too much metadata parsing and
   operation normalization?
2. Are `.svcmap`, `.wsdl`, `.disco`, and `.xsd` handling boundaries safe,
   including service-reference folder gating for WSDL/DISCO/XSD?
3. Does the spec avoid runtime reachability, binding compatibility, deployment,
   authorization, and schema/DTO overclaims?
4. Are the proposed fact types and rule IDs sufficient and not duplicative?
5. Is `FooAsync -> Foo` normalization safe with the added corroboration
   requirement and original-name candidate preservation?
6. Is `BeginFoo`/`EndFoo -> Foo` normalization safe enough when requiring a pair
   on the same contract/type?
7. Are lifecycle exclusions complete enough now that they apply to normalized
   base names, including `BeginOpen`/`EndOpen`, `BeginClose`/`EndClose`, and
   `BeginAbort`/`EndAbort`?
8. Do the logical-operation convergence rules prevent `Foo`, `FooAsync`, and
   `BeginFoo`/`EndFoo` from re-triggering false ambiguity while still catching
   genuinely distinct candidates?
9. Are ambiguity rules strong enough to avoid false mappings?
10. Are safety rules sufficient for remote WSDL URLs, SOAP actions, namespace
   locations, local metadata paths, and XXE/entity-expansion parser risks?
11. Are validation expectations realistic with old reduced-coverage public
    samples?
12. Are the tasks reviewable and traceable to requirements, including model
    wiring, scanner-version bump, and legacy summary count updates?
13. Are metadata fact property contracts and gap classification strings precise
    enough for deterministic implementation/tests?
14. Does WSDL corroboration require connection to the generated service reference,
    not just a matching operation name anywhere in the repo?
15. What tests are missing before implementation?

Return:

- Blocking issues with exact file/section references.
- Important non-blocking issues.
- Suggested spec edits.
- Scope cuts or PR slicing changes.
- Missing tests.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review the `legacy-wcf-metadata-normalization` spec on branch
`codex/legacy-wcf-metadata-normalization-spec` as an implementation planner.

Focus on:

- Existing code seams in `LegacyWcfExtractor`.
- Whether new fact types are required or existing WCF facts can carry metadata.
- How to parse `.svcmap` and WSDL conservatively without leaking URLs.
- How to configure XML parsing safely against XXE/entity expansion.
- A safe operation alias model for generated clients and APM operations.
- How to collapse convergent generated operation forms into one logical mapping
  candidate without hiding real ambiguity.
- How to tie WSDL metadata to the generated client through `.svcmap`,
  service-reference folder, or safe portType/contract identity.
- How to avoid duplicate mappings from generated sync/async/Begin/End method
  combinations.
- Minimal first PR boundary.
- Tests most likely to catch false positives.
- Validation commands and likely failure points.

Return a concrete implementation plan, risky assumptions, and recommended first
PR boundary.

## Qodo/Gemini Review Prompt

Review the `legacy-wcf-metadata-normalization` spec for correctness, safety, and
maintainability.

Look for:

- raw URL, local path, schema, SOAP action, config value, snippet, or secret
  leakage risks;
- runtime service reachability or binding compatibility overclaims;
- unsafe operation normalization false positives;
- missing ambiguity gaps;
- new evidence without rule IDs or documented limitations;
- stable key dependence on volatile fact IDs, row order, display names, or local
  paths;
- test gaps around malformed metadata, repeated operation names, lifecycle
  methods, duplicate generated methods, and reduced coverage.

Return actionable findings with exact section references and suggested fixes.
