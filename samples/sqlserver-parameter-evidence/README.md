# Independent SQL Server parameter fixtures

Compile/scan only; never invoke methods. No database or runtime values are needed.
Both C# and VB have 25 methods / 26 execution sites. See
[the contract, exact matrix and native commands](../../docs/validation/SQLSERVER_PARAMETER_EVIDENCE.md).
`declarations-base.json` is authored synthetic input, not SQL DDL or installed schema.
The native smoke derives explicit per-case declarations/bindings with generator and
public-input provenance, admits them through the CLI, and keeps partial reporting.
