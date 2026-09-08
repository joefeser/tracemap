# Base44 normalized query evidence v3 — issue #713

Extractor `base44-evidence/0.8.0` emits `88mph.entity-query.v3` only when a
filter argument is a source-bound local object binding whose construction
cannot be represented by v2. Direct object-literal queries remain byte-for-byte
v2 descriptors.

V3 preserves v2's argument roles, redacted operands, operators, pagination and
selection controls, exact UTF-16 spans, traversal bounds, and typed gaps. It
adds the following filter-entry fields:

- `presence`: `always` or `conditional`;
- `derivation`: a nonempty array containing the source construction kind and
  exact span.

The filter value also records a redacted `construction` object binding the use
span to the variable-declaration span. Binding names and operand text are not
persisted.

The admitted construction is deliberately narrow: an intrafunction local
identifier initialized with a static object literal, followed only by static
property assignments before the SDK call. Assignments under branches, loops,
short-circuit expressions, or exception regions produce `conditional`
presence. A duplicate/reassigned field, computed key, compound assignment,
delete, method call, alias, call escape, nested-function capture, unsupported
initializer, shadow conflict, or unbounded syntax leaves a typed unresolved
gap. Runtime values are never evaluated.

V3 exists because v2 cannot distinguish a conditionally added filter field
from an always-present field. Removing that distinction would erase an
executable branch. Consumers must reject v3 until they explicitly validate its
shape and admit the exact reviewed producer. Initial host rejection is
intentional; this producer change alone does not reconcile or publish Stage 6.

The descriptor is serialized as `querySemanticsJson` inside the source-bound
`Base44EntityQuery` fact. Its fact identity includes the deterministic scan ID,
fact/rule type, source path and call lines, entity/operation identity, the full
normalized descriptor JSON, construction/field evidence, analysis gaps, and
source-file SHA-256. The fact evidence also carries the SDK-call snippet
SHA-256 and extractor identity/version. These are the exact digest inputs; raw
filter values are excluded.
