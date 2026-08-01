# Design

`AccessExpressionProjector` performs a bounded lexical projection. It emits
hash-only structure/function/operator metadata and safe stable-key candidates;
it does not evaluate Access expressions or retain raw text.

`AccessUiProjector` enriches each control with separate value-binding and
population fields. Static query output ordinals are supplied from existing
`AccessQueryOutputDeclared` facts, so `BoundColumn` can produce a selected
output candidate without claiming a returned value or persistence.

Existing conditional RowSource evidence remains the authoritative population
projection. Dynamic and ambiguous shapes remain partial gaps.
