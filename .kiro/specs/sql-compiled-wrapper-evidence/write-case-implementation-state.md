# SQL write evidence

Branch `codex/webforms-sql-evidence` includes bounded compiled string-array
operands, wrapper-path composition and a separate VB source argument rule.
The SQL matrix has public VB and C# compiled controls for scalar and array
composition, escaping arrays and same-name SQL Server/ODBC wrappers.
The provider query joins through module and metadata identity for both
implementations. It does not establish execution or runtime parameter values.

The VB source sample exposes SELECT and UPDATE arguments, source spans and an
unverified command-instance status. Source hit, command binding, bulk destination,
parameter attachment and transaction identity each require their own evidence.
See samples/sql-database-matrix and samples/vb-sql-argument for hand review.
