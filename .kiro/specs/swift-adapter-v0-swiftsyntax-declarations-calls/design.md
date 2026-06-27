# SwiftSyntax Declarations and Basic Call Facts Design

## Overview

This spec defines the Swift v0 declaration and basic call evidence slice for
TraceMap. The intended implementation uses SwiftSyntax as a deterministic
parser, emits TraceMap-compatible facts and SQLite rows, and labels everything
that requires compiler semantics or runtime behavior as reduced coverage.

The evidence flow is:

```text
selected .swift files
  -> SwiftSyntax parse tree
  -> file/import/declaration facts
  -> #381-compatible syntax-backed symbol IDs and occurrences
  -> basic call/object facts
  -> optional syntax call/navigation/relationship rows
  -> gaps for unsupported SwiftSyntax, macros, generated code, ObjC bridging,
     conditional compilation, runtime dispatch, and missing module context
```

The slice deliberately starts with syntax evidence. It is useful for browsing
and reducer/report context, but it must not claim resolved Swift symbols,
selected overloads, runtime dispatch targets, actual UI navigation, or runtime
impact.

## Goals

- Extract Swift file, import, declaration, call, and construction evidence from
  SwiftSyntax parse trees.
- Emit declaration evidence that can feed #381-compatible Swift symbol IDs and
  display signatures for files, modules/packages where known, nominal types,
  protocols, extensions, functions, methods, initializers, and properties.
- Populate shared TraceMap fact, symbol, occurrence, call edge, object
  creation, relationship, and fact-symbol shapes where the evidence matches
  existing contracts.
- Carry file spans, repo-relative paths, commit SHA, extractor IDs, extractor
  versions, rule IDs, evidence tiers, and supporting fact IDs.
- Preserve deterministic ordering and byte-stable outputs.
- Emit explicit gaps for reduced coverage and unsupported Swift features.
- Keep outputs public-safe by storing hashes, lengths, kinds, labels, and line
  spans instead of raw snippets or raw expressions.

## Non-Goals

- No Swift semantic compiler integration in this slice.
- No SourceKit/sourcekit-lsp, SwiftPM semantic load, or Xcode build execution.
- No macro expansion execution or compiler plugin execution.
- No simulator/device inspection or app runtime execution.
- No runtime UI navigation proof, protocol witness resolution, override target
  proof, overload resolution, Objective-C selector binding, dependency
  injection resolution, branch feasibility, callback scheduling, or production
  usage proof.
- No package/dependency, HTTP, storage, serializer, config, SwiftUI, UIKit, or
  storyboard surface depth beyond declaration and basic call/object evidence.
- No LLM calls, embeddings, vector databases, prompt-based classification, or
  AI impact-analysis claims.
- No raw source snippets by default.

## Proposed Rule IDs

These names are planning anchors only. Implementation must add rule catalog
entries before emitting any of them.

| Proposed rule ID | Purpose | Expected tier |
| --- | --- | --- |
| `swift.syntax.file-inventory.v1` | Swift file inventory and parse eligibility. | `Tier3SyntaxOrTextual` or `Tier4Unknown` for gaps |
| `swift.syntax.import.v1` | Syntax-visible import declarations. | `Tier3SyntaxOrTextual` |
| `swift.syntax.declaration.v1` | Syntax-visible declarations and #381-compatible source symbol IDs. | `Tier3SyntaxOrTextual` |
| `swift.syntax.symbol-relationship.v1` | Syntax-visible containment, extension target, inheritance, or conformance relationship inputs owned by #381. | `Tier3SyntaxOrTextual` |
| `swift.syntax.call.v1` | Syntax-visible call and member-call expressions. | `Tier3SyntaxOrTextual` |
| `swift.syntax.object-creation.v1` | Syntax-visible initializer/type construction candidates, default fact type `SwiftConstructionCandidate`. | `Tier3SyntaxOrTextual` |
| `swift.syntax.navigation-edge.v1` | Derived syntax call/navigation edges with supporting facts. | `Tier3SyntaxOrTextual` |
| `swift.syntax.analysis-gap.v1` | Swift parser, coverage, toolchain, macro, generated-code, conditional compilation, bridge, or runtime boundary gaps. | `Tier4Unknown` |

If existing shared rule IDs can represent a fact precisely with documented
limitations, the implementation may reuse them instead of adding a Swift rule.
No product code should emit a proposed ID until `rules/rule-catalog.yml`
documents the rule using the existing catalog shape: `id`, `name`,
`description`, `evidenceTier`, `emits`, and `limitations`. Property
expectations, false positives, and false negatives should be captured in
description or limitations prose unless a separate schema-change spec extends
the catalog validator.

## Evidence Tiers

SwiftSyntax facts are syntax-backed. Default tier is `Tier3SyntaxOrTextual`.

`Tier2Structural` may apply only when deterministic package/project metadata
from the inventory slice proves structure such as package target membership.
For this slice, the minimum qualifying `Tier2Structural` evidence is a #379
inventory fact for the file with a deterministic package/target identity,
non-ambiguous module name, and no `ModuleContextUnavailable` or
`ModuleContextAmbiguous` gap for that file. Otherwise SwiftSyntax declaration
and call facts remain `Tier3SyntaxOrTextual`. This spec does not define a
`Tier1Semantic` Swift path. Future semantic enrichment must use
compiler/sourcekit evidence and a separate spec.

`Tier4Unknown` applies to explicit gaps and unable-to-prove states.

## Source Identity Model

Issue #381 owns the canonical Swift symbol identity and relationship contract.
This slice defines the declaration and call evidence that feeds that contract.
If implementation of #380 lands before #381, #380 must use an explicitly
documented interim file-scoped syntax ID scheme and leave a migration note for
#381; it must not create a second permanent Swift symbol identity format.
If #381 has already landed on the implementation base, this interim format is
forbidden and the implementation must emit only #381-compatible IDs.

### File Identity

File identity should use:

- repo-relative path;
- normalized path casing rules chosen by the adapter and documented in tests;
- file kind `swift-source`;
- file size or content hash metadata;
- commit SHA from the manifest.

Local absolute paths must not enter fact IDs, reports, or safe metadata.

### Module and Package Identity

The declaration/call slice should consume package or project inventory when it
is available from the Swift inventory/project slice. Safe context may include:

- SwiftPM package identity;
- target or module name;
- Xcode target identity when parsed structurally;
- source root identity;
- package manager family.

When context is absent or ambiguous, declarations remain file-scoped and a
module/package gap is emitted. The adapter must not guess target membership
from folder names unless the inventory rule documents that inference and its
limitations.

### Symbol ID Inputs

The #381-owned Swift symbol IDs are expected to be derived from safe,
deterministic inputs such as:

- language `swift`;
- module/package context when known;
- repo-relative file path;
- lexical containment path;
- declaration kind;
- declaration name when visible;
- generic arity when visible;
- function/init parameter labels and arity;
- property/subscript shape when visible;
- line span as evidence metadata, not default ID input;
- normalized syntax hash only where needed to distinguish ambiguous sites.

The exact canonical formula belongs to #381. This spec may require those inputs
to be available on declaration/call facts, but it should not fork the canonical
formula. Extractor version should be recorded on facts but excluded from fact ID
inputs, following existing adapter guidance.

Interim syntax IDs, used only if #380 lands before #381, should use this
documented format:

```text
swift-syntax:v0:<sha256-lower-64>
```

The hash input is UTF-8 text joined with `\n` from:

```text
language=swift
module=<module-name-or-unknown>
path=<repo-relative-path-normalized>
kind=<declaration-kind>
containment=<lexical-containment-display-or-empty>
name=<safe-name-or-empty>
genericArity=<integer-or-0>
parameterLabels=<comma-joined-safe-labels-or-empty>
syntaxHash=<sha256-lower-64-of-normalized-node-text-with-literals-masked>
```

The normalized node text used for `syntaxHash` masks string and numeric
literals, strips comments, normalizes trivia whitespace to single spaces, and
does not store the text itself. Line span is preserved on facts as evidence
metadata but is not part of the default interim symbol-ID hash input because
line shifts would churn IDs. If two declarations still collide, append an
`ordinal=<n>` line assigned by deterministic source order and emit an
identity-collision gap. Anonymous or ambiguous declarations may use the ordinal
and syntax hash as their stable file-scoped discriminator. #381 may replace
this interim format, but it must be able to map or intentionally deprecate it.
If #381 has landed first, skip this interim format entirely.

Display signatures should be reviewer-friendly, such as:

```text
ModuleName.OrderService.submit(orderId:)
<unknown-module>::Sources/App/OrderService.swift#OrderService.submit(orderId:)
```

Display signatures are not semantic proof. They are safe navigation labels.

## Declaration Extraction

The extractor should walk recoverable SwiftSyntax declarations and emit facts
for:

- source files;
- imports;
- classes;
- structs;
- actors;
- enums;
- protocols;
- extensions;
- functions and methods;
- initializers;
- properties, including stored/computed shape where visible;
- subscripts where deterministic;
- enum cases;
- typealiases and associated types where deterministic.

Declaration facts should include:

- `declarationKind`;
- `name` when safe and visible;
- `displaySignature`;
- `symbolId`;
- `containingSymbolId` when available;
- `moduleName` or module context status;
- `genericArity`;
- `parameterLabels`;
- `arity`;
- `async` and `throws` flags when visible;
- `propertyShape` for closed-set property metadata;
- `syntaxIdentityStatus` such as `named`, `file-scoped`, `ambiguous`,
  `conditional`, or `generated-gap`;
- file path and line span.

The adapter should avoid storing raw declaration text, comments, attributes
arguments, literal values, or expression text.

## Import Extraction

Import facts should record:

- imported module path segments;
- import kind such as module, struct, class, enum, protocol, func, typealias,
  or unknown;
- `exportedImport = true` for `@_exported import`, without claiming runtime
  re-export behavior for consumers;
- conditional compilation status if the import is inside a conditional region;
- file span;
- rule ID, tier, extractor ID, and extractor version.

Import facts do not prove the imported module exists, is linked, is used at
runtime, or exposes a symbol.

## Call and Object Evidence

Supported v0 syntax patterns include:

- simple calls such as `submit(orderId:)`;
- member calls such as `client.send(request)`;
- static-looking member calls such as `OrderService.build(...)`;
- `self` and `super` member calls;
- initializer-like construction such as `Order(...)`;
- explicit `.init(...)` where the receiver/type syntax is visible;
- chained calls as separate syntax sites when spans are deterministic.

For each call site, emit:

- caller/containing symbol ID;
- `callKind`;
- callee display path or unresolved placeholder;
- callee syntax hash;
- argument labels;
- argument count;
- argument expression kinds and hashes when needed;
- optional chaining, try/await, force unwrap, trailing closure, closure
  argument, or result-builder context flags where visible;
- file span;
- supporting declaration fact ID when available.

Object creation evidence defaults to a Swift-specific construction candidate
fact such as `SwiftConstructionCandidate` under `swift.syntax.object-creation`
because shared `ObjectCreated` can be reducer-sensitive in other adapters. A
future implementation may also emit shared `ObjectCreated` only after adding
cataloged syntax-tier limitations and reducer-safety tests proving it is not
upgraded to runtime allocation or semantic invocation proof.

For chained calls, each direct SwiftSyntax function-call or member-access call
node is treated as a separate candidate when it has a deterministic span. If
the receiver of a member call is itself a call expression, the callee identity
records the member name and the receiver shape `chained-expression`; it does
not synthesize a named receiver symbol.

## Navigation and Relationship Edges

The implementation may derive syntax edges only when supporting facts exist.
Every derived edge should name its supporting fact IDs.

Allowed v0 edges:

- containing declaration -> syntax callee placeholder;
- containing declaration -> locally matched declaration if deterministic and
  syntax-only;
- declaration -> contained member;
- declaration -> extension target syntax placeholder;
- type declaration -> inherited/conformed type syntax placeholder.

Canonical symbol relationship rows and stable relationship-kind semantics are
owned by issue #381. This slice may emit only the syntax evidence and supporting
fact IDs that #381 can consume or normalize.

Forbidden v0 conclusions:

- selected overload;
- protocol witness;
- actual dynamic dispatch target;
- runtime navigation;
- Objective-C selector target;
- SwiftUI destination reachability;
- storyboard segue wiring;
- dependency-injection target;
- macro-generated member;
- branch-feasible call path.

If a local syntax match is implemented, it should be bounded:

- same file first;
- same known module only when module context is reliable;
- exact safe signature/name/arity match only;
- deterministic tie handling;
- ambiguous matches become gaps or unresolved targets.

## Gaps and Coverage

Gap facts should be bounded and deterministic. Suggested `gapKind` values:
these are values under `swift.syntax.analysis-gap.v1`, not separate rule IDs.

- `SwiftSyntaxUnavailable`;
- `SwiftSyntaxVersionUnsupported`;
- `SwiftParseDiagnostics`;
- `FileSkipped`;
- `GeneratedSourceExcluded`;
- `MacroExpansionUnavailable`;
- `ConditionalCompilationAmbiguous`;
- `CanImportConditionalAmbiguous`;
- `ObjectiveCBridgeUnresolved`;
- `DynamicDispatchUnresolved`;
- `OverloadResolutionUnavailable`;
- `ProtocolWitnessUnavailable`;
- `ModuleContextUnavailable`;
- `ModuleContextAmbiguous`;
- `SourceLocationUnavailable`;
- `UnsupportedCallShape`;
- `UnsupportedDeclarationShape`.

Gap properties should be safe:

- relative file path;
- line span when available;
- construct kind;
- parser diagnostic category or hash;
- module context status;
- bounded counts.

Do not store raw diagnostic text if it can contain paths or snippets; store a
category and hash when needed.

SwiftSyntax parser diagnostic messages SHALL be classified by kind/category,
such as `parse-error` or `warning`, and message text SHALL be hashed rather
than stored verbatim in gap facts. Use SHA-256 over UTF-8 diagnostic message
bytes and store the full 64-character lower-case hex digest, matching the
syntax hash convention. Reports may render a short hash prefix for readability,
but persisted properties and stable-key inputs use the full digest.

## Artifact Contract

The implementation should write the normal TraceMap scan outputs:

```text
scan-manifest.json
facts.ndjson
index.sqlite
report.md
logs/analyzer.log
```

This spec does not implement the scaffold. The scaffold/output-contract slice
from issue #378 is a hard prerequisite for product-code implementation of this
slice because #380 depends on the adapter being able to write the required
TraceMap outputs.

SQLite should use shared tables:

- `scan_manifest`;
- `facts`;
- `symbols`;
- `symbol_occurrences`;
- `fact_symbols`;
- `symbol_relationships`;
- `call_edges`;
- `object_creations`.

Any new table or column must be additive and update shared schema tests.

## Determinism

Sort by:

1. relative file path;
2. start line;
3. start column if available;
4. declaration/call kind;
5. computed fact ID string.

Use stable JSON property ordering and deterministic SQLite inserts. Do not emit
timestamps into fact IDs. The manifest timestamp may be present as scan
metadata, but `scanId` and fact IDs must not depend on timestamps, process IDs,
temporary paths, UUIDs, or output paths.

Swift scan IDs should be derived from stable repo identity, commit SHA, selected
Swift file inventory hash over sorted repo-relative paths and content hashes,
and normalized scan options. The exact scaffold-level input list must be
documented before implementation emits facts because fact IDs may include
`scanId`. Product-code implementation for this slice must not begin Phase 3
fact emission until that exact formula is documented either by #378 or this
implementation's state note.

## Safety

Default outputs must not include:

- raw source snippets;
- raw expression text;
- comments;
- raw string literals;
- raw URLs or hostnames;
- connection strings or credentials;
- local absolute paths;
- raw remotes;
- signing/provisioning metadata;
- private labels;
- simulator/device identifiers.

Use safe names, closed-set kinds, hashes, lengths, and line spans.

## Implementation Order

1. Verify the Swift scaffold/output contract from issue #378 has landed on the
   implementation base before starting product-code work for this slice.
2. Add rule catalog entries and tests that fail on uncataloged Swift rule IDs.
3. Add tiny Swift fixtures covering declarations, imports, calls, object
   creation, and reduced coverage.
4. Implement file parsing and line-span mapping with SwiftSyntax.
5. Implement declaration extraction and #381-compatible symbol IDs, or an
   explicitly documented interim file-scoped syntax ID scheme if #381 has not
   landed.
6. Implement call/object evidence.
7. Implement optional syntax edges and relationships only after supporting fact
   IDs exist.
8. Implement report summaries and gap rendering.
9. Validate shared SQLite/combine/export behavior where touched.

## Validation Commands

Spec-only validation:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-swiftsyntax-declarations-calls --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-swiftsyntax-declarations-calls --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

Future implementation validation should include, after the Swift scaffold
chooses exact commands:

```bash
<swift-adapter-build-command>
<swift-adapter-test-command>
<swift-adapter-scan-command> --repo <swift-fixture> --out <tmp>/swift-fixture
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

If shared schema/report/combine/path/export behavior changes, add the relevant
`docs/VALIDATION.md` commands before PR merge.
