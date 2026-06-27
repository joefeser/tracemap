# SwiftSyntax Declarations and Basic Call Facts Review Prompts

Use these prompts to review the spec before implementation starts.

## Merge-Readiness Review

```text
Review the TraceMap `swift-adapter-v0-swiftsyntax-declarations-calls` Kiro spec
on branch `codex/spec-swift-swiftsyntax-declarations-calls` for merge
readiness.

This is a spec-only PR for GitHub issue #380, a child of the Swift v0 runway
issue #377. It depends on #378 for scaffold/output contract, may consume #379
for module/package context, and must not conflict with #381, which owns
canonical Swift symbol identity and relationships. It should only add spec
artifacts under:

.kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/

It must not implement Swift analyzer/runtime code.

Please inspect:

- .kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/requirements.md
- .kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/design.md
- .kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/tasks.md
- .kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/implementation-state.md

Focus on:

1. Whether the spec preserves TraceMap's deterministic evidence model.
2. Whether SwiftSyntax declaration and call evidence is correctly capped as
   syntax-backed evidence rather than semantic/runtime proof.
3. Whether files, modules/packages, imports, classes, structs, actors, enums,
   protocols, extensions, functions, methods, initializers, properties, calls,
   object construction, and syntax navigation edges are covered at the right
   v0 depth.
4. Whether file spans, stable symbol IDs/signatures, supporting fact IDs, rule
   IDs/evidence tiers, commit SHA, and extractor versions are required.
5. Whether reduced coverage for macros, generated code, Objective-C bridging,
   conditional compilation, unavailable SwiftSyntax/tooling, and runtime-only
   behavior is explicit.
6. Whether public claim boundaries prevent overclaiming runtime navigation,
   dispatch, SwiftUI/UIKit behavior, branch feasibility, production usage, or
   impact conclusions.
7. Whether rule IDs are clearly proposed only until rule catalog entries exist.
8. Whether #380 consumes or coordinates with #381 rather than defining a
   divergent permanent Swift symbol ID or relationship contract.
9. Whether implementation tasks are reviewable, ordered safely, and remain
   unchecked.
10. Whether validation commands are exact for this spec-only PR and future
   implementation validation is framed without inventing scaffold commands.
11. Whether outputs remain public-safe and avoid raw snippets, raw expressions,
    raw URLs, hostnames, local absolute paths, raw remotes, secrets, signing
    metadata, and private labels.

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher issue and say whether the spec is ready to merge after fixes.
```

## Implementation-Planning Review

```text
Review the same spec as an implementation planner for the current TraceMap
codebase.

Focus on:

1. Whether the implementation order correctly gates Swift rule IDs through
   rules/rule-catalog.yml before product code emits facts.
2. Whether the scaffold/output-contract prerequisite from #378 is a hard gate.
3. Whether fallback behavior is clear when #379 module/package context is
   missing.
4. Whether the #380 declaration/call slice correctly defers canonical symbol
   identity and relationship ownership to #381.
5. Whether declaration extraction handles Swift-specific constructs without
   pretending to resolve compiler semantics.
6. Whether call/object extraction is scoped small enough for v0 and safe around
   overloads, optional chaining, result builders, closures, operators, key
   paths, Objective-C selectors, macros, and dynamic dispatch.
7. Whether syntax navigation edges have enough supporting fact IDs and
   ambiguity rules to avoid false strong edges.
8. Whether SQLite/shared artifact compatibility is specific enough for
   implementation without adding divergent Swift-only schema.
9. Whether tests cover determinism, gaps, public safety, reducer compatibility,
   combine/export compatibility, and report wording.

Return blockers and suggested edits only. Do not recommend runtime execution,
device/simulator inspection, macro expansion execution, LLM calls, embeddings,
vector databases, or prompt-based classification.
```

## Skeptical Bug-Hunt Review

```text
Act as a skeptical reviewer for the SwiftSyntax declarations/calls spec.

Find likely bugs before implementation:

- Syntax evidence accidentally treated as Tier1 semantic evidence.
- Name-only call matching represented as a resolved target.
- Protocol dispatch, overloads, Objective-C selectors, SwiftUI builders, or
  storyboard navigation overclaimed as runtime behavior.
- Missing module/package context causing unstable or misleading symbol IDs.
- Interim file-scoped syntax IDs emitted without the documented
  `swift-syntax:v0:<sha256-lower-64>` format or migration note for #381.
- Conditional compilation or macro-generated code treated as complete coverage.
- `#if canImport(...)` treated as unconditional evidence.
- Generated code or unavailable files silently omitted without gaps.
- `@_exported import` silently treated like a normal import or as runtime proof.
- Chained calls inventing named receiver symbols for call-expression receivers.
- Raw expressions, snippets, literals, URLs, hostnames, local absolute paths,
  raw remotes, signing metadata, or secrets leaking into artifacts.
- Fact IDs depending on timestamps, output paths, temporary paths, UUIDs,
  extractor versions, or unordered properties.
- SQLite rows missing supporting fact IDs.
- Rule IDs emitted before rule catalog entries exist.
- #380 defining a permanent symbol ID or relationship format that conflicts
  with #381.
- Tier3 SwiftSyntax call/member facts emitted as `MethodInvoked` or
  `PropertyAccessed` and consumed as semantic reducer proof.
- Tasks accidentally marked complete in a spec-only PR.

Return issues ordered by severity with suggested spec edits.
```

## Self-Review Checklist

- [ ] Is this clearly spec-only for issue #380?
- [ ] Are all implementation tasks unchecked?
- [ ] Is `implementation-state.md` status `ready-for-implementation`?
- [ ] Does the spec require repo and commit SHA provenance?
- [ ] Does every evidence claim require a rule ID and documented limitation?
- [ ] Are proposed rule IDs prohibited from product emission until cataloged?
- [ ] Are SwiftSyntax facts capped as syntax/textual evidence?
- [ ] Are gaps required for macros, generated code, Objective-C bridging,
  conditional compilation, unavailable SwiftSyntax/tooling, and runtime-only
  behavior?
- [ ] Are raw snippets and unsafe values excluded by default?
- [ ] Are exact spec validation commands present?
- [ ] Are no LLM/AI/vector/embedding impact-analysis claims introduced?
