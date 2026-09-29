# Web Forms Review Productization Implementation State

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

Consolidate the long manual compatibility reference only after its diagnostic
and recovery entry points have equivalent behavioral tests. Ticket automation
and licensing remain deliberately separate private follow-up work.
