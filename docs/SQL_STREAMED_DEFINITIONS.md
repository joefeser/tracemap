# Stream larger local SQL definition exports

The ordinary `sqlserver-local-definitions-input/1` contract retains its original
256 KiB/file and 1 MiB total limits. A separate opt-in
`sqlserver-streamed-local-definitions-input/1` contract admits up to **8 GiB
(8,589,934,592 bytes) per source file and in total**. Manifest fields and exact
original file SHA-256 values otherwise stay the same. SQL is parsed, never run.

When the wizard encounters an ordinary SQL size refusal, choose `stream`. It
writes a new local manifest beside the original, preserving every listed file,
context field and original digest; generator and bounded-input provenance are
recorded. The original manifest and SQL files are unchanged. Setup validates the
new route before workspace creation. No manual splitting, hash retyping or
truncation is performed. The streamed export version is
`sqlserver-streamed-local-definitions/1`.

The reader consumes exact UTF-8 source bytes sequentially and validates the whole
original SHA-256 before returning evidence. It buffers at most one batch. A
standalone GO line is a boundary only when the pinned ScriptDom lexer agrees;
GO inside multiline strings, quoted identifiers or comments stays in its batch.
Original line spans are retained. Global duplicate checks, table/procedure
resolution and session/schema-mutation conservatism run across all batches/files.
Existing SQLCMD, GO repetition, executable top-level statement and result-shape
refusals remain. Existing v1 and explicit selected-definition contracts are not
silently expanded.

## Independent work/evidence limits

The 8 GiB cap is a source-container limit, not permission to build an 8 GiB AST.
The stream profile refuses admission when any other budget is exceeded:

| Resource | Limit |
| --- | --- |
| Source files | 32 |
| UTF-8 batch | 4 MiB |
| Tokens per batch | 100,000; existing syntax/64 MiB parser-stack guards |
| Batches | 65,536 |
| Retained definition-batch text | 64 MiB |
| Retained tokens | 1,000,000 |
| Objects | 8,192 |
| References / gaps | 65,536 each |
| Exported metadata | 32 MiB |

An over-budget, malformed or digest-mismatched input produces a named refusal,
not assumed values, silent omission or a completion claim. No deployment,
application-call binding or database-state claim is added. JSON and SQLite remain
local-only with explicit partial coverage. Large data INSERT/install scripts do
not become admitted definition sets merely because they fit the byte cap.

## Reproduce

```powershell
python scripts/tests/Test-WorkspaceWizardLauncher.py --output-root C:/reviews/sql-7mb --sql-beside-workspace --sql-export-bytes 6671580
```

For the sibling-repo/net20 layout, add `--legacy-layout`. Choose a fresh output
folder. The replay reaches the ordinary size refusal, explicitly selects streamed
mode, then verifies the complete workspace/SQL/SQLite path.

The separately invoked stress probe generates and consumes an actual 8 GiB valid
virtual UTF-8 stream in full, with exact hashing and a final definition. It does
not allocate an 8 GiB array/file. A 1 GiB resident-memory watchdog terminates only
its own process if exceeded:

```powershell
dotnet run --project scripts/diagnostics/StreamedSqlStress -c Release
```

That stress input is mostly comment batches. It verifies byte-volume handling,
not support for arbitrary 8 GiB SQL grammar or unlimited objects. Run normal-sized
fixtures separately; the generic full test suite does not run the 8 GiB probe.
