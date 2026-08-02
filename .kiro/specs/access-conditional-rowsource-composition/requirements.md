# Access conditional RowSource composition

## Scope

Trace bounded literal `Me.<control>.RowSource` assignments in VBA procedures,
including branch condition hashes and exact procedure/event provenance. Project
literal SELECT statements without execution and preserve dynamic or ambiguous
forms as explicit gaps.

The projection may carry safe SQL hashes, dependency keys, output ordinals,
predicate/order hashes, static function-name hashes, BoundColumn, ControlSource,
and surface RecordSource candidates. It never stores raw SQL or source text.

## Limitations

This evidence does not prove runtime reachability, branch feasibility, selected
values, persistence, or a foreign-key relationship. Dynamic SQL, unresolved
functions, unsupported nesting, ambiguous controls, and nonliteral BoundColumn
values remain partial.
