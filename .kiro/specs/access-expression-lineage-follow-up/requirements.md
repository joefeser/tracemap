# Requirements

## Purpose

Improve bounded Microsoft Access expression lineage so a design review can connect calculated controls and domain functions to deterministic form, report, query, table, and field candidates without evaluating Access expressions or widening the production extraction boundary.

## Requirements

1. The projector shall resolve a unique identifier matching a control on the same form or report to that control's stable key.
2. The projector shall preserve hash-only control references when an exact stable key is unavailable.
3. The projector shall preserve both field and control candidates, with an explicit gap, when Access expression semantics are ambiguous.
4. Domain functions shall preserve the domain object, selected field, criteria field, and same-surface control candidates independently.
5. Session/global references such as TempVars shall not be misrepresented as database fields or controls.
6. Dynamic expressions and ambiguous references shall remain partial and rule-backed.
7. Standard facts shall not contain raw expressions, customer identifiers, source values, or private paths.
8. Output shall be deterministic across input ordering.
9. A provenance-compatible private representative rerun shall record exact before/after gap counts without committing private artifacts.

## Non-claims

- No expression evaluation or VBA execution.
- No runtime branch, data outcome, or reachability proof.
- No proof that a candidate control, field, query, or table was used at runtime.
- No widening of Access COM or Windows extraction behavior.

