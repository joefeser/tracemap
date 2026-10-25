# Independent SQL Server result-column fixtures

Compile/scan only; never invoke methods. No database or runtime values are needed.
C# and VB each have 33 methods / 34 execution sites (TwoReaders has two). See
[the contract, independent matrix and native commands](../../docs/validation/SQLSERVER_RESULT_COLUMN_EVIDENCE.md).
`result-declarations-base.json` is authored synthetic input, not DDL, catalog or
runtime evidence. `ddl/*.sql` are public synthetic parse-only templates: `base.sql`,
`renamed.sql` and `reordered.sql` are supported projections; the others are
deliberately unsupported bodies that must stay unknown shapes.
