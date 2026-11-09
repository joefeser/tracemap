# Same-name dataset readers

This compile-only VB sample puts two implementations in the same file.
Both expose `ReadBatch(String, ByRef ArrayList)`. Each list overload copies
boxed structs into its own typed array and forwards to a second overload.
Each struct constructor creates a provider parameter and stores it in a
field. The loop attaches that field to the command. The adapter constructor
receives the command and the body calls the inherited dataset fill method.

| Root | Expected struct array | Expected adapter constructor |
| --- | --- | --- |
| FromLedger | LedgerSlot() | SqlDataAdapter(SqlCommand) |
| FromDriver | DriverSlot() | OdbcDataAdapter(OdbcCommand) |

From the repository root, with the Debug CLI already built:

```sh
sample=samples/sql-database-matrix/provider-overloads
dotnet build "$sample/ProviderOverloads.vbproj" -c Debug -warnaserror
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan \
  --repo "$sample" --compiled-input "$PWD/$sample/bin/Debug/net472/ProviderOverloads.dll" --il-body-evidence --out "$out"
sqlite3 "$out/index.sqlite" < "$sample/review.sql"
```

Set `out` to a fresh directory outside the repository. Expect two rows.
The list and typed-array tokens differ between the two roots. Both final
call operands identify `DbDataAdapter.Fill(DataSet)`. Follow the constructor
in the same typed-array body to distinguish the providers. This query establishes
compiled call sites. Runtime dispatch, parameter values, database execution
and a command-instance dataflow join are outside its claim.

Existing fixtures cover the SQL Server table-mapping loop, optional transaction,
and the parameter constructor variants. See
[PublicLegacyCommandFlow.vb](../../messy-dotnet-workspace/vb-publish-crossdll-framework/PublicLegacyCommandFlow.vb)
and [ClosureShape.vb](../../sql-compiled-wrapper-evidence/vb/whole-protocol/ClosureShape.vb).

The [C# counterpart](../provider-overloads-csharp) has the same entry points
and overloads. Build ProviderOverloadsCSharp.csproj, scan that directory with
its own ProviderOverloadsCSharp.dll, and run this same review.sql on its index.
Its Fill operand identifies DataAdapter.Fill(DataSet) in both providers. The VB compiler emits DbDataAdapter.Fill(DataSet). Tokens and fact IDs are specific to each compiled artifact.
