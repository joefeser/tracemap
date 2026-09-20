# Compiled .NET Evidence Foundation Requirements

Tracking: #759, #766, #767

Status: scoped for a narrow first implementation slice

## Goal

Add a supplemental deterministic extractor for managed assemblies that records
compiled metadata evidence without replacing Roslyn semantic analysis or syntax
fallback and without promoting build artifacts beyond their provenance.

## Requirements

### 1. Bounded managed-input inventory

1. The scanner shall inspect only assemblies admitted by an explicit bounded
   input policy.
2. Every admitted input shall retain a repository-relative or explicitly
   external safe locator, file SHA-256, assembly identity, module MVID when
   readable, extractor ID/version, exact generator SHA-256, and bounded-input
   SHA-256.
3. Assembly identity shall include name, version, culture, public-key-token
   state, module name, and target framework when available. None of these alone
   proves source equivalence or authenticity.
4. Native, mixed-mode, unreadable, oversized, or unsupported inputs shall fail
   closed with rule-backed gaps rather than guessed managed facts.

### 2. Provenance and coverage

1. Source repository and commit shall be recorded only when supplied by a
   validated scan/build receipt or another documented deterministic binding.
2. A binary discovered under a source tree without such a binding shall be
   labeled unbound compiled input, not current output of that source commit.
3. Missing expected binaries, proven stale binaries, ambiguous inputs, proven
   source/build mismatches, unresolved dependencies, unavailable symbols,
   reader failures, and limit exhaustion shall produce explicit partial or
   unknown coverage.
4. File timestamps alone shall not prove freshness or staleness.
5. Compiled facts shall remain distinguishable from source semantic and syntax
   facts in rules, extractor identity, coverage labels, and reports.

### 3. Metadata identity

1. The first slice shall inventory assemblies, modules, types, methods,
   constructors, fields, properties, events, and their metadata tokens and full
   signatures.
2. Identity shall preserve declaring assembly/module, nested type structure,
   generic arity and construction state, parameter and return types,
   by-reference and pointer shapes, custom modifiers when readable, and member
   kind.
3. Properties and events shall remain distinct from generated accessors.
4. Compiler-generated members shall be labeled; their relationship to a source
   construct shall remain unknown until a separate rule proves it.
5. Metadata declarations are direct structural evidence, not proof of runtime
   execution, source ownership, dispatch, reachability, or build freshness.

### 4. Deterministic source/compiled reconciliation contract

1. The first slice shall keep source and compiled identities separate and shall
   not emit source-to-metadata identity edges.
2. A later reconciliation slice may join source and compiled identities only
   through documented deterministic identities with explicit ambiguity
   handling.
3. Display strings, short names, same-arity overloads, timestamps, and path
   proximity shall not establish a join.
4. Zero or multiple candidates shall emit an explicit gap; the extractor shall
   not choose a convenient candidate.
5. Reconciliation shall preserve both endpoint identities and the rule,
   evidence tier, supporting IDs, limitations, and provenance that authorize
   the edge.

### 5. Fixture and platform validation

1. The fast suite shall use public, purpose-built C#, VB.NET, and F# fixtures
   covering the first-slice metadata matrix in `design.md`. The deferred matrix
   is not part of tasks 1-7.
2. The suite shall include semantic/projectless or failed-build states where
   applicable, missing/stale/ambiguous/unbound/mismatched binaries, unresolved
   dependencies, reader disagreement, and deterministic repeat runs.
3. Mono.Cecil shall be a supplemental reader, not the sole oracle. Admitted
   identities shall be cross-checked with `System.Reflection.Metadata` in the
   first slice.
4. macOS validation covers portable managed assemblies and deterministic
   artifacts. Windows validation owns .NET Framework, Windows PDB,
   ILAsm/ILDAsm, Web Forms build behavior, and the historical `dotnetperf`
   lane.
5. C++/CLI remains a separate Windows-only feasibility slice and shall not be
   claimed as ordinary Mono.Cecil/native C++ support.

### 6. Safety and product boundary

1. The extractor shall not store source snippets, raw private paths, secrets,
   signing material, or private corpus identities in shareable artifacts.
2. No LLM, embedding, vector database, graph database, or prompt classifier may
   participate in extraction or reconciliation.
3. All correctness contracts and public fixtures belong to the open evidence
   engine. Hosted orchestration, managed private workers, retention, and policy
   workflows are outside this implementation slice.

## Non-goals for the first slice

- broad IL call-graph or control-flow traversal;
- rewrite generation or rewritten-member comparison;
- PDB sequence-point reconciliation;
- runtime loading or execution as evidence;
- the full `dotnetperf` corpus;
- C++/CLI extraction; or
- replacing or weakening existing Roslyn and syntax evidence.
