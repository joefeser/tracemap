# Base44 Injected-Client Dataflow

Extractor identity `base44-evidence/0.4.0` adds bounded, syntax-backed dataflow
for Base44 clients passed into local helper functions.

## Authority

An injected helper parameter is treated as a Base44 alias only when executable
source establishes all of the following:

1. the helper resolves to an implementation-bearing local function declaration,
   an immutable local function expression, or an exported local function
   imported by name/default;
2. the argument is a direct SDK import, a client produced by a supported SDK
   factory/alias chain, or another parameter already proven by this rule; and
3. every directly resolved callsite for the target parameter supplies the same
   SDK-derived chain.

The fixed-point propagation is bounded by the number of discovered parameter
targets and retains at most two distinct candidates per target (one proven
candidate or enough to establish ambiguity). A recursive cycle becomes
authoritative only when at least one resolved external callsite seeds it with
the same SDK-derived chain. Cycles without that source binding, or cycles that
produce conflicting chains, do not become authoritative.

## Fail-Closed Boundaries

The extractor does not infer a client from a parameter name, JSDoc, comments,
types, README content, or conventional names such as `base44`. A missing,
non-SDK, or conflicting direct callsite prevents propagation for that
parameter. Lexical parameters, block declarations, catch bindings, loop
bindings, classes, and functions shadow outer injected bindings. Mutable or
reassigned client/helper aliases are not followed.

Dynamic dispatch, callback escape, namespace calls, unresolved re-exports, and
computed call targets are not followed. Their absence from the emitted
operation facts is not evidence that the source application lacks such a
surface; downstream coverage gates must retain the broader analysis coverage
label and known gaps.

## Evidence

Facts reached through this rule include
`clientBindingKind=callsite-proven-parameter`. Existing facts reached through
direct imports and ordinary derived aliases retain their prior shape and stable
identities. Every emitted `CodeFact` carries the applicable registered `ruleId`
at fact level; its `EvidenceSpan` supplies only the source coordinates for that
rule-backed conclusion. Raw argument values and source snippets are not
persisted.
