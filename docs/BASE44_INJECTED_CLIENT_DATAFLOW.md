# Base44 Injected-Client Dataflow

Extractor identity `base44-evidence/0.4.1` adds bounded, syntax-backed dataflow
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
candidate or enough to establish ambiguity). Candidate identity preserves each
property segment; a dotted literal key cannot alias a multi-segment path. A recursive cycle becomes
authoritative only when at least one resolved external callsite seeds it with
the same SDK-derived chain. Cycles without that source binding, or cycles that
produce conflicting chains, do not become authoritative. Candidate collection
is followed by bounded invalidation: an unknown, missing, or ambiguous input
invalidates every dependent parameter, including downstream recursive cycles.

## Fail-Closed Boundaries

The extractor does not infer a client from a parameter name, JSDoc, comments,
types, README content, or conventional names such as `base44`. A missing,
non-SDK, or conflicting direct callsite prevents propagation for that
parameter. Lexical parameters, block declarations, catch bindings, loop
bindings, classes, and functions shadow outer injected bindings. Mutable or
reassigned client/helper aliases are not followed. Factory names are resolved
through their lexical import binding or immutable identifier aliases, rather
than matched against a file-wide name. Exported clients are resolved from the
actual immutable top-level binding, never a same-named nested initializer.
Named function/class expressions also mask outer client bindings.

Rest parameters contain arrays and cannot acquire client authority. A spread
argument at or before a target argument makes its positional binding unknown;
a spread after the target does not affect that earlier binding. Destructured
client aliases are unsupported and remain unproven. Assignments through
`for...in` and `for...of` invalidate an injected parameter like explicit writes.
Immutable exported arrow/function expressions are checked against the module's
end position when resolving imports, rather than its start position.

Dynamic dispatch, callback escape, namespace calls, unresolved re-exports, and
computed call targets are not followed. Their absence from the emitted
operation facts is not evidence that the source application lacks such a
surface; downstream coverage gates must retain the broader analysis coverage
label and known gaps.

## Evidence

SDK primitive, entity-operation, and function-invocation facts reached through
this rule include `clientBindingKind=callsite-proven-parameter`. Payload/query
facts link to the corresponding entity operation through `operationEvidenceId`.
HTTP/environment facts retain their file-level context semantics. Existing facts reached through
direct imports and ordinary derived aliases retain their prior shape and stable
identities. Every emitted `CodeFact` carries the applicable registered `ruleId`
at fact level; its `EvidenceSpan` supplies only the source coordinates for that
rule-backed conclusion. Raw argument values and source snippets are not
persisted.
