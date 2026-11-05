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

The reader consumes exact original source bytes sequentially and validates the whole
original SHA-256 before returning evidence. The streamed extractor `/2` accepts strict UTF-8 (with or without BOM), or
BOM-marked UTF-16 LE/BE. BOM selection is explicit from the first four raw bytes;
unmarked legacy encodings and UTF-32 are not guessed. Invalid sequences,
truncated UTF-16 units and unpaired surrogates refuse admission rather than
being replaced. Hashes and the 8 GiB cap cover original bytes including the BOM;
the 4 MiB batch budget measures decoded text re-encoded as UTF-8. JSON manifests
remain UTF-8. Ordinary v1 decoding is unchanged; its wizard decoding failure can
also offer the explicit stream opt-in. It buffers at most one batch. A
standalone GO line is a boundary only when the pinned ScriptDom lexer agrees;
GO inside multiline strings, quoted identifiers or comments stays in its batch.
Original line spans are retained. Global duplicate checks, table/procedure
resolution and session/schema-mutation conservatism run across all batches/files.
Existing SQLCMD, GO repetition, executable top-level statement and result-shape
refusals remain. Existing v1 and explicit selected-definition contracts are not
silently expanded.

After updating this branch, rebuild the launcher tool once to avoid reusing its
previous published DLL, then continue the saved setup. For the example layout:

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 -Build webforms-review wizard --root C:/tracemap-output --continue
```

Use your existing output root in place of the example. Select workspace and
choose `stream` at the SQL size refusal. Saved source/DLL/SQL selections remain.

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

## Local qualification (2026-11-05)

Core/CLI touched lanes passed 578 tests. Both normal and sibling-repo/net20
launcher replays completed with an exact 6,671,580-byte SQL export; the latter
indexed 552 SQL objects and passed three SQLite integrity checks. A separate
8,589,934,592-byte virtual-stream run consumed and hashed every byte, found the
final definition with its original line coordinate, and completed in 372,581 ms
with 261,373,952 bytes sampled peak resident memory on macOS. That measurement
is for the mostly-comment stress input, not an arbitrary 8 GiB definition set.

The full Debug `-warnaserror` gate on the Core/CLI implementation checkpoint
`807b2b0fd1843ed513567e29644fa485dff5feb3` passed: 6,037 passed, 3 skipped,
zero failed (6,040 total). No private-input or runtime acceptance claim.

### UTF-16 qualification

The `/2` streamed extractor additionally passed 42 focused tests covering
UTF-16 LE/BE BOM, non-ASCII/supplementary characters, CRLF line coordinates,
raw-byte digest drift, malformed/truncated code units and unpaired surrogates
(including inside otherwise valid SQL string literals),
UTF-32 refusal, and small/large ordinary-to-stream wizard recovery. Both modern
and sibling-repo/net20 launcher replays completed with an exact 6,671,580-byte
UTF-16 LE BOM file. The latter indexed 275 SQL objects, retained the original
FF FE bytes/hash and passed all three SQLite checks. Reproduce in a fresh folder:

```powershell
python scripts/tests/Test-WorkspaceWizardLauncher.py --output-root C:/reviews/utf16-sql --legacy-layout --sql-export-bytes 3335789 --sql-encoding utf16-le
```

The byte argument describes the authored UTF-8/ASCII content before optional
encoding: UTF-16 BOM makes this fixture `2 * 3335789 + 2 = 6671580` raw bytes.

The `/2` full Debug `-warnaserror` run on `a3257c4d1` completed with 6,045 passed,
3 skipped and one unchanged cancellation-diagnostics test failure caused by a
listener capturing a different concurrent trace. All four diagnostics tests
passed in an isolated same-build rerun. This full run is not a green gate; the
listener-isolation follow-up remains. The final 42-test encoding/wizard lane
and both complete UTF-16 workspace replays passed.
