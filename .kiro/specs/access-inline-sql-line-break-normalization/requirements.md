# Requirements

## Goal

Recover deterministic Access inline-SQL dependency and binding evidence when a
SaveAsText scalar encodes SQL line breaks as `\015\012`, without executing the
SQL or broadening the Access extraction boundary.

## Requirements

1. The Access design-text parser SHALL decode only the bounded contiguous
   SaveAsText line-break pair `\015\012` inside quoted scalar properties.
2. Standalone `\015` or `\012` tokens, unsupported numeric escapes, and
   ordinary backslashes SHALL remain literal.
3. Adjacent quoted property fragments SHALL retain their current deterministic
   reconstruction behavior.
4. A multiline inline record source whose qualified wildcard dependency is
   uniquely declared SHALL expose that dependency's existing static field
   catalog to control binding composition.
5. The wildcard record source itself MAY remain partial when output order or
   runtime availability is not proven.
6. The implementation SHALL NOT execute SQL, open a recordset, read rows,
   render a form/report, or claim runtime reachability or output availability.
7. Tests SHALL cover decoded CR/LF escapes, unsupported escapes, qualified
   wildcard scoping, partial record-source coverage, and raw-SQL non-persistence.
8. The immutable private snapshot SHALL be regenerated to measure the exact
   remainder change.
