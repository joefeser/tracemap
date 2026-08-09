# Review prompts

1. Confirm that `pivot-literal` is not treated as an alias for `W0`, `W1`, or
   any other report control name.
2. Confirm that pivot-expression source candidates are separate from aggregate
   source candidates and do not imply returned values.
3. Confirm that no raw SQL, literal pivot value, local path, row, or report
   rendering result is persisted.
4. Confirm that dynamic, malformed, duplicate, and unresolved shapes remain
   partial and that the existing rule ID and limitations are preserved.
5. Confirm that the slice uses no new COM calls and no query execution.
