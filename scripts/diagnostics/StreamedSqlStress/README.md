# Streamed SQL volume probe

Run `dotnet run --project scripts/diagnostics/StreamedSqlStress -c Release` from
TraceMap. Optional argument is a byte count divisible by 4 MiB, at most 8 GiB.
The default generates an actual 8 GiB valid virtual byte stream of comment/GO
batches ending with one procedure. Exact SHA-256 and full consumption are checked,
as are original line coordinates. No 8 GiB allocation or disk file, database
connection or SQL execution. The process exits if sampled resident memory exceeds
1 GiB. Terminal output reports elapsed time and peak working set. This is a
byte-volume stress case; other bounded parser/evidence refusals still apply.
