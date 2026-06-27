# Swift Adapter v0 Symbol Identity And Relationships Review Prompts

Use these prompts after reading:

- `.kiro/specs/swift-adapter-v0-symbol-identity-relationships/requirements.md`
- `.kiro/specs/swift-adapter-v0-symbol-identity-relationships/design.md`
- `.kiro/specs/swift-adapter-v0-symbol-identity-relationships/tasks.md`
- `.kiro/specs/swift-adapter-v0-symbol-identity-relationships/implementation-state.md`
- companion Swift v0 specs in the same implementation series, when present
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `docs/ACCEPTANCE.md`
- GitHub issue #377
- GitHub issue #381

## Prompt For Opus

Please review this TraceMap Kiro spec for issue #381, "Swift adapter v0:
symbol identity and relationships."

Context:

- This is spec-only. It must not implement Swift analyzer/runtime code.
- TraceMap is deterministic static analysis. No LLM calls, embeddings, vector
  databases, runtime tracing, app execution, simulator/device inspection, or
  prompt-based classification belong in scanner/reducer behavior.
- Facts require rule IDs, evidence tiers, repo-relative file paths, line spans,
  commit SHA, extractor versions, and documented limitations.
- Swift v0 is expected to begin with SwiftSyntax-backed evidence and local
  package/project metadata. Compiler/SourceKit enrichment is a future option,
  not a v0 assumption.
- The spec must preserve reduced-coverage honesty and must not overclaim
  Objective-C bridging, generic specialization, conditional compilation,
  protocol witness/runtime dispatch, macros, generated code, or unresolved
  imports.

Review goals:

1. Identify any overclaim that could imply runtime proof, compiler semantic
   proof, Xcode build success, or production impact.
2. Check whether symbol identity inputs are stable enough for cross-file
   declarations, extensions, duplicate names, overloads, and module/package
   boundaries.
3. Check whether gap behavior is explicit enough for ambiguous identity,
   unresolved imports, typealiases, conditional compilation, macros, generated
   code, and external symbols.
4. Check whether relationship kinds are compatible with existing
   `SymbolRelationship` facts, `symbol_relationships` rows, and current
   combined-reader canonical kinds (`InheritsFrom`, `ImplementsInterface`,
   `ExtendsInterface`, `Overrides`).
5. Check whether extension membership and protocol conformance behavior is
   conservative enough for Swift.
6. Check whether override and protocol implementation approximation avoids
   claiming protocol witness selection or runtime dispatch.
7. Check whether public claim level and safe/no-overclaim boundaries are clear.
8. Suggest missing limitations, tests, or rule catalog entries that should be
   added before implementation.

Please return:

- Blockers
- Medium findings
- Minor findings
- Suggested follow-up implementation order

## Prompt For Sonnet

Please review this TraceMap Swift adapter v0 symbol identity and relationships
spec for implementability.

Focus on:

1. Whether the future implementation tasks are sized for a normal feature PR.
2. Whether the declaration prepass and symbol map are specific enough to build
   without rediscovering the model.
3. Whether the proposed rule IDs, gap kinds, properties, and relationship kinds
   are specific enough for tests.
4. Whether SQLite compatibility with `symbols`, `symbol_occurrences`,
   `fact_symbols`, and `symbol_relationships` is clear.
5. Whether the spec keeps SwiftSyntax-only evidence out of `Tier1Semantic`.
6. Whether validation commands are exact for this spec PR and realistic for a
   future implementation PR.
7. Whether any site/product/public copy claim leaked into this spec.
8. Whether tasks accidentally mark work complete or imply this PR implemented
   analyzer behavior.

Assume this spec PR must pass:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-symbol-identity-relationships --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-symbol-identity-relationships --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

Return:

- Blockers
- Medium findings
- Minor findings
- Recommended first implementation cut
