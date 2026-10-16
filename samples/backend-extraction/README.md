# Synthetic backend extraction input

Build-only .NET 10 library with no executable entry point, provider package,
connection string, credential, database or service. Do not call its methods.
The HTTP literal uses the reserved `.invalid` domain; SQL is invented.

`dotnet build samples/backend-extraction/BackendExtraction.Sample.csproj`

Cases: two possible roots share one stored-procedure callsite; a query has no
local caller; literal and dynamic HTTP destinations; a name-only string decoy.
The methods named SaveClick/JobTick are not ASP.NET event-wiring proof. Abstract
DbConnection/DbCommand calls intentionally exercise a provider-dispatch boundary.

This is a compiler/scanner input and a future full-index inventory oracle, not
an asserted end-to-end migration success. Initial retained-report projection
tests separately exercise deduplication, missing paths, coverage and escaping.
Reuse `samples/compiled-dotnet-evidence/constructed-generics` for compiler-built
generic-definition edges; do not duplicate or weaken its negative controls.
