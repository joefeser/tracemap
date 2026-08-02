# Access event/property reconciliation

## Goal

Reconcile static Access UI event properties with the exact form/report module
and procedure that can receive them, while preserving bounded command and
property evidence as candidates rather than runtime claims.

## Requirements

1. Event bindings MUST retain owner, event role, module/procedure identity,
   coverage, and a classification (`resolved`, `expression-handler`,
   `declared-handler-missing`, `ambiguous`, or `unsupported-dynamic-target`).
2. Literal `DoCmd.RunCommand(acCmdSaveRecord)` in a resolved procedure MAY be
   emitted as a `save-current-record` command candidate with a hash and span.
3. Dynamic command arguments, dynamic event expressions, unresolved handlers,
   and ambiguous modules/procedures MUST remain explicit gaps.
4. Design text parsing MUST preserve bounded multiline quoted properties,
   escaped quotes, and opaque compound-property gaps without executing Access.
5. Facts MUST contain rule IDs, evidence tiers, provenance, coverage, and
   limitations; raw source, SQL, private names, and runtime claims are out of
   scope.
