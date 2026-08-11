# Report-specific remainder follow-up

## `qrptSAsByWeek` crosstab output mismatch

Rick's private review identified report expressions that reference numeric
outputs such as `[1]` while the visible report metadata exposes `W0`–`W11`
controls. The current static corpus can show that both names occur, but it
cannot prove that one is an alias for the other. A saved crosstab may derive
numeric columns from a pivot expression, while report controls may use a
different naming layer.

The focused follow-up is tracked in
`.kiro/specs/access-crosstab-alias-provenance/`. It now preserves:

- output alias kind (`explicit-as`, `access-colon`, `direct-field`, or
  `pivot-literal`);
- role-scoped source-expression hashes;
- aggregate/value source-field candidates;
- pivot-expression source-field candidates.

This permits an evidence-backed chain such as “literal pivot column → pivot
expression → candidate source field” without turning `[1]` into `W1`, and
without claiming that the query ran or returned a value.

## Remaining evidence boundary

The private Windows bundle still needs an optional same-snapshot QueryDef
property reconciliation if the owner wants to confirm Access's stored
`ColumnHeadings`/pivot metadata. Direct query execution, recordset reads,
report rendering, and runtime DCount behavior remain out of scope. The
numeric-vs-`W` mismatch stays a deliberate partial/gap until that evidence or
owner confirmation exists.
