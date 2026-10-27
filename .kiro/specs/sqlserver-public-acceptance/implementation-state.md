# Implementation state

Branch `codex/sqlserver-public-acceptance`; parent frozen at
`6aa8c40e975b9277418aa3bc4b561073887c7f3e` (`codex/sqlserver-field-reads`).
Stacked package: offline public acceptance replay and tested blockers, not another
consumer feature. No parent worktree/build/output is modified. No runtime lane
is activated. Final integration/full/live validation awaits coordination.

Inspection: DNN's selected operation remains outside concrete reader/parameter
and hydration binding. Helper consumer bindings do not prove parameter bindings;
the packet must not collapse these into one alleged complete application path.

## Proven defect and bounded repair

The new same-execution check found `WithParameter` had a bounded reader match but
unknown parameter state (`nested-alias-or-escape`): the enclosing variable
declarator started before the execution span and misclassified the execution
itself. Exempt only a declarator whose initializer is reference-identical to the
current invocation, before conversion unwrapping. C# 0.22.21 / VB 0.10.24; ten
positive/negative initializer regressions. Focused parameter suite: 91 passed.
Shared Core change requires a final full suite after parent coordination.

## Development failures (not acceptance)

- Initial replay at 54291e017 failed on missing fixture assets. The runner now
  builds the eight selected public fixture projects before their scans.
- Replay at a7e2040f6 passed parameter/reader native gates, then packet inventory
  rejected the intentionally retained symlink in the native negative-input tree.
  Negative inputs under `refusals/` are now excluded from successful evidence;
  their named refusal outcomes remain bound by the hashed native receipt.
- Same-execution development check exposed the parameter defect above; expected
  match/mismatch matrix was not weakened.
