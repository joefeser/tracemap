# Design

`AccessVbaProjector` recognizes bounded literal RowSource assignments and reuses
`AccessQueryProjector.ProjectStaticSelect`. The resulting projection is nested
in the existing effect contract, preserving procedure, line span, condition
hash, branch order, and event-binding support.

The composer supplies a private in-memory control context derived from existing
UI bindings. Facts expose only stable keys, hashes, ordinals, coverage labels,
and limitations. No Access COM, query execution, row reads, or layout inference
is introduced.
