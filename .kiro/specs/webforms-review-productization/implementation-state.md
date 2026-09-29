# Web Forms Review Productization Implementation State

## Current gates (2026-09-29, admission accounting and operator consolidation)

- Added separate generator/input-bound metadata and IL logical admission usage,
  with successful aggregate reservations and aggregate refusal counts. These are
  not CPU/runtime/total-scan work; per-input caps and failed/skipped inputs retain
  their own outcome gaps. Historical missing fields and materialized fact
  identities are preserved. Native status validates and projects known records.
- Initial rebuilt focused gate 91862 passed 192 tests in 4 minutes 7 seconds.
  Subsequent gate 9677 passed 192 and failed one assertion in 4 minutes 26 seconds:
  a historical timing-only test incorrectly expected the independently retained
  admission records to be absent. That source assertion is corrected; its failed
  result is preserved, not relabeled green. Full rebuilt solution gate 11702
  passed on the corrected source: 2,800 tests, zero failed/skipped in 14 minutes
  40 seconds. The candidate is not a committed exact-head gate yet. Subsequent
  help wording distinguishes the passing public stress case from unproven
  arbitrary graph capacity; final rebuilt validation covers that small change.
  Rebuilt CLI SHA-256 53973e32ab94d2f70f70964ac846b5491f4891f1b3a1746ba296022996adcc8a;
  Core e5c4dd1c908f88d2f3600e2a890f5759b7c6783f3a756f8fee3c9f993146f069;
  tests 127d8ce18e6c45c757d5e517ac6155ee16bd332486bf7afa39b61540079193e4.
  This gate covers dirty candidate source, not a new committed exact head.
- Replaced the long native slice-history document with a shorter current
  operator contract. Original notes are preserved under docs/history; the
  PowerShell quickstart is explicitly the compatibility path. Native start,
  config/receipt authority, one run root, status/resume, independent limits,
  grouped lossless retrieval and protect-only retention are documented together.
- Added docs/validation/webforms-native-completion-audit-2026-09-29.md mapping
  each goal requirement to implementation/tests and the authorized Windows
  start/status/resume commands. Interactive file-URL browser inspection was
  denied by browser policy; no workaround was attempted and no new visual pass
  is claimed. Actual renderer/navigation and parity tests remain separate proof.
- Next: commit this owned candidate,
  rebuild the clean committed head, run final full no-build regression and strict
  graph scale sequentially with an absolute output root outside distributions.
  Keep final results/receipts without rebuilding their pinned binaries. Audit
  original objective before completion; no merge or deletion is authorized.
- No private Windows run, push, PR, merge, folder deletion or wrapper replacement
  occurred. Final exact-head regression/scale and private operator acceptance
  remain distinct gates; do not infer completion from a local subset.

## Tool durability exact-head gates (2026-09-29, strict fresh scale passed)

- Tool durability committed as 0992a1fce6b2ebcb71063309cca1566b12b9ea40.
  Exact-head solution build 20741 passed in 13.53 seconds, zero warnings/errors.
  Strict fresh 32/256-page graph-admission scale 79958 passed in 5 minutes
  53 seconds, one test, zero failed/skipped. Its receipt is preserved under
  src/dotnet/tests/TraceMap.Tests/bin/Debug/net10.0/output/native-scale-retained-tool-fresh-20260929/native-scale.receipt.json.
  The relative output option resolved against the test working directory;
  subsequent benchmarks must use an absolute root outside tool distributions.
- Exact CLI SHA-256 0e83b9d379bfc3cc100d9008cfc9a244b5f7d6a6776659b017bcbfd7d5cde00d;
  Reporting 2ad5373e5ed3d5635222040cce524a7c3b739a7a2581f30a2df3e2d3a53d46ad;
  tests 9148a8a1edbecfbf2b5a35c40a3de1c7096a2b22dc53e11051cac6069390ba2d.
  These binary hashes cover the committed tool-durability slice, not the later
  admission accounting. Publication/private acceptance remain gated.

## Original tool durability (2026-09-29, rebuilt focused validation passed)

- Phase observations/help fix committed as fc8e56a166874221c68b90a67d7a0a4c5b2d9ca0.
  Exact-head full solution rebuild 8548 passed in 12.91 seconds with zero
  warnings/errors. Full no-build regression 42876 passed on these binaries:
  2,765 passed, zero failed/skipped in 14 minutes 27 seconds. This validates
  the committed phase-observation/help slice, not the later dirty tool-copy source.
  CLI SHA-256 590ab837468341ca090f343acc5c9005e3295a947e3e4ebcc8afda1db3da0b87;
  Reporting f979fe19bd1b5dcd6293b5e3d59ec35b6504a0ecd61094a5bfd06f3c1243d70a;
  tests d795b9084ab95959fef58d38fc0b7c3d054df360fa0f5b60b28b252746df1b14.
- New source retains optional original distribution/entry/runtime locators with
  the existing runtime digest in checkpoint payloads. Status projects declarations
  without an availability claim; retention protects original locations.
  Historical absent fields remain absent even after failed-attempt/report resume.
- Added explicit retain-tool for a completed native run: lock/verify run, inspect
  original bounded distribution, copy to a new separate folder, hash/recheck both
  sides and publish a generator/input/checkpoint/file/payload-bound local manifest.
  No copied code is executed, no run/source/site/SDK bytes are changed, and all
  failure staging/original proof remains preserved. External runtime/SDK/adaptor
  closure stays unpinned; the distribution copy is not self-contained or portable.
- Source tests cover actual distribution copy and subprocess completed resume,
  immutable run bytes, protection-only dependency plans, historical missing
  locations, missing/changed tools, runtime mismatch, overlap/existing output and
  eight malformed locator categories. Rebuilt focused validation 65239 passed
  212 tests, zero failed/skipped in 4 minutes 45 seconds with no compiler
  warnings reported. A subsequent entry-DLL/generator-hash guard and wrong-entry
  refusal case passed rebuilt focused tool validation 82273: 16 passed, zero
  failed/skipped in 36 seconds, no compiler warnings reported. Exact-head full
  regression and representative scale still gate publication of this slice.
  No push, PR, merge or cleanup occurred. Whole goal remains active.

## Production phase observations (2026-09-29, focused rebuilt validation passed)

- Exact-head regression 5568 ended against 872bba50 binaries in 13 minutes
  40 seconds: 2,749 passed, one failed, zero skipped. The sole failure expects
  old generic preflight-only help text. Source assertion now pins the updated
  explicit preflight/start distinction. No runtime/admission regression was
  reported, but the final gate is failed, not green; retain that result.
- Added optional nested phaseUsage to terminal scan/report checkpoints, with
  elapsed attempt time and constant-space start/end/requested one-second parent
  working-set samples. Measurements inherit checkpoint generator/input/payload
  commitments, retain their own rule/tier/scope/limitations and do not change
  static report/fact identities. Exact OS phase peak, child/adaptor usage, CPU,
  metadata/IL work and transient disk remain explicit gaps, not fake zero.
- Status projects the exact retained attempt observations; old absent fields
  remain unknown. Success/failure/cancellation and artifact admission remain
  separate. Source tests pin sampled aggregates/idempotent finish, null/invalid
  readings, historical resume without rewrite, nine malformed-usage categories,
  actual scan/report/failed/cancelled/status/relocated contexts. Native subprocess
  scale now retains both attempt observations and verifies their scopes, nullable
  readings and elapsed time within the measured whole CLI run; completed resume
  still must preserve checkpoint bytes. Rebuilt focused validation 81726 passed
  197 tests, zero failed/skipped in 2 minutes 51 seconds, with no compiler
  warnings reported. Full solution build 48118 passed in 3.85 seconds with
  zero warnings/errors. Exact-head full regression remains required before
  publication; this is not whole-workflow completion.

## Current committed validation target (2026-09-29, help contract failure recorded)

- Source slice committed as 872bba506039d54a8cc9efc489d26bab7d20c754 on
  codex/webforms-native-preflight. No push, PR, merge or cleanup occurred.
- Rebuilt that exact head with full solution build 27318: 12.60 seconds,
  zero warnings/errors. Final full no-build regression 5568 ended with one
  stale help assertion failure, 2,749 passed and zero skipped in 13 minutes
  40 seconds. Source fix and phase-observation validation are above. Require
  a new passing full gate before pushing; focused starter and earlier full
  regression results below are not substitutes for that final gate.
- Goal remains active. Continue production phase/resource observations,
  durable original tool/runtime/dependency handling and the authorized private
  Windows acceptance contract. Preserve every proof, public failure and wrapper.

## One-command starter (2026-09-29, focused validation passed)

- Added native start composition: explicit config/new review root, optional
  exact-commit attestation only for receipt preparation, then pinned preflight
  and ordinary fresh/attachment execution. Existing explicit receipts require
  no new attestation. Admission gates, budgets and source/published paths remain
  unchanged. Atomic new-root reservation refuses replacement; failed outputs
  remain intact and print the exact run/ resume root. No discovery or cleanup.
- Added fresh/attachment + completed resume, existing receipts, missing/wrong
  authority, source membership, overlap, failed-scan retry and precancellation
  tests, plus changed-config rejection between preparation/preflight/execution.
  Full no-build regression 97195 completed against the successful scale binaries:
  2,739 passed, zero failed/skipped in 11 minutes 46 seconds. Starter rebuild
  and focused validation 93397 passed 182/182 in 3 minutes 29 seconds, zero
  failed/skipped and no compiler warnings. CLI help smoke exits zero. A final
  help clarification passed full-solution build 75562 in 7.93 seconds with zero
  warnings/errors. After committing this validated source slice, rebuild and
  run final full regression against that exact committed head before pushing.
  The goal is not complete: production phase usage, durable tool/dependency
  availability and authorized private Windows parity remain open.

## Indexed terminal fan-out classification (2026-09-29, fresh scale passed)

- Fresh strict run 11551 stayed CPU-active beyond eleven minutes. Source audit
  found ClassifyLegacy decoding/counting every global edge for every path:
  392,192 edges multiplied by 1,024 required variants. This is distinct from
  page-join admission. Explicit SIGINT stopped only owned native CLI PID 92794;
  terminal test failed after 13 minutes 26 seconds with child exit 130. Owned
  PIDs 92794/92793/88780/89236 are absent. The retained scan and incomplete
  reports remain in output/native-scale-full-il-4g-fresh-20260928; checkpoint
  0003 remains reports-started and is not completed report evidence. No aggregate
  success receipt exists. This is an intentionally cancelled incomplete attempt.
- Added an incoming-edge SQLite index and threshold query reading at most five
  references per classification, never edge payloads. All global edges contribute
  exactly as before; the memory branch uses the same threshold. No ambiguity,
  classification, rule, tier, path ordering or full-inventory admission is pruned.
  Diagnostic counters expose incoming queries/references and global edge payload
  reads. Existing 128/1,024-branch legacy guards now require bounded global
  payload reads independent of retained paths, exact parity and <=5 references
  per incoming query. Corrected the rule catalog's older distinct-root wording:
  the implementation counts incoming edges, not distinct/runtime callers.
- Rebuild in 82603 passed 114/114 focused memory, publication, preflight, codec,
  profile and real native attachment checks in 11 seconds, zero failed/skipped
  and no warnings. Attachment now respects explicit/default IL body budgets.
- Fresh strict 32/256 scale run 90412 passed in 6 minutes 14 seconds into
  output/native-scale-indexed-fanout-fresh-20260929. Both IL inventories and graph
  queries admitted; all 24,000/192,000 sparse methods, 32/256 surfaces and
  128/1,024 required static paths were retained. Completed resume preserved
  run bytes and source/published rosters. Coverage remains partial, including
  cycle truncation; generated PE/IL is not an aspnet_compiler acceptance run.
- Actual native run elapsed 32,970/304,763 ms with OS peak resident usage
  941,457,408/4,185,030,656 bytes. Retained run disk was
  629,400,563/5,034,029,161 bytes; transient scratch/sorter disk peak is not
  measured. Large global graph retained 393,740 nodes and 392,192 edges.
  Explicit stress limits remain 250,000 IL bodies, 1 GiB input text and 4 GiB
  graph scratch; production defaults are unchanged. Native CLI generator
  4336c1776ca9474ab8982e9cae87c0e5ed870ab2484e487863dd37db4ad75ab9;
  test generator 9f4e8bf01aadcfee6bdf5566ac1f271dea5690a389afd6cca922fd818d7cfd23;
  bounded input bf1eacc4e9837dc10929bbaae5478ad51415f991092d452b804e0444943b59eb.
- Full no-build regression 97195 passed against those same binaries: 2,739
  passed, zero failed/skipped in 11 minutes 46 seconds. New starter source
  requires a separate rebuilt focused gate and final full regression.
  Private acceptance, production phase instrumentation and durable dependency
  packaging remain open. Original proof and every failed attempt are preserved.

## Full IL inventory capacity gate (2026-09-28, in progress)

- Previous turn made concrete source progress (explicit IL body budget and
  130 focused passes). This continuation reverified live handle 21300; it then
  failed terminally in 2 minutes 40 seconds. Both data/site IL outcomes are now
  admitted under the explicit 250,000-body budget; large graph refusal is
  graph-text-bytes, with zero classified paths/nodes. All 256 surfaces remain
  retained. This is a capacity failure, not acceptance or source evidence loss.
- Read-only SQLite measurement of the retained full combined input records
  398,101 facts and 743,548,761 serialized payload bytes, exceeding the explicit
  512 MiB diagnostic input-text budget. The stress profile now explicitly
  declares 1 GiB input text, leaving its 2 GiB storage, fact/edge/path/work caps
  unchanged. No production default changes.
- The retained-index storage diagnostic now optionally declares text bytes
  with TRACEMAP_GRAPH_DIAGNOSTIC_TEXT_BYTES, validates 1 through Int32.MaxValue,
  and binds the effective value in its exact diagnostic input. Default remains
  512 MiB. Rebuild and two focused checks passed in session 31901 (2/2, 868 ms).
- Full retained replay in session 8476 finished in 34 seconds. It categorically
  refused storage during initial nodes under explicit 1 GiB text / 2 GiB scratch:
  398,101 successful facts, 152,338 successful nodes, zero edges and last valid
  logical allocation 2,143,473,664 bytes. These are partial successful writes,
  not an admitted graph. Input SHA 10978c52d1182b573e317e426fcee31fa8792add69cc5a6ddf115355d6b1ecd5
  was rehashed unchanged. Receipt and fixed stage trace stay retained in
  output/native-scale-full-il-storage-2g-replay-20260928.
- Full retained replay with explicitly declared 4 GiB scratch finished in 82800:
  1/1 passed in 1 minute 58 seconds. Its categorical receipt in
  output/native-scale-full-il-storage-4g-replay-20260928 records admitted,
  398,101 facts, 393,740 nodes, 392,192 edges and 3,877,765,120 logical scratch
  bytes. Same full input rehashed unchanged. Test generator
  89e4db66bf88cea7929f6411f3eb78430ab809cd217347c0ff36d006621056dd;
  Reporting cbc823ec75bee7cee5a68a9451e8492f3c629c6baf15387e73a07d02ef7ad8b9;
  bounded diagnostic input b7d3b42a2fa8e8b9ad5213f1b394cf03761cd165c3853af8255ca21af94d3ae0.
  This proves full retained admission, not fresh workflow or OS-peak acceptance.
- The stress profile now explicitly pins the measured 4 GiB scratch allowance
  alongside 1 GiB input text and 250,000 IL bodies. Production defaults remain
  unchanged. Tiny profile guard rebuilt and passed 1/1 in session 59638 (672 ms,
  no warnings). Strict fresh 32/256 run is now live; require all four branches
  per page, complete IL inventory and immutable resume. Output root is
  output/native-scale-full-il-4g-fresh-20260928. No runtime rebuild until it ends.
  Preserve all inputs, old failures and partial observations.
- Live strict fresh handle is 11551. Read-only wiring audit also found the native
  compiled-only attachment options still used the default IL body count. Source
  now passes the same optional budget/default into attachment; direct option
  checks and real attachment manifest checks cover both default and explicit
  values. These later source edits are not rebuilt yet, preserving the live
  fresh benchmark's exact generators. Rebuild/validate after 11551 terminates;
  do not claim this earlier fresh measurement as an exact later CLI generator.

## Published-page join throughput (2026-09-28, in progress)

- Prior turn was concrete progress: 173 focused checks and a hash-bound live
  stage trace. This turn reverified session 43166/process tree and its compiled-il
  entry at 25,210 ms; after more than six minutes it still had not left that stage.
  Explicit SIGINT stopped only owned runner 69227, terminal exit 1, and all
  PIDs 69227/69686/69689 are absent. The partial trace stays retained and is
  not an admission receipt. Original combined index rehashed unchanged.
- AddProjectlessPublishCandidateEdges now constructs complete typed rosters
  once, keys pages/handlers/bindings/declarations by exact source/file, retains
  every generated-type competitor including unbound mapless candidates, and
  reuses qualified method indexing with the original final receipt/hash/type/
  name/span predicates. This replaces repeated global fact decoding, not global
  inventory admission. Type-specific managed allocations remain a separate gate.
- New guard uses 8,011 public rows and 1,000 repeated page lookups: exactly seven
  complete typed roster passes, no further fact reads; duplicate pages/bindings/
  methods and unbound type competitors remain present. Metadata method boundness
  follows the original page predicate. The qualified fallback roster is unchanged.
- Focused publication/memory/native/attachment coverage passed 69/69, zero
  failed/skipped, 23 seconds in sessions 50650 and 33303, no compiler warnings.
  The latter rebuild adds compiled substage boundaries for PDB identity,
  published pages, member candidates and metadata/IL edge work. This is public
  static fixture coverage, not authentic private ASP.NET/Windows acceptance.
- Retained large replay finished in terminal 8337: one test passed in 44 seconds.
  output/native-scale-storage-page-joins-2g-replay-20260928/graph-storage.receipt.json
  categorically records admitted, 202,518 facts, 197,132 nodes, 3,072 edges and
  1,466,482,688 logical scratch bytes under the explicit 2 GiB budget. Original
  input SHA remains bf3a8f03767af85602dc4e3176736e60e7c7ab7b919464988b18bb4b57018afe.
  Reporting generator is cbc823ec75bee7cee5a68a9451e8492f3c629c6baf15387e73a07d02ef7ad8b9;
  bounded diagnostic input is e1800e25488129551fb26b54144179aef9ddc550dc958d2cad94345e559d34d6.
  Published-page joins took 2,061 ms; traversal took 1,790 ms. These are local
  diagnostic wall intervals, not production phase/OS-peak/transient-disk metrics.
- Strict fresh native 32/256-page acceptance in terminal 37521 failed after
  3 minutes 21 seconds; output/native-scale-page-joins-declared-storage-20260928
  stays retained. The 32-page run retained 128 paths, 49,228 nodes and 49,024
  edges in 49,067 ms with OS peak 913,195,008 bytes, then resumed byte-identically.
  The 256-page graph admitted 197,132 nodes but zero paths and 3,072 edges.
  Its scan manifest categorically records IlBodyCountLimitExceeded on the data
  assembly under maxBodyCount 50,000: metadata admission was not IL admission.
  This is not successful large end-to-end acceptance or a throughput-only gap.
- Added optional native ilMaxBodies, absent historically/default 50,000, validated
  1 through 1,000,000, hash-bound, passed to --il-max-bodies and exposed in status.
  The graph stress profile explicitly declares 250,000 and the strict test now
  requires all IL outcomes admitted before graph/path acceptance. Focused checks
  in 33121 passed 130/130, zero failed/skipped, 2 minutes 36 seconds, no compiler
  warnings. A later two-test rebuild in 58270 pins status and strict IL assertions:
  2/2 passed in one second, no warnings. New strict fresh run is live in session
  21300, output/native-scale-explicit-il-bodies-2g-20260928, declaring 250,000
  IL bodies and unchanged explicit 2 GiB graph storage. Do not rebuild runtime
  binaries until it ends. Inspect IL outcomes before graph capacity/branch
  acceptance; preserve every output on failure. Full regression, actual complete
  graph capacity, production phase metrics and private acceptance stay open.
  No merge, proof deletion or hidden production-default increase occurred.

## Throughput diagnosis and live stage trace (2026-09-28, in progress)

- The previous continuation made concrete source progress and verified a live
  wait. This turn reverified terminal session 53655 and its exact process tree.
  The retained 192,000-method query remained active beyond twenty minutes,
  exhausting the native fresh workflow's per-phase timeout for just one query.
  Explicit SIGINT canceled only owned runner PID 10376; terminal exit was 1
  (Attempting to cancel the build), and PIDs 10376/10827/10831 are now absent.
  This is an incomplete canceled attempt, not graph admission or storage refusal.
- Rehashed the original combined index after cancellation: unchanged
  bf3a8f03767af85602dc4e3176736e60e7c7ab7b919464988b18bb4b57018afe.
  output/native-scale-storage-declared-2g-replay-20260928 remains retained with
  no generated receipt. No proof dependency was deleted and no merge occurred.
- The diagnostic now flushes a maximum of 32 fixed stage entries / 64 KiB to
  graph-stages.ndjson. Every entry binds exact test/Reporting generators and
  the actual bounded input/query/budgets, and says attempt-stage-entry-not-admission.
  Completed receipt hashes this trace. No method/path/source labels are printed.
  Internal graph stage callbacks are diagnostic-only; published graph evidence
  remains deterministic. Stage times include waits/GC, not CPU or OS peak.
- Rebuild plus focused validation finished in terminal session 19044: 173 passed,
  zero failed/skipped, 3 minutes 5 seconds; no compiler warnings. This includes
  explicit scratch admission/refusal through both graph APIs and bounded trace
  commitments. It is not representative large-graph acceptance.
- Instrumented retained-index diagnosis is live in terminal session 43166 into
  output/native-scale-storage-staged-2g-replay-20260928. Testhost PID 69689,
  runner 69227 and vstest 69686 are confirmed live. Do not rebuild their binaries.
  Trace binds Reporting e9979f0e49484395d27d7188e6266d4cce9a0719b4e0182b42a592c86cb45296
  and test b633b2f9d916866f23a3825a9c22925868dedc7327f4b28f48ee3c0d005002d0.
  It entered compiled-il at 25,210 ms after passing read/inventory/nodes/source
  edges/legacy roots/symbol reconciliation, and remains in that stage. There is
  no final admission receipt yet.
- Source inspection found repeated full-fact enumerations inside each map and
  handler of AddProjectlessPublishCandidateEdges, despite the earlier qualified
  member-candidate index fix. Next replace those repeated reads with exact
  indexed/type-qualified inventories, preserving every relevant bound/unbound,
  overload, source-file and receipt competitor, then pin parity and actual
  enumeration work before remeasuring. A staged sub-observation can further
  isolate compiled helper costs if needed. Require strict fresh dual-graph
  acceptance after throughput is corrected. Capacity,
  per-phase production resource instrumentation and private acceptance stay open.

## Explicit scratch budget and raw framing (2026-09-28, in progress)

- Removed the unsuccessful Brotli experiment from the active scratch codec.
  Raw UTF-8 JSON now uses exact length framing; all global competitors, decoded
  cache bounds, ordering and published identities remain unchanged.
- Native report configuration can explicitly declare maxGraphStorageBytes from
  64 KiB through 16 GiB. Omission preserves 512 MiB and historical serialization.
  The declared value is hash-bound, shown in status, and passed to packet and
  compiled-path queries independently of input/work/path/output limits.
- The public graph stress profile explicitly declares 2 GiB, and its receipt
  exposes that budget. Admission and throughput remain unverified until a new
  measured replay and strict end-to-end run pass. No production default was
  silently increased and no refusal counts as large-graph success.
- Initial rebuilt focused coverage had 169 passing and three failing cases:
  invalid-budget tests expected the wrong exception type. They now pin the
  existing typed PreflightException. Rebuilt focused coverage passed 172/172,
  zero failed/skipped, in 2 minutes 6 seconds. The later strict dual-graph gate
  and explicit 2 GiB corpus configuration rebuilt successfully; its tiny corpus
  test passed 1/1 in 592 ms. git diff --check is clean.
- Read-only retained 192,000-method replay is running with explicit 2 GiB scratch
  storage into output/native-scale-storage-declared-2g-replay-20260928. It has not
  returned a receipt or admission result after eight minutes. Terminal session
  53655 owns the test; testhost PID 10831 remains active. No runtime binary may
  be rebuilt until it ends. A short native process sample is retained in
  /tmp/dotnet_2026-09-28_225414_vznT.sample.txt; managed symbols are unresolved,
  so it cannot identify the slow managed stage. This is not a throughput pass.
- Added a direct test for low-budget refusal versus explicit-budget admission
  through both public packet and selected-symbol APIs. It is not rebuilt yet;
  run it after the retained replay terminates, then run strict fresh graph
  acceptance under the declared budget. Preserve every previous output.
- The prior goal turn was concrete progress (explicit budget, corrected typed
  tests, 172-test pass); this continuation confirms the same replay handle and
  testhost live at thirteen minutes. Observation timeout is not process exit.
  No restart, rebuild, deletion or merge occurred.
- Added internal fixed graph-stage wall-time observations for index reads,
  endpoint/surface work, nodes/edges, reconciliation, IL/VB/dispatch bridges,
  sort, report traversal and input rehash. They are retained only in the
  generator/input-bound local diagnostic usage, not in deterministic published
  path evidence. Refusal retains incomplete timings and never gains paths.
  Timings include waits/GC and do not claim CPU time, CLI-phase timing, OS peak
  or transient disk. Tests and limitations are updated in source; rebuild and
  validation remain pending until live session 53655 ends. Diff check is clean.
- The compression sections below record superseded experimental observations,
  not the active codec or a completed scale solution. All failed inputs and
  outputs remain retained, and no private proof cleanup or merge was performed.

## Storage refusal allocation diagnostic (2026-09-28, in progress)

- Strict compressed graph acceptance terminated failed (1 failed, 6 minutes
  37 seconds): the 192,000-method case still refused graph-storage-bytes.
  Its completed partial reports and every input remain retained; no aggregate
  success receipt was emitted. Compression is not a completed scale fix.
- Added internal storage phase and bounded SQLite dbstat allocation snapshots
  every 8,192 successful scratch writes. With journaling disabled, SQLITE_FULL
  can leave the disposable scratch database unreadable. Refusal uses only prior
  successful snapshots and managed write counters, never queries/reuses that
  damaged graph. Missing snapshots/dbstat remain unknown, not zero. Classified
  paths and graph counts remain empty on refusal.
- A local explicit retained-index diagnostic binds exact test/Reporting
  generators, complete input-index SHA, actual query options and budgets. It
  emits no methods/source text and cannot overwrite an existing output. The
  tiny schema-refusal test correctly has no store observation; a separate
  128 KiB fixture exercises post-schema refusal snapshots.
- Rebuilt codec/graph-memory coverage passed 45/45, zero failed/skipped, in
  15 seconds with no build warnings. The retained 192,000-method combined index
  was replayed read-only into output/native-scale-storage-replay-20260928:
  1/1 passed in 19 seconds and rehashed the original index unchanged. Refusal
  occurred in nodes after 202,518 successful fact writes and 43,191 node writes,
  with zero edges. Last valid logical allocation sample was 525,299,712 bytes;
  final successful-write payload counters were 588,291,796 decoded and
  300,340,120 framed bytes. The dbstat facility is unavailable on this SQLite
  build, so physical object allocations remain null, not invented estimates.
- This identifies global scratch admission, not selected traversal, as the
  current capacity bottleneck. The operator workflow still needs an explicit
  declared scratch-storage budget instead of a hidden fixed ceiling, with the
  existing default preserved and actual large-graph/throughput acceptance under
  its declared budget. Compression's slowdown must also be resolved; it cannot
  be shipped as the finished scale solution merely because parity passes.

## Lossless private graph payload storage (2026-09-28, in progress)

- Source now encodes every private fact/node/edge JSON row in a v2 scratch
  database as a length-framed raw-or-Brotli blob, retaining compression only
  when smaller. No input/published artifact, identity, alias key, ordering or
  graph-storage ceiling changes. Decode rejects oversized declarations and
  requires exact decoded size and complete compressed-frame consumption.
- Random-access outgoing pages continue to budget decoded JSON bytes, not the
  smaller compressed frame. Internal usage observations record actual decoded
  and stored payload bytes separately; this is not an OS memory guarantee.
- Added raw Unicode/escaping, compressed null/identity round-trip, deterministic
  encoding, truncated/trailing/invalid-mode/oversize frame source tests. Existing
  graph parity/all-branch tests remain the integration oracle. Added an explicit
  graph-admission-required diagnostic flag so a fail-closed refusal cannot pass
  the forthcoming storage acceptance run.
- Exact pushed 6d306274136add97b6bdea278c868f12d394114c full regression
  terminated green: 2,722 passed, zero failed/skipped in 12 minutes 59 seconds.
  Runtime rebuild started only afterward. New source validation is in progress:
  codec/graph/native coverage, followed by a fresh required-admission graph run.
  A new parity fixture budgets highly compressible 300 KB edge rows by decoded
  bytes and requires all three branches. Rebuilt codec, graph-memory and native
  scale coverage passed 46/46 in 23 seconds, zero failed/skipped and no build
  warnings. An initial test-fixture compile typo was fixed before this pass.
  Strict retained admission is running at output/native-scale-compressed-graph-20260928
  with TRACEMAP_WEBFORMS_REQUIRE_GRAPH_ADMISSION=1; its outcome remains pending.
  The compressed 24,000-method case completed reports with all 32 surfaces and
  128 compiled variants. Run recorded 302.39 seconds and 883,343,360 maximum
  resident bytes versus the earlier 135.71 seconds/1,063,288,832 bytes: observed
  peak is lower, but run time is over twice as long. The 192,000-method case is
  running next. Capacity and throughput both require the terminal measurements;
  do not claim improved overall scale from compression or focused parity alone.
  The rebuilt ordinary CLI sample completed with 27 facts and
  Level1SemanticAnalysis at output/native-traversal-work-compressed-sample-smoke-20260928.
  Original stress inputs and PowerShell proof remain untouched.

## Sparse compiled-graph stress (2026-09-28)

- Added source for an explicit graph diagnostic profile: same 32/256 declared
  pages and exact-eight-times source bytes, plus 24,000/192,000 distinct sparse
  compiled methods. This exercises global inventory instead of multiplying only
  selected paths. Sparse generator parameters participate in the exact bounded
  input commitment; identical source alone cannot identify generated PE bytes.
- Diagnostic configuration explicitly declares two-million metadata work and
  one-million fact/500,000 edge/512 MiB text admission. Production defaults,
  graph search caps and the internal 512 MiB graph-store bound are unchanged.
  Scan admission verifies the exact unique sparse method name range from the
  read-only retained index. Successful graph admission must observe at least that
  many nodes and retain four branches per
  page. A graph-input refusal must classify zero paths and record partial state,
  observed counts and categorical gaps, not be relabeled full acceptance.
- Exact pushed 9797b6a3ceccf71631a864a8943161b7c0527b9b regression
  terminated green: 2,721 passed, zero failed/skipped in 13 minutes 59 seconds.
  Runtime binaries were rebuilt only after that process terminated. The rebuilt
  scale class passed 2/2, including the input-commitment fixture and ordinary
  1/8-page CLI smoke, with no introduced build warnings. The retained sparse
  stress run completed at output/native-scale-sparse-graph-20260928. No proof
  was deleted.
- The retained 24,000-method case has completed reports: 49,781 facts,
  49,228 graph nodes, 49,024 edges, all 32 pages and 128 compiled variants.
  Page/compiled traversal counters are 2,944/1,408. OS timing recorded
  135.69 seconds and 1,063,288,832 bytes maximum resident size for run.
  Coverage remains partial-static-review, not runtime or authentic ASP.NET
  acceptance.
- The 192,000-method case retained all 192,000 unique sparse methods and
  202,518 facts, with all 256 source surfaces. Graph admission hit the unchanged
  internal 512 MiB graph-storage budget (`graph-storage-bytes`), producing an
  explicit GraphInputLimitReached gap, zero graph nodes/edges and zero compiled
  paths. Run took 45,937 ms and peaked at 2,461,483,008 resident bytes; retained
  run disk was 2,469,452,155 bytes. This is fail-closed capacity evidence, not
  successful representative graph admission. Do not raise the ceiling silently.
- The two-case subprocess diagnostic passed 1/1 in 3 minutes 35 seconds. Its
  retained native-scale.receipt.json binds the exact test/CLI generators and
  corpus inputs. Both source/page and sparse-method counts are exact eight-times.
  The next scaling slice must address indexed graph storage amplification and
  retain graph-admission/storage observations; private acceptance remains open.

## Native graph-query work scope (2026-09-28)

- Inspection after 389f1a19 found an incorrect new status/documentation label:
  native packet and selected-symbol reporting each invokes one graph search over
  its selected roots. MaxPaths and MaxTraversalWork are shared across that query,
  not independently available to every root. Corrected status keys and docs;
  the earlier per-root wording below is superseded, not an acceptance claim.
- New optional path-summary traversalWorkUnits carries the search's existing
  actual counter, including terminal inventory and shortest-witness work. It does
  not change search limits, path selection, traversal order or retained evidence.
  Grouped projection retains it losslessly under existing generator/input hashes.
  Historical summaries omit the field and remain unknown without mutation.
- Native packet summary carries the page query's counter. Status retrieves both
  bounded summary counters and labels their sum's scope as
  page-and-compiled-graph-query-traversal-all-selected-roots. Graph input,
  phase elapsed/peak/transient usage remain explicit separate gaps. Regression
  source pins work exhaustion, no-terminal work and historical null/round-trip.
  No runtime binaries were rebuilt while exact 389f1a19 full regression is live.
  Final rebuild, regression and old/new retained-run smokes remain required.
- Exact pushed 389f1a195b8e3672667b7510cfbe609bcc9b0683 full regression
  terminated green: 2,719 passed, zero failed/skipped in 12 minutes 34 seconds.
  This includes the Git output-admission fix and original status implementation,
  not the subsequent query-scope/counter source edits. Runtime rebuilding began
  only after that process was terminal.
- Rebuilt query-scope/counter execution, indexed query, combined path, grouped
  handoff, packet and ordinary native-scale regression passed 218/218, zero
  failed/skipped, in 3 minutes 19 seconds with no introduced build warnings.
  Subsequent benchmark receipt counter assertions still require their own build.
- Actual new CLI status verified the old relocated 256-page/1,024-variant run,
  retained partial coverage and showed page/compiled work as unknown. Original
  checkpoint/artifact bytes were not rewritten. New status labels its two-million
  work cap per graph query, shared across selected roots.
- Final rebuilt native subprocess diagnostic passed 1/1 in 8 seconds and
  retained an explicit public 1/8-page receipt at
  output/native-traversal-work-smoke-20260928. The eight-page case retained 460
  facts/32 variants, page work 736 and compiled work 352, with independent
  two-million per-query caps. Actual CLI status verified that run and these
  counters with the original generator match, preserving partial coverage and
  eight cycle truncations. Workbench HTML also exposes the measured counters
  with their limitations. This is a smoke, not representative/private scale.
- Final ordinary CLI sample scan passed with 27 facts and
  Level1SemanticAnalysis. Explicit diagnostic/smoke outputs are retained locally
  and narrowly ignored by Git, not committed or deleted.
- Traversal accounting subtask is checked. Phase elapsed/peak/transient usage,
  graph/metadata/IL admission consumption and representative private graph scale
  remain open. No old proof, parent input or completed artifact was deleted or
  rewritten. Exact new committed-head full regression remains to be launched.

## Native operational status and Git output admission (2026-09-28)

- Exact-head full regression at 8d299bbe3500c30736e6a1e4e53026777413bf27
  terminated with 2,697 passed, one failed, zero skipped in 11 minutes 52 seconds.
  CSharpIdentityReceiverFixtureTests reported a generated repository scan commit
  of `unknown`. This is not a green full-regression result and is not reclassified
  as harmless test noise.
- Inspection found that GitMetadataProvider ignored the redirected-pipe drain
  timeout result and returned Failed=false with null output if an async read had
  not completed successfully. New source marks incomplete, faulted, cancelled
  or required-empty output as a failed probe so the existing single retry applies.
  Process timeout, output-drain timeout and retry count are unchanged; successful
  empty repository prefixes remain valid. Deterministic pending/fault/cancel/
  empty/trimmed-output unit tests pin this distinction. This admission gap is
  verified from code; it is a plausible contributor, not a proven root cause of
  the one observed fixture failure. Final rebuilt regression remains required.
- New native `status --run ... [--json]` source reads immutable retained history,
  verifies admitted artifacts and bounded indexed summary tokens, and gives
  observed phase/count/coverage/gap state, configured limits, missing declared
  input locators, the workbench path and a next action. It never scans, repairs,
  searches TEMP, reads full handoffs or approves cleanup. New machine output has
  exact current generator and bounded-input commitments and is local-only.
- Unknown work/time/peak/transient-disk consumption is explicitly unavailable,
  not zero. Aggregate variants are distinct from per-root path caps, and artifact
  bytes from renderer byte usage. Status lists at most 50 compiled gap kinds and
  64 checkpoint gaps with omission counts; lossless original artifacts remain.
- Source-only edits were made while the full regression remained live; no runtime
  binary rebuild occurred during that run. Added preflight, failed scan/report,
  completed, relocated, missing-input, tamper and busy-run status tests. Native
  Windows/private parity, full phase instrumentation and representative scale
  remain separate goal gates.
- Rebuilt broader native/Git/identity regression passed 224/224, zero failed or
  skipped in 3 minutes 36 seconds. The final scope/commit status fields and strict
  configuration admission then passed a rebuilt focused 37/37, zero failed or
  skipped in 18 seconds with no introduced build warnings. This includes the
  previously failing identity fixture but does not replace full regression.
- Actual final CLI status verified the relocated 256-page public run: fresh/all,
  14,100 facts, 1,024 variants/groups, 516 compiled gaps and 256 cycle truncations.
  It correctly retained partial coverage and reported a different reader
  generator without implying resume admission. The ordinary sample scan passed
  with 27 facts and Level1SemanticAnalysis. No proof was deleted.
- The bounded status subtask is checked; production usage instrumentation stays
  open. Native Claude evidence guidance now begins with status then bounded
  queries, not source access, whole-file reads or executing suggested resume
  actions. Exact committed-head full regression remains to be launched.

## Explicit completed-run copies and protect-only retention (2026-09-28)

- Exact pushed da2ce561b236b71a971277893df9514c5861ac95 full regression passed
  2,680/2,680, zero failed/skipped, in 12 minutes 17 seconds. Runtime binaries
  remained unchanged until that run terminated. This evidence covers the
  qualified member index, not the new relocation source below.
- Added native `relocate --run ... --out ...` for completed runs only. It copies
  admitted scan/report artifacts, original manifest/checkpoint chain and optional
  README into an explicitly new separate root. Existing artifact/checkpoint bytes
  are never rewritten. Source run locks, bounded streamed copy hashes, source
  rechecks and sibling-directory publication precede completion output. Failed
  staging and all original proof folders are retained, not deleted.
- New private location metadata pins current generator, bounded copy roster,
  original policy root, destination and original completed checkpoint/artifact
  anchor. History checks use the original root only as a lexical policy digest
  input; owned file access continues under the current physical run root.
  Two-hop copies carry the previous locator hash without depending on old-root
  availability for read-only queries. Completed execution resume retains its
  original generator/runtime and external-input validation gates.
- Added `retention-plan --run ...`: a hash-bound local protect-only dependency
  inventory on stdout. Every admitted artifact is rehashed, external roots/files
  and previous proof locations are protected declarations, and deletion
  candidates are always empty. Unknown files, abandoned attempts and other runs
  remain protected. This is not machine-wide cleanup safety or deletion approval.
- A rebuilt CLI copied the retained public 256-page diagnostic (1,024 paths) to
  `output/native-scale-relocated-256-20260928`, preserving the original run.
  Bounded compiled query succeeded from the new root, and retention planning
  returned 20 retained files, 1,044 protected dependencies and zero deletion
  candidates. Both new outputs carry current generator and bounded-input hashes.
  This observation precedes final rebuilt regression and is not private Windows
  parity, original-generator resume, runtime proof or complete-site acceptance.
- Initial execution regression passed 67/67 in 3 minutes 1 second. Broader
  rebuilt native execution/preflight/preparation/input validation/query/normal
  scale regression passed 199/199 in 2 minutes 55 seconds. Final rebuilt source
  adds physical admission of current owned scan/report paths separately from
  original lexical policy digests and two link-rejection tests; it passed 201/201,
  zero failed/skipped, in 3 minutes 10 seconds with no introduced build warnings.
  Windows junction cases still require the native Windows lane; portable tests
  do not establish that private platform gate.
- Final actual CLI performed a second copy at
  `output/native-scale-relocated-final-256-20260928`; bounded `/variants` retrieval
  exposes all 1,024 retained variants, independently of retrieval-slice omissions.
  Current-reader query and protect-only retention succeeded after rebuild.
  The ordinary checked-in modern sample scan passed with 27 facts and
  Level1SemanticAnalysis. No runtime SQL, private-site or cleanup claim is made.
- The completed-copy/retention implementation subtask is checked; broader
  dependency-aware cleanup, original runtime location retention, cross-platform
  private acceptance, phase budget guidance, representative graph scale and the
  final short private Windows operator workflow remain open. No proof was
  deleted and no PR/merge action occurred. Full regression for this new slice
  remains to be launched at its committed head.

## Qualified publish member candidate index (2026-09-28)

- Continued from pushed d8d9d77d while its exact-head full regression remained
  live; no runtime binaries have been rebuilt during that run. New source replaces
  the name-wide member candidate array with global name/assembly counts and
  qualified-type buckets. Only a safely framed simple declaring type narrows
  candidate work; opaque, nested, malformed or delimiter-bearing identity rows
  retain the historical predicate and name-wide work accounting. No row or
  overload/assembly competitor is deduplicated, and the 100,000 cap is unchanged.
- Source/page membership uses one complete typed file-key index rather than
  repeatedly deserializing all facts for every mapped page. Tests added for
  400 same-name types, duplicate same-type ambiguity, global name/assembly gap
  counts, case matching and opaque identity predicate parity. Native scale now
  explicitly rejects the previously observed member work-limit gap.
- Exact pushed d8d9d77dc85192040d61902d0912ac152f1e7c09 full regression passed
  2,665/2,665 with no failures or skips in 13 minutes 3 seconds. Build, focused
  regression for the final new source remains to be run at its committed head.
  Initial rebuilt member-index/messy-workspace/memory validation passed 111/111,
  no failures/skips, in 1 minute 10 seconds. One xUnit collection-size warning
  was repaired with Assert.Single before the final rebuild. The prior
  benchmark observations remain historical rather than being rewritten as an
  improvement claim. This slice does not finish all semantic-index allocation,
  private Windows acceptance, relocation or cleanup.
- Final rebuilt index/memory/native-scale regression passed 54/54 with no failures
  or skips in 4 minutes 39 seconds; the analyzer warning did not recur. The actual
  32/256-page CLI runs retained 128/1,024 paths and immutable source/published/
  completed-resume rosters. The larger case no longer has the member work-limit
  gap, and retains 8,192 graph edges versus the earlier 7,168. Both cases now
  expose cycle truncation. Run times were 7,363/236,527 ms and OS peak resident
  usage 219,250,688/386,105,344 bytes. Retained evidence plus run bytes were
  22,796,063/177,531,917. This single observation is not a speedup claim: larger
  execution took longer while restoring candidate edges withheld previously.
  Owned ignored output/native-scale-qualified-member-index-20260928 retains the
  exact generator/input receipt and phase diagnostics; no old proof was removed.
- Final normal 1/8-page native smoke also passed (1/1, 10 seconds). Actual CLI
  scan of checked-in samples/modern-sample completed with 27 facts and explicit
  Level1SemanticAnalysis in owned ignored
  output/native-receipt-set-smoke-qualified-members-d8d9d77d. This sample scan is
  not compiled-site or private Windows acceptance.
- Read-only next-slice audit: report policy hashes currently include physical
  scan/report paths (WebFormsReviewExecutionCommand.Reports.cs), so copying a
  completed run to a new root alone cannot pass report-context admission.
  Relocation needs an explicit hash-bound original/current locator contract,
  not rewritten checkpoint bytes or timestamp-based discovery. External source,
  published, receipt and parent inputs must remain verified dependencies.

## Native subprocess source-size scale diagnostic (2026-09-28)

- Added a public generated VB/PE/IL corpus and real-CLI prepare/preflight/run/
  completed-resume diagnostic. Normal smoke is 1/8 pages; explicit retained
  benchmark is 32/256 pages with 2,236,416/17,891,328 source bytes (exactly 8x).
  Each page has four cross-assembly terminal branches and an overload competitor.
  Tests restore the grouped handoff, verify every page/branch by exact symbol
  identity, and pin unchanged source/published bytes and resume output rosters.
- Initial prepare refused GraphMaxPaths=4096 at the former 256 ceiling. Native
  preflight now admits explicitly configured 1..4096 paths; default 256 and all
  depth/work/admission/output budgets remain unchanged. Boundary/default tests
  reject zero/4097 before output and retain all other budget values.
- Final focused benchmark plus preflight regression passed 42/42, no failures
  or skips, in 3 minutes 36 seconds without introduced build warnings. It retained
  1,780/14,100 facts, 128/1,024 compiled paths and 22,814,356/175,574,091 evidence
  plus run bytes. OS-measured run peaks were 225,771,520/439,500,800 resident bytes;
  run times 6,826/189,785 ms. This is not linear-throughput acceptance.
- Packet/compiled truncation were both true at 32 pages and false at 256 pages.
  The latter still retains ProjectlessPublishMemberWorkLimit: name-only candidate
  matching crosses its 100,000 work bound and withholds source member bridges.
  Therefore no truncation is not complete coverage. All four compiled branches
  per page remain retained; source-bridge completeness is not claimed. Next slice
  should index qualified member candidates while retaining every global competitor
  and the existing work cap, then rerun this diagnostic.
- Benchmark output is retained read-only in ignored owned
  output/native-scale-32-256-final-20260928. Exact test/CLI generator and bounded
  corpus input commitments are in native-scale.receipt.json. Generated IL is
  not aspnet_compiler acceptance; retained disk excludes transient graph/sorter
  peak. Private Windows graph scale, relocation/retention and proof replacement
  remain open; no proof dependencies or compatibility scripts were removed.

## Indexed admitted fact rows (2026-09-28)

- Continued from pushed `3a2c3dbd` while its exact-head full regression remained
  live; runtime binaries were not rebuilt during that run. Source changes stream
  each compact admitted fact into private transient storage before graph building
  rather than retaining a complete fact list. Exact combined-ID and source/
  original-ID dictionary facades fetch requested rows; identity-order enumeration
  uses the pinned .NET ordinal collation. No competitor or source row is pruned.
- Surface projection now wraps the fact list lazily rather than constructing a
  second complete projection-input array. Existing ordinary readers preserve
  their duplicate-ID behavior; canonical combined storage enforces exact source
  namespaces. New parity coverage retains repeated original IDs across sources.
  Fact storage shares the existing quota, generator/input header and lifetime.
- Initial build exposed a fixture-only `Repo`/`RepoName` typo, corrected before
  execution. First runtime parity passed but the unchanged allocation guard
  caught repeated lazy full scans: 137,966,552 allocated bytes versus 69,561,912
  in the historical reader. Indexed fact-type queries now deserialize only the
  requested rule inputs; they do not prune the retained fact set. The repaired
  memory suite passed 38/38. A dedicated isolated run recorded 61,297,520 versus
  69,577,304 allocated bytes while retaining all 1,005 rows. This is higher total
  allocation than the previous compact-list design, not proof of peak-memory or
  end-to-end throughput improvement.
- Broader native/compiled/grouped/combined-report regression passed 605/605,
  zero failed/skipped, in 4 minutes 14 seconds before the final source-key/PDB
  lookup facade and per-source-copy removal. Final rebuilt memory plus compiled
  messy-workspace regression passed 96/96, zero failed/skipped, in 45 seconds,
  without introduced compiler/analyzer warnings. A final dedicated allocation
  rerun recorded 51,914,128 versus 69,576,840 bytes, retaining all 1,005 facts.
  The actual CLI sample scan completed with 201 facts and explicit reduced
  semantic coverage in ignored owned `output/native-receipt-set-smoke-indexed-facts-3a2c3dbd`.
  Exact-head full regression at `2f190445b54dbac7649c45fbf22d8192ae6f8916` passed
  2,659/2,659, zero failed/skipped, in 11 minutes 4 seconds. This is slower than
  the preceding adjacency slice's full suite; throughput is not inferred improved.
  Type-specific compiled/dispatch arrays, reconciliation alias
  groups and retained output still allocate managed memory. This is not a whole-
  workflow peak-memory or representative eight-times scale acceptance claim.

## Paged outgoing adjacency (2026-09-28)

- Continued from exact tested `3007644d` without rebuilding during its full suite.
  Outgoing adjacency is now an indexed read-only list backed by a dense global/
  local order roster. Each random-access page loads at most 64 records, targeting
  512 KiB serialized payload; one already-admitted oversized record is retained
  alone. Reverse traversal wraps the list rather than allocating a full array.
  The roster is included in the existing logical storage quota; construction is
  frozen after sorting, and incomplete positions fail closed.
- Focused memory/parity suite passed 37/37, zero failed/skipped, in 7 seconds.
  Dedicated 128/1,024-branch cases passed 4/4, retaining all 129/1,025 paths with
  complete historical JSON parity in legacy depth-first and ordinary breadth-
  first traversal. Maximum outgoing rows loaded was 64. The 1,024-branch cases
  used 6,496,256/6,488,064 logical database bytes and took 4 seconds/388 ms in the
  dedicated detailed run. These are fixture measurements, not source/compiled
  end-to-end peak-memory or representative eight-times scale acceptance.
- Broader native regression passed 582/582, zero failed/skipped, in 3 minutes
  57 seconds. Actual CLI scan of `samples/vb-webforms-sample` completed with 201
  facts and explicit `Level1SemanticAnalysisReduced` coverage in ignored owned
  `output/native-receipt-set-smoke-paged-3007644d`. Global facts,
  semantic indexes and adversarial alias groups still use managed memory. No
  existing proof folder was written or deleted; private Windows, phase usage,
  relocation/retention and representative scale gates remain active goal work.
- Exact pushed-head full regression at `3a2c3dbd` passed 2,658/2,658, zero failed
  or skipped, in 8 minutes 50 seconds. It covered the paged adjacency slice, not
  the subsequently uncommitted indexed fact changes.

## Indexed bounded combined graph (2026-09-28)

- Continued on `codex/webforms-native-preflight` based on `6562aa86`, whose exact
  committed runtime passed 2,648/2,648 full regression tests in 10 minutes 55
  seconds. Source changes below were not rebuilt during that live regression.
- Bounded combined reports now construct nodes/edges directly in a private
  temporary SQLite graph, rather than constructing a complete managed graph and
  spilling it afterward. Exact node/edge IDs remain authoritative. Complete
  source-namespaced alias membership is indexed and one alias group is loaded at
  a time; outgoing adjacency is fetched by exact node ID. Dispatch receives a
  lazy exact-node facade, preserving all competitors and ordinal enumeration.
- Scratch storage records actual Reporting DLL/input-index SHA-256 and effective
  storage quota, uses an 8 MiB page cache and defaults to a 512 MiB logical
  database ceiling. The input is rehashed before returning a report. SQLite owns
  the temporary file; it is never an emitted/reusable run artifact. Input/proof
  files are not written or deleted. Storage refusal discards every partial path.
- Initial focused validation exposed incomplete scratch schema initialization
  under an artificially tiny quota. Setup now executes/validates each schema
  statement, refuses insufficient header budgets and tests actual graph growth
  refusal with a 64 KiB quota. The repaired memory/parity suite passed 30/30.
- Complete serialized report parity passed for 32/256 public synthetic pages,
  retaining 224/1,792 nodes and 96/768 edges. Logical storage was 487,424/3,534,848
  bytes; maximum outgoing rows loaded was one in both cases. This is an 8x page
  ratio, not representative source/compiled-distribution or peak-memory proof.
  The wider native suite passed 575/575 in 4 minutes 4 seconds before the final
  branch-order/cancellation tests and metadata-budget field. After those final
  changes, the rebuilt focused suite passed 90/90 in 39 seconds, zero failed or
  skipped, without introduced compiler/analyzer warnings. Exact-head full
  regression at `3007644d51cdbe7f2cc965c5effa71be407b4f1b` passed 2,654/2,654,
  zero failed or skipped, in 8 minutes 52 seconds. Its actual CLI sample scan
  completed with 136 facts in ignored owned `output/native-receipt-set-smoke-3007644d`.
- Global admitted fact rows and compiled/dispatch semantic indexes remain
  managed, and one adversarial alias or outgoing group can still have high
  fan-out. Sorter scratch files are outside the logical database ceiling. Total
  disk/time/peak-memory benchmarks, effective phase usage reporting, source/graph
  allocation completion, real Windows parity and retention/relocation remain
  active goal work. The overall indexed-graph/scale task remains unchecked.
- Final source-admission review found that a canonical fact namespace with no
  source metadata could be skipped by per-source projection. It now fails with
  `COMBINED_FACT_SOURCE_UNAVAILABLE`; a regression pins unchanged input bytes and
  prevents silently removing a global competitor. Storage refusal also retains
  the already-read source metadata. Final rebuilt focused validation passed
  91/91 in 36 seconds, zero failed or skipped, after these guards. Exact-head
  full regression for the indexed slice passed as recorded above.
- The isolated warmed allocation fixture passes with the indexed backend:
  69,309,224 full-reader bytes versus 14,753,680 indexed compact-reader bytes,
  retaining all 1,005 facts. This is more total allocation than the earlier
  in-memory compact reader because indexed lookups deserialize records; no
  peak-memory or end-to-end performance improvement is inferred from it.

## Combined graph property allocation runway (2026-09-28)

- Continued on `codex/webforms-native-preflight` based on `c06a406e` after its
  exact committed runtime passed the full 2,637-test regression. Bounded combined
  reads now use a connection-local SQLite property projection, retaining every
  fact row, exact source namespaces, competitors and attachment context. Input
  indexes and full facts remain immutable; raw admission ceilings are unchanged.
- Per-row and aggregate fact/edge text admission occurs before copying row
  strings into managed memory. Noncanonical fact namespaces are rejected rather
  than reconstructed into invented evidence IDs. The bounded connection uses
  query-only access, file-backed temporary operations and an 8 MiB page cache.
- An initial focused run exposed four compiled-chain regressions from omitted
  source symbol IDs and VB body/signature context. Expectations were not weakened:
  the shared projection was expanded to retain the fields needed by compiled
  bridges. The same focused cases then passed 78/78 in 39 seconds. After adding
  legacy-column and malformed/duplicate property compatibility cases, the final
  focused suite passed 85/85 in 38 seconds. The wider native regression passed
  572/572 in 3 minutes 53 seconds. After the final legacy/declared-surface
  projection guard, the rebuilt focused suite passed 85/85 in 49 seconds, zero
  failed or skipped. Exact-head full regression at
  `6562aa86809a36246e7ed1428c26179d3bd86997` passed 2,648/2,648, zero failed or
  skipped, in 10 minutes 55 seconds. Its actual CLI sample scan also completed
  with 136 facts in ignored owned `output/native-receipt-set-smoke-6562aa86`.
- An isolated warmed 1,005-row public fixture retained identical complete path
  JSON while allocating 69,237,792 managed bytes with the full reader versus
  4,849,728 with the final compact reader; retained projected text was 236,491 bytes.
  This measures allocation only, not process peak memory or representative scale.
- Flat string/null property validation prevents duplicate JSON key or non-string
  coercion from inventing identity metadata. The combined reader retains its
  existing last-key/empty-malformed-properties semantics; single-index malformed
  property hashes remain unchanged. The real CLI sample scan completed with
  136 facts in ignored owned `output/native-receipt-set-smoke-compact-c06a406e`.
- This is a measured allocation runway, not completion of the indexed graph
  requirement: the global graph still materializes and source/compiled eight-times
  disk/time/peak-memory validation, public Windows parity and retention remain open.

## Native partition preparation and pinning (2026-09-28)

- Continued on `codex/webforms-native-preflight` in the managed attachment
  checkout, based on `b093b98c`. Native preparation now emits deterministic
  bounded page and inventory partitions and validates exact global source,
  published-file and page counts through Core before publishing an evidence root.
- Preflight and resume pin every partition. A separately opted-in publication
  inventory budget preserves old flat admission when omitted and leaves compiled
  metadata limits unchanged. Follow-on input count and actual hash-byte refusal occur before the
  final evidence directory is published; failed staging remains unadmitted.
- Four public integrated cases cover selected/all-page fresh and immutable
  attachment workflows with 67 declared pages, 368 source files and 69 published
  files. They retain original source/published/parent hashes, exact generator and
  bounded-input commitments, checkpointed reports and lossless partition handoff.
  Completed resume succeeds; changed partition bytes cannot create a checkpoint.
- Existing larger-inventory refusal tests now assert mapless/metadata admission
  rather than obsolete single-receipt count caps. Partitioning does not bypass
  binary admission or turn declared page mappings into compilation proof.
- Final focused preparation/preflight/receipt-set validation passed 97/97, zero
  failed or skipped, in 1 minute 14 seconds with no introduced build warnings.
  This includes the follow-on actual hash-byte budget guard. Before that final
  additive guard, the broader Web Forms/attachment/grouped suite passed 502/502
  in 2 minutes 53 seconds. A real CLI syntax-only public sample scan completed
  with 136 facts in the ignored owned output
  `output/native-receipt-set-smoke-native-b093b98c`.
  Exact-head full regression at `c06a406e4d7e50763c239eca746d342f74bdc6cb`
  passed 2,637/2,637, zero failed or skipped, in 11 minutes 21 seconds. The CLI
  sample at that head also completed with 136 facts in the ignored owned
  `output/native-receipt-set-smoke-c06a406e`. Public
  Windows compilation parity, representative eight-times scale, bounded global
  graph processing, legacy presentation parity and retention remain open. No
  private proof dependencies were deleted and no PR or merge was performed.

## Publish receipt partition contract (2026-09-28)

- Continued on `codex/webforms-native-preflight` based on `199ee4a2`.
  That exact committed native retrieval/reporting runtime passed the full .NET
  regression: 2,602/2,602, zero failures/skips, 10 minutes 17 seconds. This pass
  preceded the receipt-set changes below and does not validate those changes.
- The existing Core publish option now admits a private
  `webforms-publish-binding-set.v1` container with exact generator, source commit,
  ordered partition SHA-256 roster and bounded input framing. Each of at most
  64 non-nested partitions retains the legacy 1 MiB/256 source/64 published/32
  page bounds. Conservative repeated artifact read/hash admission is capped at
  8 GiB and retained map virtual paths at 4,096 characters.
- Admission is all-or-none. Exact repeated source/assembly membership is
  coalesced; case aliases, conflicting rows, repeated pages and cross-partition
  mapped/mapless ambiguity withhold every binding. Source/published and receipt
  bytes are rechecked before success. Facts retain the established rule, tiers,
  generator/input commitments and review-only limitations.
- Explicit inventory-only partitions carry additional nonempty source/published
  inventories with zero page bindings and unchanged byte/count caps. They are
  valid only inside a set with normal page-bearing receipts, never standalone
  publish proof. This supports shared-inventory chunking without duplicating
  pages to satisfy per-receipt count limits.
- Initial focused receipt-set, legacy root and preparation validation passed
  51/51; broad Web Forms/attachment/grouping regression passed 486/486 (3 minutes
  15 seconds), before inventory-only support. Final focused checks after that
  addition passed 54/54 (14 seconds), zero failures/skips, with no introduced
  build warnings/errors.
  Public fixtures cover 67 pages across three partitions, stable materialized
  facts, unchanged input bytes, malformed/tampered/nested/duplicate receipts,
  global map ambiguity, cancellation, inventory-only context without fake pages,
  and oversize file/identity refusal.
- A real CLI syntax-only scan of public `vb-publish-projectless` completed with
  136 facts. Its owned local output is retained under
  `output/native-receipt-set-smoke-199ee4a2` and ignored, not published. The final
  CLI smoke after inventory-only support also passed with 136 facts under
  `output/native-receipt-set-smoke-final-199ee4a2`. Final broad Web Forms,
  attachment and grouping regression passed 489/489, zero failures/skips,
  3 minutes 18 seconds, after inventory-only support.
- Native partition preparation and preflight pinning remain the next required
  integration. This Core reader is not an all-page native workflow, compiled-site
  parity, representative scale or private Windows acceptance claim. No existing
  proof, wrappers, inputs or dependencies were deleted; no PR or merge occurred.

## Bounded native evidence retrieval and Claude guidance (2026-09-28)

- Continued on `codex/webforms-native-preflight` in the managed attachment
  checkout at `fb373ac5`. Native report completion now also pins a private
  `review-evidence.sqlite`, built by streaming both exact handoff JSON documents
  into a lossless ordered token tree. Metadata pins actual CLI generator,
  input hashes, run ID, counts and parser/storage bounds. Original JSON and
  PowerShell proof remain unchanged; no source or DLL bytes are copied.
- `webforms-review query --run` consumes only a completed owned journal/index.
  It never reads source/published/parent inputs or whole handoff documents,
  never repairs missing older indexes, and emits one bounded private JSON slice
  with generator/run/checkpoint/index/input commitments. JSON Pointer, pagination
  and depth are closed selectors. Omitted child counts and cursors are explicit;
  retrieval omissions cannot upgrade/downgrade retained page verdicts or coverage.
- Responses cap at 128 KiB/2,048 nodes; retained scalar/pointer bytes are checked
  before response deserialization, and oversize results emit no partial stdout.
  Indexing caps at 2,000,000 values, 64 levels, 4,096-character property names and
  a 1 MiB pending token buffer; SQLite and aggregate JSON input retain existing
  configured byte caps. Failed report/index bounds retain safe categorical codes
  in the journal, never raw exception text.
- The checked-in Claude prompt and agent handoff now distinguish native query
  review from the legacy PowerShell artifact/grant/session contract. Native
  launching is not implemented by query; no model/API call or source grant was
  added. All query output remains private, not shareable.
- Initial execution/query checks passed 66/66; expanded grouped/execution/query
  checks passed 90/90 with zero failures/skips. Broader review, attachment,
  snapshot, grouping and messy-workspace regression passed 322/322 (2 minutes
  18 seconds). After the final locator-first scalar loading and stdout byte-cap
  refinements, execution/query checks passed 67/67 (1 minute 43 seconds), with
  zero failures/skips and no introduced compiler/analyzer warnings.
- Public-fixture Playwright desktop/mobile screenshots were inspected. The
  bounded query guidance is visible, viewport/document both measure 390 pixels
  on mobile, and the browser reported no console errors or warnings. Browser
  and localhost server were closed. Representative scale and the real Windows
  compiled-site/work-machine gate remain unverified.
- Whole-index hash verification is streamed before and after each query; this
  is not constant-time retrieval. All-page receipt partitioning, global graph
  memory redesign, retention/relocation and private acceptance remain active goal
  work. No wrapper retirement, proof cleanup, PR creation or merge occurred.

## Native report execution and entry workbench (2026-09-28)

- Continued on `codex/webforms-native-preflight` in the managed
  `webforms-native-attachment` checkout, based on `8762069c`.
- Operator run/resume now continues beyond its immutable scan checkpoint into
  explicit combine, the existing full page packet and a separate grouped compiled
  supplement. Native entry HTML and root JSON retain original page-chain verdicts,
  full packet records, source/published/receipt locations, exact scan manifests,
  DLL/input byte hashes and CLI/Reporting generator commitments. No snippets or
  source rescan occur in this phase. Attachment uses the admitted explicit link.
- Selected handlers use one bounded graph admission and exact source-index,
  scan, commit and full symbol roots. Empty/missing selectors never fall back to
  unrelated roots; global ambiguity remains intact. The graph still materializes
  after admission, so this is not the eight-times memory gate.
- Reports share the contiguous hash-chained run journal. Started/failed/cancelled
  phases reference the completed scan checkpoint; retries allocate fresh report
  IDs. Producer-calculated hashes must match independently collected bytes before
  completion. Completed resume revalidates all admitted output without rendering
  or scanning again. Partial failures remain on disk, unadmitted.
- Broader focused validation passed 303/303, zero failed/skipped (2 minutes
  10 seconds), before the final event-fixture/browser-layout adjustments. Final
  exact-slice validation is recorded below after those adjustments.
- Final execution plus source/compiled bridge parity passed 55/55, zero
  failed/skipped (1 minute 20 seconds); after the mobile wrapping repair, the
  no-build repeat passed 55/55 (1 minute 35 seconds). Builds introduced no compiler
  or analyzer warnings. The real CLI fixture retains one control/event chain,
  requests its exact handler root and verifies the root packet canonical digest.
- Playwright desktop/mobile QA used only the public execution fixture. Expanded
  long provenance paths initially overflowed mobile; `overflow-wrap:anywhere`
  repaired it, with viewport/document both 390 pixels. Screenshots were inspected;
  grouped HTML and native JSON navigation worked. HTML had no console errors;
  Chromium's raw JSON viewer requested an absent favicon (404 only), not an
  application failure. Browser and localhost server were closed. The disposable
  ignored preview harness needed its test EF dependency explicitly pinned to avoid
  harness-only dependency conflicts; no production dependency change was made.
- All-page receipt partitioning, complete legacy presentation parity, bounded
  Claude retrieval, durable relocation/retention, representative scale, full
  regression and the authorized real Windows compiled-site run remain gates.
  No proof deletion, wrapper retirement, PR creation or merge occurred.

## Native immutable attachment execution (2026-09-28)

- Continued on `codex/webforms-native-preflight` in the managed
  `webforms-native-attachment` checkout, based on `c7bdb328`.
- Native run/resume now executes an explicit attach configuration without the
  source scanner. It independently validates the immutable original parent,
  retained source bytes and compiled admission, then writes a separately owned
  standard scan through the Core compiled-only producer. Exact parent/context
  provenance is visible in its manifest and Markdown report.
- Derived manifest/index/NDJSON consistency is checked before completion.
  Completed resume verifies parent/source/output/context without re-extraction;
  failed/cancelled attempts remain unadmitted and retries allocate fresh IDs.
  Parent SQLite sidecars and post-extraction input changes reject admission.
- Focused validation passed 133/133, zero failed/skipped, covering execution,
  compiled production, input gates and source snapshot regressions. New cases
  cover legacy/complete-roster parents, source-scan absence, cancellation,
  failure/retry, parent/source/context tampering and completed-output tampering.
- This is not cross-index join or unified report acceptance. Those phases,
  compiled-site parity, all-pages partitioning, scale, retention and the final
  authorized private Windows run remain outstanding. No proof cleanup or PR
  merge occurred. Earlier full-suite results do not validate this later slice.
- Full regression of this slice at `fd13069c` subsequently passed 2,528/2,528,
  zero failed/skipped (9 minutes 54 seconds), with no compiler/analyzer warnings.
  This result precedes the following attachment-combine contract changes.

## Explicit compiled attachment combination contract (2026-09-28)

- Added an opt-in .NET combine contract with explicit parent/attachment index
  and manifest paths. Ordinary combines infer no links from labels, commit names
  or adjacent files. Native reporting is not wired to this API yet.
- Admission pins actual bounded bytes, compares complete external and embedded
  manifests, validates compiled-only context and exact parent content/snapshot/
  repository identity, and rejects sidecars or symbolic input paths. Inputs use
  immutable SQLite URIs. Only a fresh output can be allocated for this operation.
- The additive link table records actual Combine generator and bounded-input
  hashes plus original source IDs, scan IDs and artifact/context hashes. Parent
  fact IDs/namespaces remain unchanged. This is local integrity, not authenticity
  or source-to-compiled edge proof. Complete fact/NDJSON and retained-source
  admission remains the separate native parent gate's responsibility.
- Focused regression passed 85/85, zero failed/skipped (54 seconds), across
  combine, attachment production and native execution. Cases cover exact hashes,
  unchanged input bytes, original fact IDs, escaped URI paths, wrong parent/index
  manifests, duplicate/missing contracts, sidecars, role/aggregate byte budgets,
  pre-cancellation, existing output preservation and absence of implicit links.
  No compiler/analyzer warnings were emitted; `git diff --check` passed.
- An initial fixture hit macOS's temporary-path alias; it now uses the physical
  path required by the contract. SQLite ATTACH needed the owned main connection
  opened in URI mode; the positive escaped-path regression pins that behavior.
  A reused index locator must still pass a smaller manifest-role limit; streamed
  hashing and fixed-size manifest reads reject growth instead of allocating it.
- Reporting consumption, per-edge attachment context, native report checkpoints
  and public method-chain parity are next. This is not final workflow acceptance;
  the 2,528-test full result above belongs to `fd13069c`, before this contract.

## Reporting consumption of explicit attachment links (2026-09-28)

- Reporting now validates stored link payload, exact embedded manifest hashes,
  attachment context and source-row identity before selecting a retained parent
  namespace. Changed links, manifests, source metadata or oversized payloads
  refuse reporting; earlier draft links lacking embedded-manifest hashes require
  explicit recombination instead of a silent upgrade.
- Existing VB PDB and publish graph bridges can use parent declarations/pages/
  handlers while metadata/IL facts stay in the attachment index. Original rule,
  tier, checksum, signature and ambiguity requirements remain unchanged. Parent
  facts are never copied or relabeled. Each cross-index bridge carries its link
  digest; local path JSON retains complete link context and a review-only
  limitation. Reports with no link retain the previous optional-field shape.
- Public tests compare complete terminal method-chain display, edge kind, rule,
  tier, span and classification keys against single-index baselines for PDB and
  publish fixtures. They check both original supporting namespaces, absence of
  guessed joins without a link, withheld duplicate declarations, bidirectional
  receipt member candidates and post-combine tamper rejection.
- Broader focused regression passed 231/231, zero failed/skipped (1 minute
  28 seconds), across native execution, producer/combine, messy workspace and
  dependency path/report suites. No compiler/analyzer warnings were emitted.
  Final targeted validation, including the subsequent explicit draft-link
  recombination-required case, passed 27/27, zero failed/skipped (6 seconds),
  with no compiler/analyzer warnings. `git diff --check` passed.
- Synthetic fixture binding/compiler declarations are test evidence only, not
  build-authenticity or private Windows acceptance. New semantic source
  reconciliation was not added. Complete native report phase execution,
  workbench/grouped lossless JSON, all-pages partitioning, compiled-site parity,
  bounded scale, retention and final authorized Windows validation remain open.
- Next implementation should extend native checkpoints beyond scan completion
  with separately owned, retryable report attempts; consume the explicit link
  API for attachment and ordinary same-index evidence for fresh runs. Grouping
  must index all exact evidence variants instead of deleting them. Report input,
  output and generator bytes must be pinned before workbench admission; current
  `CrossIndexParentJoinsPending` remains until that orchestration actually runs.

## Grouped lossless native report projection (2026-09-28)

- Added the private .NET `GroupedCompiledPathHandoffBuilder` API. It groups
  ordered full method/source/scan/commit identities, not shortened labels, and
  indexes exact node/edge content while retaining every original ordered path
  variant, inventory occurrence, header, gap and attachment-link record.
- Restoration validates bounded context, exact record references, original
  canonical report digest and complete chain membership. Differing records
  with the same node/edge ID remain distinct; overloads and source identities
  never collapse. A synthetic 41-variant/13-chain shape test is explicitly
  public regression evidence, not a substitute for the private Windows proof.
- The handoff records the exact Reporting DLL SHA-256 and canonical admitted
  report/index/generator/limit input digest. Actual index admission remains
  the caller's responsibility. All private fingerprints stay local; no
  shareable projection or authenticated provenance claim is introduced.
- A new fixed-name private HTML/JSON writer validates the lossless input before
  allocation, refuses existing outputs and bounds aggregate output bytes.
  It returns exact output hashes/bytes; partial failures remain unadmitted.
  The HTML shows one chain with expandable identities, transition evidence
  and all original variants. Normal schemas, page verdicts and scripts are
  unchanged. Native report checkpoints/workbench integration remain pending.
- Broader focused regression passed 227/227, zero failed/skipped (1 minute
  29 seconds), across grouped projection, public messy-workspace parity,
  explicit combine, path/report and native execution. No compiler/analyzer
  warnings were emitted. Tests include exact JSON round trips, changed/extra/
  missing records, chain/context tampering, explicit budgets, cancellation,
  stale generator refusal, output preservation and HTML escaping/anchors.
- Final targeted regression, after the browser-driven responsive refinements,
  passed 28/28, zero failed/skipped (7 seconds), with no compiler/analyzer
  warnings. `git diff --check` passed.
- Browser QA uses only the disposable public regression fixture. Desktop
  expanded-chain inspection is readable. Playwright inspection exposed cramped
  mobile columns; these became stacked labeled evidence rows. At 390-pixel
  mobile width the document width remains 390 pixels. Variant-to-evidence
  navigation and the JSON link work; the HTML reports zero console errors/
  warnings after replacing its favicon-only 404 with an inline empty icon.
  Chrome's separate raw JSON viewer requested its own missing favicon after
  the successful JSON response; that cosmetic viewer request is not hidden.
- Next native integration must pin the complete scan manifests and explicit
  binding/publish receipts alongside this indexed path view, expose admitted
  DLL byte provenance, and support bounded fact lookup against the exact index
  hash. The path projection does not replace those admission artifacts or
  claim independent source/DLL authenticity.
- This is still a reporting API slice, not completed native workflow, full
  suite at this new head, Windows compiled-site or eight-times scale acceptance.
  No private proof folders were read/deleted and no PR was opened/merged.

## Active native workflow goal (2026-09-28)

- Owner requested the complete workflow as an active tracked goal after the
  preflight slice. Continue on `codex/webforms-native-preflight`; preserve the
  working PowerShell path and its external private proof dependencies.
- Delivery order: authoritative source/parent/compiled validation; durable
  resumable fresh/attach execution; normal workbench and grouped compiled-path
  navigation with lossless indexed JSON; bounded Claude guidance; public parity
  and representative scale/retention validation; short Windows operator run.
- Reuse `ManagedMetadataExtractor.Evaluate` semantics through a narrow Core
  API rather than duplicating binding classification in CLI orchestration.
  Its receipt-based `bound` state is not build/authenticity/runtime proof.
  Unbound, stale, dependency and reader-disagreement gaps remain explicit.
- Parent attachment must validate retained source/index identity and hashes
  before producing a new derived index; never append to the original scan.
- The final public acceptance denominator includes selected/all-page fixtures,
  projectless and project-based sources, fresh/attach/resume/tamper cases,
  equivalent retained method-chain/evidence identities, and bounded scale
  metrics. Counts alone are not equivalence proof.
- Keep private paths/data local. No real work-machine run has been performed
  by this coordinator. The owner will receive a short command for authorized
  Windows validation once the complete public-fixture workflow is ready.
- Cleanup starts with dependency-aware dry-run guidance only. No proof deletion,
  wrapper retirement, PR merge or source mutation is authorized by this goal.
- Goal is active, not achieved. Preflight tests establish only the first slice;
  subsequent implementation and final private validation remain outstanding.

## Explicit published root and retained publish receipts (2026-09-28)

- Continued on the same `codex/webforms-native-preflight` branch in the managed
  `webforms-native-publish` checkout after the old checkout's full regression
  completed. The old checkout is detached at its validated `33cd362a` head;
  no proof dependencies or other user lanes were removed or reused.
- Normal scan options now permit a separately explicit published-file root for
  an existing receipt. Default receipt-directory behavior and digest remain
  unchanged. Explicit root identity joins receipt bytes in the bounded publish
  digest; the source/published roots stay read-only.
- Native config accepts an optional receipt path. Preflight pins its source
  membership bytes and validates commit/page coverage plus exact DLL/map
  declarations and hashes. Scanning uses the existing Core map/receipt policy;
  malformed semantic content retains gaps rather than fabricated joins.
- Broader focused validation passed 217/217 across native preflight/input/execution,
  published-root, Core metadata, CLI, local-review, scan execution receipt and
  messy-workspace regressions. There were no
  compiler/analyzer warnings. This is retained-receipt consumption, not native
  receipt preparation, operator attestation, immutable attachment or complete
  compiled-site/report parity. Those remain active goal requirements.
- Full regression at committed `a9febf12` passed 2,418/2,418, zero failed/skipped
  (7 minutes), before the subsequent optional receipt-root change. No compiler
  or analyzer warnings were emitted.

## Separate native receipt evidence root (2026-09-28)

- Native config can explicitly locate binding/publish receipts outside the
  read-only published site. Legacy configs still use the published root.
  Assembly, page-map and PDB paths continue to use the published root only.
- The receipt root is an existing absolute physically resolved input directory;
  preflight/execution reject output overlap, escaped receipt locators and changed
  or missing receipt bytes. Execution consumes receipts in place, never copies
  them or DLLs and does not create an attestation.
- This is the input boundary needed for future owned receipt preparation, not
  receipt generation or a claim that preparation/attachment/reports are complete.
- Focused validation passed 171/171 across native preflight/input/execution,
  published-root, Core managed metadata, CLI, local-review and scan receipt
  regressions, with no compiler/analyzer warnings. New cases cover separate
  receipt inventory, output overlap, relative/missing/escaped roots and actual
  fresh/resume receipt consumption/tampering. An initial test expectation used
  the macOS `/var` alias rather than the resolved physical path; it was corrected
  before the passing rebuild. Diff and private-path guards passed.
- The prior committed full-suite result does not validate this later slice;
  full regression and public compiled-site parity remain separate gates.

## Native operator-declared preparation (2026-09-28)

- Added `webforms-review prepare --config <private-json> --out <new-evidence-root>
  --attest-exact-source-commit <commit>`. The exact configured source commit must
  be explicitly attested; this command never asks a model to infer or create
  owner authority. Public fixture declarations are synthetic tests only.
- Config declares a bounded `publishSourceRelativePaths` roster. Selected-page
  markup is also pinned; all mode covers only declared markup and retains a
  completeness gap. Git checks require committed membership and scoped-clean
  source with optional index writes disabled, bounded output and a timeout.
  Declared file bytes are independently compared with committed blobs, including
  Windows case aliases and explicit built-in CRLF normalization; assume-unchanged
  source edits cannot hide behind Git status. Custom clean/encoding transforms
  are rejected on mismatches. Per-source blob/comparison/policy evidence joins
  the preparation bounded input and is checked again before output admission.
- Metadata is inspected using the shared Core policy without an expensive source
  scan. Only uniquely admitted primary bytes receive operator-declared bindings;
  dependencies remain unbound context. Both generated binding and publish
  receipts pass independent Core inspection before output admission.
- The new evidence root owns only pinned receipts, a preparation manifest and a
  follow-on config using its separate receipt root. Exact CLI/Core generator and
  actual bounded input hashes are retained. Source, published bytes, parent
  scans and working PowerShell proof remain untouched; no binary copy occurs.
- Known compiler provenance stays unavailable using the established marker,
  never a fabricated compiler attribution. Mapless candidates are relative to
  the declared inventory, not physical publish completeness. Existing 256-source,
  64-published, 32-page receipt caps are explicit; automatic partitioning remains
  required future work rather than a silently widened or all-pages claim.
- Final focused validation passed 250/250, zero failed/skipped, across native
  preflight/input/execution/preparation, publish-root, managed metadata, CLI,
  local-review, scan receipts and messy-workspace regressions (1 minute 12
  seconds). Preparation has 26 cases including source-blob mismatches hidden by
  assume-unchanged, clean CRLF normalization across a buffer boundary, unsupported
  transforms, nested roots, typed budgets and actual prepare/preflight/run.
  The initial CRLF fixture changed attributes without renormalizing its index;
  Git correctly reported it dirty. The fixture now models a clean normalized
  checkout before exercising preparation. Builds emitted no compiler/analyzer
  warnings; diff and private-path guards passed. Full regression at committed
  `372f79b2` passed 2,451/2,451, zero failed/skipped (7 minutes 17 seconds).
  The source/DLL fixture checks
  receipt policy and native prepare/preflight/run plumbing; it is not compiled
  handler-chain parity, an eight-times benchmark or private Windows evidence.
- Native immutable attachment, unified/grouped/indexed reports, bounded Claude
  guidance, all-pages partitioning, scale/retention and final authorized private
  Windows validation remain active goal requirements. No PR or merge occurred.

## Retained-source snapshot admission gate (2026-09-28)

- Added a shared Core ordered-stream snapshot inspection API using exactly the
  existing scanner's framed path/kind/size/raw-byte digest. The original scanner
  framing and snapshot identity remain unchanged; cancellation is now checked
  during each file's chunked hashing, not only between files.
- Native attachment input validation, after full immutable parent/index/NDJSON
  checks, streams retained FileInventoried membership and requires current bytes
  to reproduce the original source snapshot. It repeats this check around the
  final input rehash. The source state distinguishes verified retained snapshot
  scope from fresh-scan pending state; new unretained files are not discovered or
  included. Clean Git/build/runtime/all-pages/source-line claims are not made.
- No full managed inventory set is constructed. SQLite uses an ordinal collation,
  file-backed temporary sorting and bounded caches; file count and raw hash bytes
  are admitted under the existing parent fact and remaining hash-byte bounds.
  Unsafe, duplicate, linked, missing, size-changed and same-size byte-changed
  members fail. A real project-scoped fixture confirms extra semantic metadata
  can belong to the original snapshot without FileInventoried rows; it fails
  with an explicit mismatch-or-incomplete-inventory reason, never guessed members.
- Final broader focused validation passed 169/169, zero failed/skipped (56 seconds),
  across snapshot inspector, parent/input, native preflight/execution/preparation
  and scanner regressions. The first build caught a test-only missing async lambda;
  a subsequent fixture used an unsupported folder option and was corrected to
  actual project selection. Both were corrected before the passing run; no
  production guard was weakened. Final cases also pin SQLite roster ordering to
  the scanner's UTF-16 ordinal ordering rather than SQLite UTF-8 binary ordering;
  a supplementary/Private Use Unicode filename fixture passes exact snapshot
  equality without parent mutation. The ordering-only second recheck passed
  43/43 before this final broader run. Builds emitted no compiler/analyzer
  warnings. Full regression at the new committed head remains a separate gate.
- Full regression at committed `758b61ea` subsequently passed 2,472/2,472,
  zero failed/skipped (7 minutes 10 seconds), before the retention slice below.
- This advances authoritative attachment input validation, not attachment
  production. At that milestone complete snapshot-membership retention for future source scans,
  compiled-only derived scan production, explicit cross-index parent context,
  unified/indexed reports and all other goal acceptance remain outstanding.
  No private proof dependency, Windows data, PR or merge was touched.

## Complete source snapshot roster retention (2026-09-28)

- Implementation continues on `codex/webforms-native-preflight` in the managed
  `webforms-native-attachment` checkout. It was created at `758b61ea` while that
  head's suite ran unchanged; after the suite passed, the prior publish checkout
  was detached at its verified head and the existing branch transferred here.
  Old ignored binaries/proof dependencies remain in place; no lane was purged.
- The scanner returns its complete authoritative snapshot inventory through an
  additive JSON-ignored execution-only ScanResult property, preserving the
  original constructor/deconstruction and default artifact schemas. Opt-in
  `scan --retain-source-snapshot` streams that roster into local NDJSON and a
  small exact CLI/Core generator, original manifest, source/roster and bounded
  input hash manifest. The NDJSON roster also has its own exact generator and
  bounded framed-source-input header, validated before yielding membership.
  No source snippets, binary copies or derived parent facts
  are added. Native fresh execution requests and pins both artifacts.
- Parent preflight pins the optional complete pair; a partial pair fails. Source
  validation prefers the retained complete membership, checks original scan and
  artifact identities/count/bytes, and applies current admission budgets. An
  actual project-scoped parent with uninventoried root semantic metadata validates
  without modifying its facts/index. Legacy exact-inventory fallback remains;
  incomplete older snapshots still refuse guessed membership.
- Configured file/source/roster limit values are validated before scanning;
  actual member/byte admission occurs during retention, after scanning. Roster
  writing/reading is streamed, with a 32,768-character member line bound covering
  escaped relative paths. These caps are not a global memory or eight-times scale
  claim. Fresh completed checkpoints verify roster/source identity; completed
  fresh resume retains its prior contract and does not rehash current unpinned
  source or rerun analysis. Tool/input/artifact tampering still rejects resume.
- Initial focused validation passed 84/84 (49 seconds), including real scoped
  retention, native fresh/resume, ordinary-scan fact/identity parity, parent
  immutability, generator/input/hash tamper, typed limits and bounded reader cases.
  CLI build passed with zero compiler/analyzer warnings. Final broader validation
  passed 227/227, zero failed/skipped (1 minute 8 seconds), after typed retention
  diagnostics, escaped-line bounds, incomplete/directory pair refusal and the
  roster's independent generator/input header were added. The denominator includes
  snapshot/retention, native preflight/input/execution/preparation, scanner, CLI,
  output transaction and execution-receipt regressions. Diff and private-path
  guards passed. Full regression at this new committed head is a separate gate;
  the prior 2,472-test result does not carry forward to these changes.
- Full regression at committed `a5f99d83` subsequently passed 2,494/2,494,
  zero failed/skipped (7 minutes 2 seconds), before compiled-only production.
- Compiled-only attachment production, explicit cross-index parent joins,
  unified grouped/indexed reports, Claude guidance, all-pages receipt partitioning,
  public compiled-site parity, scale/retention/relocation and final authorized
  private Windows validation remain outstanding. No PR or merge occurred.

## Compiled-only Core attachment production (2026-09-28)

- Added a Core producer that verifies retained source bytes before/after the
  existing metadata, IL, portable-PDB and publish-receipt readers. It runs no
  source extractors, MSBuild, Git discovery or output writer, and returns no
  source inventory/fact copies. Parent facts/index IDs remain untouched.
- An additive null-omitted ScanManifest context records exact Core generator,
  bounded input, declared parent manifest/index hashes, original snapshot and
  observed/configured source bounds. Independent caller validation of original
  parent bytes is still required; the Core API does not claim index admission or
  authenticated signatures. Derived source/build state is explicitly not run,
  reduced and review-only; original gaps and existing reader gaps are retained.
- PDB checksum candidates come only from the verified original inventory pass,
  capped at its configured source count plus one for categorical limit reporting.
  The PDB reader also stops before sorting beyond that cap, preserving its prior
  limit verdict. No second caller enumeration can substitute PDB source locators.
- Focused producer/PDB/publish regression passed 74/74, zero failed/skipped
  (53 seconds), with no compiler/analyzer warnings. The producer has 17 cases
  covering exact existing metadata/IL/PDB/publish fact parity, serialization,
  deterministic identity, no source analysis/output writes, unchanged parent
  context/source bytes, source changes/limits, invalid parent hashes, prohibited
  source/build/rewrite options, cancellation and missing inputs. An initial
  test-only serializer method-group and missing token argument were corrected;
  a factory-call-count assertion exposed an unnecessary unverified PDB roster
  enumeration, which was removed in favor of verified bounded capture.
- These synthetic Core cases do not establish native immutable parent/index
  admission, public compiled-site chain parity, cross-index report joins, full
  fresh/attach/resume completion, representative scale or private Windows proof.
  CLI attach remains refused until execution and parent joins are connected.
  Grouped/indexed reports, Claude guidance, all-pages partitioning, retention/
  relocation and final owner validation remain active goal requirements.
- Broader validation passed 374/374, zero failed/skipped (2 minutes 53 seconds),
  across compiled attachment, metadata, IL, PDB, semantic reconciliation,
  publish roots, native preflight/input/preparation/execution, snapshots,
  scan engine, output transaction and scan receipts. No compiler/analyzer
  warnings were emitted. Diff and private-path guards passed. Full regression
  at this later slice remains a separate gate; the 2,494-test result belongs
  to the preceding `a5f99d83` retention head, not these new changes.

## Native fresh execution and resume (2026-09-28)

- Added `webforms-review run/resume --run <explicit-root>` for fresh configs.
  Execution rebuilds/rechecks the preflight contract and exact tool bytes, uses
  the authoritative input gate, and invokes the existing scanner once with
  configured source scope and explicit metadata/receipt/PDB/IL inputs.
- Immutable preflight bytes are retained. Owned attempts and append-only numbered
  checkpoints replace timestamp/TEMP discovery. Every checkpoint has exact CLI
  and preflight hashes, a bounded input hash, a complete payload integrity hash,
  and the prior checkpoint byte hash. An exclusive file lock prevents two scans.
- Completed output is admitted only after required artifacts, streaming hashes,
  actual source snapshot and manifest/SQLite/NDJSON parity pass. Resume checks
  retained artifact hashes without rescanning source or changing the snapshot.
  Failed/cancelled/interrupted attempts remain separate from a fresh retry.
- Native execution still ends at `scan-completed-reports-pending`. Attachment
  is refused explicitly rather than silently rescanning the retained parent's
  source. Unified workbench, publish-map receipt production, cross-scan compiled
  provenance joins, grouped indexed handoff, Claude updates and scale gates
  remain outstanding. No proof cleanup or wrapper retirement occurred.
- Final focused validation passed 130/130 across native execution (23 tests),
  preflight/input validation, Core metadata policy, existing CLI and local-review
  regressions. Rebuilds emitted no compiler/analyzer warnings; private-path and
  diff guards passed. Full regression at `33cd362a` passed 2,404/2,404, zero
  failed/skipped (6 minutes 17 seconds); that milestone is pushed.
  Distribution dependency/runtime-version pinning includes a bounded digest test;
  external SDK bytes and clean-source authenticity remain explicit gaps. No
  private Windows run or full workflow parity is claimed.
- Next implementation constraint: existing projectless publish graph joins
  require source/page/handler/declaration and compiled facts in one source index.
  A future attachment must introduce an explicitly validated parent-context join
  while retaining original source/index/fact identities. Combining indexes alone
  cannot establish it; do not relabel/reemit parent facts to force a join. Fresh
  publish receipt support must also preserve explicit operator attestation rather
  than treating a DLL hash or Git HEAD as source/build ownership proof.

## Native input validation gate (2026-09-28)

- Added `ManagedMetadataExtractor.InspectInputs`, a narrow Core facade over the
  unchanged scanner evaluation policy, including categorical global gaps. It
  writes no artifacts and does not perform IL or runtime execution.
- Added the internal `WebFormsReviewInputValidation` execution gate: pin/recheck
  explicit inputs and Git identity, inspect compiled receipt policy outcomes,
  validate retained parent repository/root/snapshot identity, full embedded
  manifest equality and SQLite integrity, then compare every streamed NDJSON
  fact with its indexed content. Duplicate, missing, altered and oversized rows
  fail before any scan/report execution.
- Read-only immutable SQLite URI inspection supports sidecar-free checkpointed
  WAL files without creating parent sidecars. Active sidecars are rejected;
  temporary duplicate-ID tracking is file-backed with a bounded cache. Native
  SQLite work can be interrupted through the cancellation token.
- Metadata work/text, IL text, parent fact-count and per-fact line limits are
  explicit, independently validated config budgets; older configs use defaults.
- Public focused validation passed 77/77 across the preflight, execution-gate
  and managed-metadata policy tests. Builds emitted no compiler/analyzer
  warnings; `git diff --check` passed. Full regression at `3d60e2ee` passed
  2,381/2,381, zero failed/skipped (8 minutes 25 seconds).
- This is an internal gate, not a new operator command. It is not yet wired into
  native fresh/attach/resume execution, which remains the next implementation
  step. Current source-byte validation still belongs to the actual source scan;
  retained attachment identity is not a claim about current source contents.

## Native preflight slice (2026-09-28)

- Branch: `codex/webforms-native-preflight`, based on merged PR #796 at
  `f7891d980c2fd8080b70c14d0ea07a834598d35a`.
- Scope: `tracemap webforms-review preflight`, a private versioned fresh/attach
  config, explicit compiled input inventory and new durable run manifest only.
- No scanner/reducer semantics or proven PowerShell entry points changed. No
  scans, publishing, binding admission, reports, resume or cleanup occur here.
- Parent artifacts are streamed and pinned without mutation. Source commit,
  managed PE header format, configured roots/output separation and receipt hash
  candidates are checked; source snapshot, repository/index identity, full
  binding, map and PDB identity are explicitly deferred gaps.
- The temporary comparison/grouping helpers remain on their separate pushed
  branch; they are not folded into production by this slice.
- Operator contract and planned subsequent phases:
  [`docs/WEBFORMS_NATIVE_WORKFLOW.md`](../../../docs/WEBFORMS_NATIVE_WORKFLOW.md).
- Validation: final focused preflight tests passed 27/27; the final full .NET
  suite passed 2,353/2,353, zero failed/skipped. The focused rebuild emitted no
  compiler/analyzer warnings. `git diff --check` passed.
- Native CLI preflight smoke against explicit public Web Forms markup and a
  public managed assembly created only `run-manifest.json` and `README.md`.
  Its generator digest matched the exact CLI DLL. Existing `scan` smoke against
  `samples/vb-modern-sample` produced 222 facts with `Level1SemanticAnalysis`.
  These checks do not establish source/DLL binding or a compiled-site scan.
- Real private Windows run and eight-times scale acceptance are not established.


Record type: historical implementation and validation record

Status: clean-run pipeline present on current `dev`; PR #770 is authoritative
for terminal reachability

Historical branch: `codex/vb-webforms-battle-test` (retained research only; do
not merge wholesale)

Public claim level: hidden

## Selected compiled-proof export closeout (2026-09-27)

- Branch: `codex/webforms-proof-exports`; base: `dev`.
- Tested code checkpoint: `013317c6ba927566edb8b9d2e6dea882e5692559`.
- Full .NET suite: 2,326 passed, zero failed/skipped. Focused packet/memory
  regressions: 54 passed; strengthened combined-limit publication cases: 3
  passed. Saved-packet, short-view, and application-workbench guards passed.
- The additional existing-publish end-to-end public guard passed with a stable
  checkout after an initial attestation-case failure overlapped a documentation
  commit. Commit drift is a possible explanation, not a proven defect. Its
  failure now retains categorical stage/count output for future diagnosis.
- Operator-provided Windows readback at that checkpoint showed a successful
  saved-proof replay with 41 review-only database-api paths and one selected
  surface. Screenshots showed the workbench's supplemental link and readable
  method transitions with per-hop rule/tier/location and expandable identities.
  This is operator readback, not a coordinator rerun or all-pages validation.
- The source packet remains reduced/truncated. Saved compiled paths are a
  separate supplemental artifact, not an upgrade of the page-chain verdict,
  source-line identity, runtime dispatch, or SQL execution evidence.
- No private source, DLLs, indexes, paths, or screenshots are checked in.
- Normal `scan` already supports compiled inputs and binding receipts, but the
  focused pipeline does not yet offer the complete compiled-site configuration
  and .NET orchestration contract. The new requirements/tasks describe planned
  work, not implemented commands or a completed larger-corpus capability.
- The old 2 GiB combined-index proof-admission ceiling was not a universal scan
  limit. This proof helper now streams hashes with a 4 GiB index ceiling; the
  focused pipeline's receipt hashing has a separate 16 GiB artifact ceiling.
  Packet input admission, IL work, traversal, and output caps are independent.
  None of these ceilings establishes acceptable memory/time for an eight-times
  repository. Larger-corpus benchmarks and bounded graph redesign remain open.
- Retain the working proof and its dependencies until durable archival and
  reopen/hash verification. Cleanup of unrelated temporary attempts is separate
  from source/scan/report authority; no automated deletion has been implemented.

## Completed foundation

### PR #796 consolidated review repair

- SQL replay now always emits a fresh handoff on repeat runs. A missing or
  ambiguous optional API handler retains the gap proof instead of failing it.
- API receipt reuse binds exact report bytes and recomputes the saved input
  digest. Old receipts stop with an explicit recheck-required category;
  `wview.ps1 -FromSavedProof -RecheckApi` regenerates the graph report without
  scanning, publishing, or collecting again.
- Handoffs validate scan-source repository metadata and retain repository
  identity hashes. Saved packets require repository ID plus commit agreement.
- Gap rules/tiers are validated; available source IDs, spans, extractor scope,
  support IDs, and candidate accounting survive projection. JSON and Markdown
  packet artifacts each have an actual SHA-256 receipt entry.
- Public publish end-to-end guard passed with a stable checkout, including a
  missing-handler gap proof. Public handoff, saved-packet, short-view,
  workbench, and packet-log guards passed. Earlier full .NET checkpoint remains
  2,326/2,326; this repair changes PowerShell projection/orchestration only.
- Larger-corpus performance and native .NET orchestration remain planned, not
  acceptance claims for this proof closeout.
- Follow-up exact-head repair: an explicitly selected `wview -ProofRoot`
  permits its saved base index; automatic discovery remains higher-work only.
  Both normal workbench attachment and standalone packet discovery now match
  repository ID plus commit, rejecting same-commit fork evidence. Public view,
  workbench, handoff, saved-packet, and packet-log guards pass. Refreshed full
  .NET suite passes 2,326/2,326 (zero failed/skipped); the public publish
  end-to-end guard also passes. Windows saved-proof acceptance remains a
  separate operator check.

- Full application workbench with compact application index and per-page
  evidence handoffs.
- Explicit `P / F / S` call accounting, normalized source sites, evidence
  ceilings, omissions, gaps, and incomplete-chain states.
- Private application-index rows expose the full retained route and bounded
  control ID/type projection beneath each alias; shareable outlier artifacts
  remain alias/count only.
- Compiler-backed technology-family projections with syntax fallback retained
  separately.
- C# and VB.NET code-behind support, plus projectless reduced-coverage handling.
- Private versus alias-only shareable output boundary.
- Generator and bounded-input provenance hashes for current handoff artifacts.
- WITS immutable human-review overlay and supplemental exceptional-handler
  review set.

## Aggregate validation checkpoint

An authorized identity-free stress run completed 476 selected surfaces. It
demonstrated that projection/fact/site counts can differ, that evidence ceilings
and omissions need first-class reporting, and that repeated coverage gaps must
be triaged as possible extractor limitations before being treated as hundreds
of application issues. Private source identities and paths are intentionally
not recorded here.

## Current decisions

- Public documentation and work-machine agent ingestion are higher priority
  than external ticket automation.
- The VB.NET battle-test guide remains a validation appendix, not the primary
  onboarding path.
- The existing scripts remain supported while a setup/config/orchestration
  layer is added above them.
- Agent prompts consume retained evidence and produce interpretations; they do
  not become scanner rules or scanner facts.
- External ticket creation begins with an alias-only dry run and explicit
  approval pipeline.

## Implemented clean-run workflow

- `Initialize-FocusedWebFormsReview.ps1` creates one empty review root, a
  seven-setting config, logs directory, and local retention README.
- `Invoke-FocusedWebFormsPipeline.ps1` runs build, scan, all/selected-page
  packet composition, evidence-docs export, and application workbench creation.
- `run-receipt.json` binds the run to the config hash, pipeline generator hash,
  source commit, TraceMap commit, exact artifact paths, sizes, and hashes.
- Resume validates every completed artifact and reuses only an identical run;
  it does not select folders by timestamp.
- Project selection supports explicit solution, explicit projects, bounded
  discovery under the three configured roots, and explicit projectless mode.
- Wrong folders identify the requested value and source root. Wrong solution
  and project paths include bounded in-scope candidates.
- Config loading rejects unescaped Windows backslashes before JSON parsing,
  including sequences such as `\t` that JSON would otherwise accept and alter.
  The failure directs operators to the unambiguous `C:/...` form.
- First-run recovery guidance distinguishes an empty solution intersection from
  empty bounded discovery, routes Web Site checkouts to explicit projectless
  mode, and limits failed-receipt removal to runs with no completed retained
  stage.
- Retained artifact hashing is bounded at 16 GiB. A narrowly guarded migration
  promotes an otherwise complete scan that failed only at the former 2 GiB
  receipt limit, preserves the prior TraceMap commit and generator hash in the
  migration record, and continues downstream stages without rescanning.
- C# call-edge producers now label compiler-resolved evidence
  `bounded-semantic-callgraph` and syntax fallback `syntax-only`. This prevents
  otherwise valid retained calls from being misreported as
  `EvidenceCoverageLabelUnavailable` packet gaps.
- `Export-FocusedWebFormsPageShareable.ps1` validates the completed workbench
  receipt, resolves one page alias, and emits one explicitly named shareable
  JSON/ZIP pair. The projection keeps anonymous chain/endpoint/handler/site/
  callee equality and bounded technology/boundary shape signals while omitting
  paths, symbols, URLs, spans, source/scan/commit identity, raw evidence IDs,
  human comments, and the private handoff fingerprint.
- Compiler-resolved C# calls through local helpers now join generated WCF
  client operations by canonical call-target identity. Web Forms traversal
  stops at the external `wcf-operation` boundary and does not claim behavior
  inside the remote service or any downstream database.

## Validation checkpoint

- Focused setup/config, launcher, and application-workbench PowerShell suites
  pass.
- Setup/config tests pin actionable failures for both invalid and silently
  parseable single-backslash Windows paths.
- One-project C#, mixed multi-project C#/VB.NET, discover, projectless, all-page,
  and selected-page config contracts are pinned.
- Folder discovery retains C# and VB.NET project files beneath the three roots
  and excludes an unrelated fourth root.
- Focused C# semantic, syntax, and Web Forms packet tests pin explicit call
  coverage labels and pass.
- A synthetic public regression pins `AJAX -> ASHX -> local helper -> generated
  WCF proxy` composition, the external WCF terminal, and the absence of an
  inferred database boundary.
- The application-workbench regression pins per-page shareable provenance,
  shared endpoint/handler equality, normalized-site structure, ZIP contents,
  structural signals, and a denylist of private fixture identities.
- An external selected-page C# validation retained identical call accounting
  before and after the producer-label correction: 43 pages, 3,999
  chain-associated projections, 3,798 unique call facts, and 1,943 normalized
  source sites. `EvidenceCoverageLabelUnavailable` fell from 3,798 to zero and
  the alias-only summary reported zero remaining packet gaps. No source paths,
  symbols, repository identity, or private input fingerprints were retained in
  this checkpoint.
- A clean projectless VB.NET Web Forms fixture completed scan, packet,
  evidence-docs, and workbench publication. A second invocation reused all five
  stages under the original run ID after verifying receipt provenance and
  artifact hashes.

## Remaining follow-up

### Local config migration branch (2026-09-29)

- Branch `codex/webforms-config-migration` starts from merged dev `8cae5664`.
  No private user configs are available here or committed. The local .NET
  `migrate-config` command converts the known pipeline JSON/JSONC schema into a
  separate native draft and hash-bound local receipt; `wmigrate.ps1` is only a
  short build/launch/prompt helper for one or two explicitly named review roots.
- Existing source/folder/project/page settings are preserved. Missing commit,
  publish inventory/membership and binding/attestation are never inferred.
  Drafts remain not ready; discover and virtual-route cases require owner input.
  Original config/scans remain immutable; two-repository merge replacement is
  not implemented or claimed by this config migration.
- Validation results are recorded with the final commit handoff; user-machine
  acceptance remains separate. No cleanup, scans or publishing are performed.

### Retained baseline proof import (2026-09-29)

- The user confirmed unchanged source/solution and existing aspnet_compiler
  output and authorized building/pushing the verification helper. The actual
  private input files remain on the user's Windows machine, not in this repo.
- Native `import-proof` accepts an exact draft, retained proof root and original
  publish root, verifies legacy source/assembly/map and binding commitments,
  exact source commit/remote, clean committed source membership, raw retained
  source/DLL bytes, and primary metadata binding admission. It never issues a
  new attestation, edits source, infers ownership from DLL names or substitutes
  another commit/root when inputs do not match.
- Legacy selected DLLs remain primary; artifact context stays unbound dependency
  context. Baseline selected pages are explicit in the output and receipt;
  migration draft source/project scopes and budgets stay unchanged. Retained
  source/publish/binding receipts remain external and byte-identical.
- `wverify.ps1` is a prompt/build/launch helper. TEMP candidates require explicit
  owner selection, not newest discovery. Default is input verification only;
  `-Run` invokes existing native start into a separate output for new document
  generation. Existing output and overlaps are refused; no cleanup or original
  solution rebuild is performed. Private parity is not claimed, and backend
  source scopes/two-repository merging remain separate existing-workflow gates.
- Public direct/import/helper validation and final-head build/full-suite results
  are recorded in the handoff. No private config, path or input was committed.

### PR #797 round-one review repair (2026-09-29)

- Owner authorized up to four review/fix rounds, no merge. Initial settled ACK
  on `e829c36c85f96449660bd8ce58b88ad950e6e4d3` reported three unresolved
  threads, no held findings, and zero pending/failed checks.
- Preserve native structured scan option values without comma-list expansion;
  ordinary `scan` and legacy local-review parsing stay unchanged. Actual native
  fresh/report/resume tests include comma-containing roots, compiled paths and
  selected source-folder globs; start also uses a comma-containing output root.
- Read-only evidence queries share read locks, continue to exclude writers and
  retain hash/checkpoint revalidation. Lock-open I/O is reported as busy or lock
  unavailable, not corrupt index. Other operations keep their existing locks.
- The strengthened comma regression also exposed native selected-page lines
  being reparsed as CSV. Native reporting now declares literal-path line mode;
  legacy page-list CSV/header/comment parsing remains default. Tests distinguish
  comma, quote and hash-containing literal names from legacy requests.
- The solution/project fallback comment is unreachable in this native config:
  `solution` and `projects` are mutually exclusive at preflight. Added both
  mixed-mode rejection regressions and opposite-argument absence assertions;
  do not expand this PR into semantic-extractor behavior changes.
- Focused and final exact-head regression results will be recorded in the PR
  settlement evidence. Historical passing receipts remain tied to their heads;
  private Windows acceptance, wrapper retirement and cleanup remain separate.
- Final round-one focused regression matrix passed 30 tests, zero failures or
  skips (15 seconds), after the literal page-list fix. The earlier 174-test
  focused run passed before that strengthened regression; it is not the final
  head gate. Commit and rebuild before the full exact-head validation/push.

### PR #797 round-two review repair (2026-09-29)

- Fresh Codex review on `ff3b2a9b` identified native relative source folders
  beginning with `--` being rejected as missing option values after preflight.
  Reproduced with `--legacy` and `--legacy,literal`: both failed while the
  existing ordinary/comma cases passed. Generated native key/value pairs now
  consume values positionally; caller-facing legacy CLI parsing is unchanged.
- The real native scan/report/resume regression matrix retains handler evidence
  for all four folder/root combinations. Final head validation belongs to the
  PR settlement receipts; earlier round-one receipts remain historical.

Consolidate the long manual compatibility reference only after its diagnostic
and recovery entry points have equivalent behavioral tests. Ticket automation
and licensing remain deliberately separate private follow-up work.

### Retained-proof import diagnostic repair (2026-09-29)

- Private Windows verification stopped with the generic input/output-invalid
  code before any scan. Its exact cause is not established by the screenshot.
- Import now reports a closed stage and safe failure category; missing required
  receipt fields expose only schema field names, never private paths or raw
  JSON/IO exception text. Preparation failures are handled by the direct import
  entry point, not only the outer CLI dispatcher.
- Added direct-entry negative regressions for missing draft/receipts/source/DLL,
  missing receipt fields/rosters, malformed JSON and dirty source scope. No root
  rebasing, receipt rewriting, new attestation or verification bypass was added.
- Private acceptance still requires a rerun and actual document comparison.
- Repair validation: solution build passed with zero warnings/errors; 92 focused
  migration/import/preparation tests passed with zero failures/skips; private
  path guard and diff whitespace checks passed. The prior 2,840-test full-suite
  pass belongs to the parent head, not this diagnostic repair.

### Explicit retained receipt source base (2026-09-29)

- Owner confirmed the configured root is the unchanged repository root, while
  the website is a child folder and old receipts use website-relative paths.
  The importer previously interpreted these paths at the repository root.
- Added explicit import/helper source-base selection and an additive native
  config field. Repository/source/project scopes and receipt bytes are preserved;
  selected page and emitted evidence paths use the repository-relative prefix.
- The base is bound into import, publish provenance and scan authorization
  fingerprints and propagated through preflight, fresh execution and attachment.
  No base discovery, root mutation, receipt rewriting or new attestation.
- Public regressions cover root and nested website layouts, unsafe bases,
  changed bytes, virtual-route preservation and the real helper/native pipeline.
  Private document parity remains owner verification, not a synthetic-test claim.
- Validation: clean solution build (zero warnings/errors), 651 broader Web Forms/
  scan-receipt/attachment tests and 156 final focused import/migration/preparation/
  publish/receipt tests passed, zero failures/skips. The focused run includes the
  final added partition-base and nested-repository regressions. Private-path and
  whitespace guards passed; the whole-repository suite was not rerun for this slice.

### Read-only retained binding admission diagnostic (2026-09-29)

- Private rerun passed source-base, byte/commit/receipt checks but stopped at
  retained compiled binding admission. The cause is not established by that
  screenshot. Inspection found a possible historical Windows relative-locator
  issue; no receipt rebind, metadata-policy bypass or attestation was inferred.
- Added explicit helper/native diagnostic mode with counts and closed gap/state
  codes only, no artifacts or scan. Existing failed outputs and original proof
  inputs are retained unchanged. Normal failures also report admission counts.
- Public tests pin locator/identity rejection summaries, original/staging/output
  byte preservation and real helper diagnostics for root/nested layouts.
- Validation: clean solution build, 104 focused import/migration/preparation
  tests plus all three final locator/identity/traversal diagnostic cases passed;
  zero warnings, failures or skips. Private-path and whitespace guards passed.
  No private acceptance or whole-repository-suite rerun is claimed.

### Retained locator projection (2026-09-29)

- Owner's read-only private diagnostic established 2 expected/observed primary
  DLLs, 0 locator matches, and 2 retained `../` locators. Admission was
  unbound; metadata-reader disagreement and unresolved reference gaps remain.
- Fixed Windows external-path classification so machine-root-relative paths
  cannot be treated as repository children. The importer projects only exact
  original proof-copy coordinates, checks both copied and published bytes and
  assembly identity, and emits a separate generator/input-hashed local binding
  receipt. Original owner receipts and source/build claims are unchanged.
- The helper preserves prior failed output and asks for a new short run-folder
  name. Private native rerun and baseline document comparison remain required;
  synthetic tests cannot establish private report parity.
- Validation: whole solution builds with zero warnings/errors; 657 targeted Web
  Forms, managed metadata and scan-execution-receipt tests passed with zero
  failures/skips. The final tampered proof-copy refusal test was rerun after its
  last edit and passed. Private rerun and whole-repository tests remain unrun.
