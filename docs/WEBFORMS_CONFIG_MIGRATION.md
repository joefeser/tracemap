# Migrate existing private Web Forms configs

## Mixed-query traversal repair: work-machine validation

For a successful saved verification run, inspect unresolved command bindings
without scanning or traversing again:

```powershell
.\scripts\wsqlroute.ps1 -VerificationRoot "<verification-folder>" -Handler "<exact-handler-method>" -UnresolvedOnly -Open
```

The pinned native status reader verifies the completed report before selecting
its handoff. The new HTML contains only groups with unresolved command-text
bindings; expand them to inspect `commandTextFromPath`, its origin/reason, the
retained route and non-IL transitions. Counts at the top still cover all variants
for the selected handler. Repeated variants are not distinct unresolved causes.
Original query limits and gaps cover the original query's full root set.

To investigate a shared path cap, create a separate single-handler report from
the completed run's retained combined index:

```powershell
.\scripts\whandler.ps1 -VerificationRoot "<verification-folder>" -Handler "<exact-handler-method>" -Requery -FillOnly -CompiledOnly -Open
```

Omit `-CompiledOnly` for the corresponding mixed-source report. These operations
rebuild the graph from saved facts and traverse one root; they do not scan source
or DLLs or combine indexes again. Each keeps the original depth/work/path limits,
and may still report truncation. The wrapper uses the pinned original reader to
locate the completed bundle, builds the current CLI for the new operation, and
writes under a new `handler-requery-*` folder. It prompts for a new name when
that folder already exists. The original completed checkpoint, combined index,
compiled report and evidence-index bindings are checked before use. The new
receipt records `completedReportSha256` and has no recovery-receipt hash; recovery
runs retain their existing receipt contract.

Use `wcompare.ps1` with the new handler report and the historical handler JSON
to compare actual method sequences. A mixed/compiled scope difference is a
comparison limitation, not automatically an omitted IL route. A single-handler
result alone also does not prove why a route was absent from a broad report.

Algorithm 1.3 prioritizes admitted IL calls and prunes depth-infeasible mixed
branches using a complete, bounded reverse-distance pass. The synthetic native
regression now retains six routes across five handlers in 391 work units, rather
than exhausting 100,000 with no routes. Limits and evidence tiers are unchanged;
depth/cycle gaps remain partial. This is not a physical drive-I/O or private
application acceptance result.

The public Windows gate now passed at code head `6dbdf2ba`: all 14 cases passed,
including actual ASP.NET mapped/mapless publishing, and retained receipts/DLLs
were inspected. [Windows validation run](https://github.com/joefeser/tracemap/actions/runs/36788401704).
To repeat that small synthetic check on your own machine, use PowerShell 7,
.NET 10 SDK and the ASP.NET Framework compiler:

```powershell
.\scripts\validation\Test-DeepProjectlessCorpus.ps1 -RequireWindowsPublish -OutputRoot "$env:USERPROFILE\tracemap-deep-corpus-1"
```

The remaining owner gate is a fresh retained-proof application run. From the
repository root, run `git pull --ff-only`, then
`.\scripts\wcmdverify.ps1 -Open` with the retained proof and a new verification
output folder. This helper builds TraceMap automatically. Existing runs use
immutable older tool snapshots; resuming them
does not apply this repair. Do not overwrite or delete old proofs/reports, and
do not treat successful synthetic publishing as private migration completeness.

## Source command-binding extraction boundary

The VB semantic extractor now retains compiler-resolved `CommandText`
assignments (constant hash/length or dynamic classification) and adapter
`SelectCommand`/`InsertCommand`/`UpdateCommand`/`DeleteCommand` assignments.
Command and adapter receiver symbol IDs allow later bounded correlation with
operation receivers. These are assignment candidates, not proof of the final
command, branch feasibility, alias flow, parameter propagation or execution.
Raw command strings are not stored. No new SQL statement is inferred.

This requires a compiler-resolved source scan. It does not retrofit the saved
projectless/compiled-only reports or recover string values from their existing
IL call facts. Do not rerun the saved ledger expecting new SQL evidence. IL
command-value extraction now exists, but private handler-to-command acceptance
remains open until a fresh owner scan produces the expected candidates.


## Extract saved handler database and SQL evidence

```powershell
.\scripts\wsqlroute.ps1 -Open
```

Enter the saved mixed-mode `verify-5\handler-requery-fill` folder, not the
compiled-only folder. This reads its grouped handoff (or an explicit raw paths
JSON), resolves references and writes a local HTML ledger. It does not build,
scan, query SQLite, execute SQL or traverse the graph. Existing ledgers are
preserved using numbered filenames; explicit output collisions fail.

The ledger displays route labels, retained database API / SQL query / SQL
persistence surface fields, their rule/tier/location/provenance, non-IL edge
kinds and saved report gaps. It never derives SQL, procedure identity or
parameter values from method names or a Fill endpoint. Missing SQL surfaces
and missing edge evidence are explicit gaps. Native receipts are not admitted;
the result is private, partial, as-supplied evidence, not execution or parity
proof. Full node records define ledger groups so different evidence is not
silently discarded; group counts are not chain-parity counts.

Input is capped at 256 MiB, 10,000 variants, 2,048 nodes/edges per variant and
500,000 combined references. Output is capped at 32 MiB with at most 500 groups
displayed. Exact helper and input hashes bind the bounded-input receipt.
Route display additionally has an 8 MiB budget, with individual sections clipped
at 65,536 characters. Report gaps are counted by kind/reason rather than copied
wholesale: at most 1,000 retained categories, 200 displayed categories and three
samples per category. Sample fields are limited to 1,024 characters and each
category's sample display to 8,192 characters. Overflow and displayed counts are
explicit; original files retain the complete evidence.


## Compare saved handler routes

```powershell
.\scripts\wcompare.ps1 -Open
```

Choose the historical handler JSON from the numbered retained-proof list, or
enter its full filename. Enter the current `verify-5\handler-requery-compiled-fill`
folder next. Raw `paths` reports and native grouped handoffs are supported.
The helper writes `chain-comparison.local.html` beside the current report;
an existing comparison is preserved by choosing `chain-comparison-2.local.html`,
then the next free numbered name (up to 1,000). An explicit `-OutputPath` still
refuses to overwrite an existing file.
It performs no scan, build or graph traversal.

When present, the sibling `handler-requery-fill/compiled-paths.handoff.local.json`
is also read to check historical-only symbol hints against the saved mixed-mode
result. Use `-Mixed <folder-or-json>` for another saved report. Its query context
and exact file hash are included; absent mixed results are labeled, not queried.
Historical edge kinds, rules, tiers and endpoint node IDs are shown for each
historical symbol difference and shared variant-count difference. These are
as-supplied evidence, not native admission; empty edge arrays are unknown, not
proof of a pure IL route. Inspect edge kinds before attributing a difference to
the compiled-only filter. Reading method labels alone is insufficient.

The private comparison shows both query settings, coverage, declared source/index
identities, distinct sequences and variant counts. Exact sequences include node
kind, source index, scan, commit, symbol-or-node identity and display name.
Symbol-only matches omit source/scan/commit and are explicitly diagnostic hints;
their unique sequence counts, historical/current-only sequences and shared
variant-count differences are shown first in the difference sections. Variants
from different exact identities are counted together for each symbol-only hint.
They do not establish input equivalence. Source and compiled nodes are not
collapsed. File SHA-256 values, helper generator SHA-256 and a bounded-input
SHA-256 identify the comparison inputs. Native receipts are not admitted by this
helper. Original chain IDs are not assumed comparable across different scans.
Inputs are capped at 256 MiB each, 10,000 variants, 2,048 nodes per variant and
500,000 node references and 500,000 edge references per report; output is capped at 32 MiB and displays at
most 500 differences while counts cover all admitted sequences. Unsupported
formats or broken node references fail before writing the result.

Local grouped reports now derive compact compiled method/constructor labels
from the retained exact symbol when the general safe-display field is redacted.
This restores the earlier private readable-path behavior without weakening
shared report redaction, changing graph edges, or changing lossless JSON. Labels
are display-only; complete identities and evidence remain in expandable details.
Existing report files are not rewritten by this change.

## Compare the retained handler using the old Fill-only terminal scope

For the separate compiled call-tree baseline:

```powershell
.\scripts\whandler.ps1 -Requery -CompiledOnly -FillOnly -Open
```

Enter `verify-5\recovered-reports`. Output goes to a new
`verify-5\handler-requery-compiled-fill` folder. The query allows root selection
and one existing evidenced source-to-compiled attachment, then only
`compiled-il-call`, `compiled-il-callvirt-candidate` and
`compiled-database-api-candidate` edges. Source calls, receiver/constructor
bridges and compiled-to-source return hops are excluded during traversal, not
filtered from an already capped mixed result. All global admission competitors
remain available; no symbol is guessed to compensate for a missing attachment.
The root attachment keeps its original tier and can be a publish candidate;
this is not an authenticated-build or source-line identity claim. Callvirt still
does not prove runtime dispatch. Missing paths under this scope are gaps, not
proof that backend behavior is absent. The hashed query records
`traversalScope=compiled-il-with-root-attachment`. Historical queries omit this
optional field. Defaults and the mixed query below remain unchanged.

Compiled-only queries do not use the unrestricted depth-recovery prewalk.
Every attachment, IL call and terminal edge counts toward the depth limit.
If no terminal is reached within that bound, the report keeps its depth gap and
`CompiledBaselineNoPath`; it does not recover a witness through source bridges.
Existing compiled-only reports generated by `b9223223` are provisional: its
depth-recovery fallback could admit excluded edges. Presence of
`TerminalReachabilityPrewalk` in their HTML or variant notes identifies that
fallback. Create a new query folder after updating; earlier artifacts retain
their original provenance.

To check an existing report for that marker without rebuilding or traversing:

```powershell
.\scripts\wprewalk.ps1
```

Enter the full `verify-5\handler-requery-compiled-fill` folder. The helper
streams rendered text and prints only marker presence. It does not validate
native hashes or prove all edge kinds are in scope. The old fallback also
requires zero terminal paths for its root, so a single-root 10-chain / 34-variant
readback alone is not evidence that this defect fired.

Graph construction is still repeated, so this is not a promise of a faster run.
The owner Fill-only mixed readback retained 18 chains / 56 variants and cycle/depth
gaps; those counts do not establish equality to the historical 13 / 41 result.

```powershell
.\scripts\whandler.ps1 -Requery -FillOnly -Open
```

Enter the existing `verify-5\recovered-reports` folder. The new default output
is `verify-5\handler-requery-fill`; existing folders are preserved. This repeats
bounded global graph construction and one exact handler traversal, so it can
take as long as the prior requery. It does not scan, combine or rebuild the site.
The terminal filter selects `DbDataAdapter.Fill`, as the old `wpath.ps1 -FillOnly`
did. It does not prune graph competitors, remove source bridges, or imply
historical count/path parity. The filter is recorded in the hashed receipt query.

Mono.Cecil IL is already part of this traversal: the scanner independently
decodes admitted IL with Cecil and System.Reflection.Metadata, retains agreed
body/call evidence, and the report graph projects uniquely matched MethodDef or
assembly-scoped MemberRef targets. Direct `call` and `newobj` become compiled
call edges; `callvirt` remains a candidate because the encoded operand alone
does not select the runtime receiver. Source/compiled joins and VB syntax
bridges are separately labeled, not promoted to IL-call evidence. `calli` has
no uniquely named member target and cannot be silently treated as an exact call.

## Inspect one handler-requery chain without rerunning it

```powershell
.\scripts\wchain.ps1 -Chain 7 -Open
```

Enter the full `verify-5\handler-requery` folder when prompted. This reads only
`compiled-paths.local.html`, not the large JSON handoff or combined index, and
writes a new `chain-7.diagnostic.local.html`. It extracts Chain 7's rendered
identities and connecting evidence, including collapsed detail text, into an
expanded plain-text view. HTML is stripped, decoded, then safely encoded again.
No build, scan, combine or graph traversal runs. Existing output is never replaced.

This is an explicitly unadmitted private diagnostic, not native evidence
validation, graph correctness, runtime execution or parity proof. It preserves
the original report and includes generator/input SHA-256 provenance. Do not
publish the output: exact identities and evidence locations are private. The
HTML input is capped at 512 MiB, retained chain body at 8 MiB and individual
rendered lines at 1 MiB. JSON inventory size does not affect this diagnostic.

From the migration branch on Windows:

```powershell
.\scripts\wmigrate.ps1
```

Enter the copied Web Forms review folder and copied backend review folder when
prompted. These are the folders containing `config/webforms-review.jsonc` (or
the legacy `.json`), not the source repositories or compiled website. Leave the
second prompt blank for one config. The helper builds the .NET CLI once and
delegates all conversion to it; no PowerShell migration engine is introduced.
With no arguments the helper explicitly requests input; a blank first answer
stops immediately. All folders are checked for exactly one legacy config before
building. Quoted pasted folder paths are accepted. Missing or ambiguous configs
produce a local selection diagnostic without running collection or conversion.

Each review folder gains a new `native-config/` containing:

- `review.draft.json`: plain native JSON with the existing source root, three
  folder scopes, project selection and selected pages preserved.
- `migration.local.json`: local-only generator/input/output SHA-256 bindings,
  missing settings and limitations. It retains the old output root for reference.

Original configs, scans, receipts, source and compiled website are not changed.
Existing destinations are refused. If one conversion fails after another
succeeded, keep the successful draft; rerun only the failed review root:

```powershell
.\scripts\wmigrate.ps1 -ReviewRoots 'C:/private/backend-review'
```

## What requires owner input

### Reuse the unchanged retained publish proof

If the earlier compiled-site proof is still available, avoid manually guessing
DLL ownership or issuing a new attestation. On this branch run:

```powershell
pwsh -NoProfile -File .\scripts\wverify.ps1 -Run
```

The helper requests the copied **Web Forms** review folder containing
`native-config`, explicitly selects a retained proof folder (a numbered immediate
TEMP candidate or an exact path), and requests the original compiled-site folder
(parent of `bin`). No newest-folder selection is performed. It does not ask for
or build the original solution. It builds only the TraceMap CLI unless `-NoBuild`
is explicitly supplied.

It imports only the selected proof's existing `publish-receipt.local.json` and
`compiled-binding.local.json`. The native importer checks the receipt's source
commit and repository, source bytes and committed membership, clean source
scope, every recorded DLL hash, inventory/receipt commitments, and actual
primary metadata binding admission. Selected DLLs remain primary; recorded
artifact context becomes unbound dependency context. Original receipt bytes and
their provenance are not rewritten. Missing or mismatched evidence stops it;
it does not substitute current HEAD for a historical commit or create a new
source-to-DLL attestation.

For an exact older proof-copy locator that no longer names the same DLL after
the move, the importer can project that locator into a separate, hash-bound
local binding receipt. It requires the recorded locator to equal the path from
the explicitly selected website base to that proof's copied DLL, plus matching
copied/published DLL hashes and metadata identity. The original binding and
publish receipts stay unchanged. The projected record carries their original
source/build claims; it is not a new owner or compiler attestation. Arbitrary
locator, identity, hash or commit disagreements still stop admission. Windows
external DLL paths now receive root-neutral external locators.

The new default output is `<copied-review>/native-proof-verification/`:

- `configuration/review-config.local.json`: verified settings, bound by the
  paired import receipt and native configuration provenance.
- `configuration/proof-import.local.json`: exact generator/input/output hashes,
  carried receipt commitments, scope, gaps and limitations.
- When a legacy locator needed projection, `configuration/compiled-binding.local.json`
  is the derived, hash-bound locator record and
  `configuration/publish-receipt.local.json` is a byte-identical snapshot of
  the original publish receipt. The config points to this local pair; the
  originals remain at the selected proof folder.
- `review/run/`: only with `-Run`, a new native run with application and compiled
  document sets. Use its printed workbench/handoff paths.
- `tool/`: only with `-Run`, a hash-checked local copy of the CLI distribution
  used to start that run. Later pulls and builds cannot replace those bytes.
  This does not copy or pin the external .NET runtime, SDK or private inputs.

Without `-Run`, the command verifies inputs only. Existing output is refused;
if the default folder exists, the helper requests a short new run-folder name.
You can also use an explicit new `-OutputRoot`. Failures preserve any
diagnostic staging, but a failed staging directory is not an admitted import or
completed run. After an interrupted native run, use native status/resume on its
printed `review/run` path rather than rerunning the import into existing output.

This is a bounded baseline verification run: its **selected pages come from the
retained proof receipt**, even if the migrated draft requested all pages. The
draft's source/project scopes and budgets are preserved. It is not all-pages
coverage. A backend draft with a different source scope cannot inherit the
website receipt or commit. Both projects being in one solution does not bypass
source-byte or receipt checks. Keep the backend scan and original two-source
merge workflow; this helper does not replace that merge.

Successful generation is **not** a parity verdict. Compare the actual grouped
method identities, all retained variants, rules, tiers, DLL/commit provenance,
gaps and truncation against the known-good document set before cleanup. Matching
path counts alone is insufficient. No private Windows acceptance is claimed by
public synthetic tests.

The direct native command is:

```text
tracemap webforms-review import-proof --config review.draft.json --proof-root retained-proof --published-root original-publish --out new-config-folder --source-base website-folder
```

The helper also asks for the **website folder relative to the repository root**.
For a repository containing `Website/Default.aspx`, enter `Website` when the
retained receipt lists `Default.aspx`. Enter `.` only when receipt paths already
start at the configured repository root. This is an explicit selection, not
recursive discovery or a newest-folder heuristic. The native command defaults
to the existing root-relative behavior when `--source-base` is omitted.

The imported config keeps `sourceRoot`, source/project scopes and budgets
unchanged. Its additive `publishSourceRelativeBase` records the chosen base;
selected page paths and emitted source evidence are repository-relative, while
receipt paths, virtual routes and original binding/publish bytes remain
unchanged. The base is included in the import's bounded-input hash, Core publish
provenance and authorized scan-scope fingerprint. Preflight, fresh scans and
compiled attachments use the same mapping. Files must still match their original
hashes and committed Git membership. Unsafe, missing, symlink-escaping or
different-repository bases are refused; no fallback or new attestation is made.

Import failures print a public-safe reason and a closed stage label, not private
paths or raw exception text. For example, `FILE_OR_DIRECTORY_UNAVAILABLE` at
`stage=source-roster` means a receipt-relative source file is unavailable under
the draft's source root; it does not prove the source changed. A missing field
is identified by its schema field name. Preserve the inputs and report that
single diagnostic line; do not upload the receipts or automatically rebase
their paths. No scan starts on an import failure. Failed staging remains
unadmitted and must not be overwritten on retry.

For a compiled binding admission failure, use the short read-only diagnostic:

```powershell
.\scripts\wverify.ps1 -Diagnose
```

Select the same explicit inputs and website base. This mode can read alongside
an existing failed output root without overwriting or deleting it. It validates
the original bounded receipt/source inventories and committed membership, then
prints only admission state/gap codes, expected/observed primary counts, locator
match counts and retained traversal-locator counts. No DLL locator, assembly or
method identity, raw hash or private path is printed. It writes no review/proof
inputs or output folders and never scans; the helper may rebuild the tool unless
`-NoBuild` is supplied. Do not combine `-Diagnose` with `-Run`. A successful
diagnostic is not full preflight, configuration admission or report parity.
The read-only diagnostic shows the *original* retained locator state; it does
not perform or publish a projection. Run the normal helper for that verified
relocation and native preflight.

If a native `-Run` stops after preflight, preserve its output and use the short
read-only checkpoint summary from the TraceMap repository root:

```powershell
.\scripts\wstatus.ps1
```

It searches only bounded immediate folders under the sibling
`tracemap-output` directory, asks for the pinned run by number, and prints recent
sequence/state/gap codes without paths, hashes, or report contents. For another
layout, pass `-RunRoot` with the exact path printed after `webFormsPinnedRun=`.
The prompt accepts either the number or a unique run-folder name. For a failed
report checkpoint, it also prints presence and byte counts for fixed expected
report files; these partial files are not admitted or read as report content.
It also prints whether the fixed compiled output directory exists, its path
length (not the path), and current volume free bytes. Free space sampled now
does not establish free space at the time of failure.
For an existing `reports-failed` checkpoint, `-Probe` separately replays the
bounded packet, compiled-path, index-hash, grouped-projection and handoff-restore stages against
the retained combined index. It builds a separate diagnostic executable, not
the pinned TraceMap CLI, and prints only fixed stage/type identifiers and
code-owned frame names. It does not scan or alter the retained run. It may take
time and use temporary graph storage while reading the private index; keep all
output local and share only the `probe.*` lines. A passing probe narrows the
failure to writer or later stages, not report completion. Do not rebuild the
pinned CLI or resume until the cause is understood.
After the read-only stages pass, `-Probe -ProbeWriter` also replays the writer
into a new private TEMP scratch directory, then removes that directory. It
does not write to the retained failed run or rescan. A process kill or cleanup
failure can leave private scratch files; the latter prints a safe folder label
for local cleanup. A passing scratch writer does not prove the original target
directory or later native artifact admission will succeed.
Native report failures now retain a fixed, private-safe stage code for combine,
packet, paths, index hash, grouped projection, or compiled writer failures.
The code identifies where an attempt stopped; it does not establish a root
cause, report parity, or permission to delete any partial output. A fresh
combine closes its SQLite writer connection instead of retaining a pooled
handle before same-process report reads. This addresses a plausible Windows
handoff failure, but private-run acceptance still requires a completed report.
Only after the owner decides to try the preserved scan's report phase again,
`-Resume` selects the same run and invokes the pinned native CLI without a
build. New runs use their adjacent `tool/` copy; historical runs without one
fall back to the current checkout and may fail the generator guard after a
rebuild. It first requires a native read-only status with a failed report,
verified retained scan artifacts and a reader matching the original generator.
The native resume rechecks runtime and private inputs, allocates a new report
attempt and never reruns the admitted scan. It can still fail and retain more
private partial files; do not delete earlier attempts. `-Resume` cannot be
combined with probe switches.
This is a quick diagnostic, not authenticated checkpoint validation; the native
`webforms-review status --run` command remains authoritative. Neither command
resumes or repeats the failed work.

### When retained proof is unavailable

The old schema does not contain the exact source commit, compiled-site root,
primary/dependency DLL inventory, source-to-publish membership or binding
receipts. These cannot be honestly guessed from config or TEMP names. The draft
therefore reports `readyToRun=false` and leaves mandatory new fields empty.
Fill them using the [native workflow guide](WEBFORMS_NATIVE_WORKFLOW.md), then
run native preflight against a new output root before collection. Configuration
conversion is not source-to-DLL attestation, scan success or runtime SQL proof.

Legacy `discover` mode needs an explicit native project selection; it is not
silently changed to projectless. Leading-slash virtual page routes require
repository-relative `.aspx` paths. Path availability and repository scope are
validated by native preflight, not by migration. Native default budgets are
written without inferring capacity from an old scan.

Each source repository remains independent. Converting the two configs does
not make the native command a two-repository merge replacement. Keep the old
merge workflow and known-good reports until the equivalent scope is validated.

The direct .NET form is:

```text
tracemap webforms-review migrate-config --review-root old-review --out native-config
tracemap webforms-review migrate-config --config old.jsonc --out native-config
```

The converter accepts only the known `focused-webforms-review-config.v1`
schema, bounded to 1 MiB. JSONC comments are supported; duplicate/unknown
properties, trailing commas and ambiguous config-file selection are refused.
It does not support the older three-field page-list-only config, which lacks
source and project scope. Failed staging folders are retained, not admitted as
completed migrations. Keep all generated files private; do not commit or upload
them. The migration receipt binds the exact draft bytes, not later owner edits.

## Completed report gap diagnostics

Run `./scripts/wstatus.ps1 -Gaps` and select the completed verification run (for example `verify-4`). This uses that run's retained tool and checkpointed bounded evidence query, printing only sanitized compiled gap kinds/reasons and returned/omitted counts. It does not print raw handoff JSON, private paths or hashes, rebuild tools, resume, or scan. The first 50 retained gaps are inspected; omitted counts remain explicit. This is diagnosis, not parity proof.

### New-plan report admission capacity

New preflights materialize an omitted `budgets.reports` from the declared scan capacity: fact and edge pools use `maxParentFacts`; text and disk-backed graph storage use `maxRetainedArtifactBytes`, capped at 16 GiB (graph storage has a 64 KiB minimum). Text limits now accept 64-bit JSON numbers. Explicit report budgets are preserved unchanged, including small fail-closed caps. Search depth/work/path, frontier, publication and output limits are not increased.

This removes the hidden 250,000-fact/128 MiB report bottleneck for configurations that already permit larger retained scans. A selected page still admits the global graph so overload, dispatch and cross-source competitors are retained. Capacity is not a memory reservation or a performance guarantee; larger admitted graphs may consume substantially more time, working set and scratch disk. Admission can still fail closed at another configured limit.

Historical plans/checkpoints retain their original policy; do not edit them to upgrade budgets. Start a new verification folder with `./scripts/wverify.ps1 -Run`; the tool scans source again but does not rebuild the website. Keep the earlier run as the comparison baseline. Artifact completion is not parity: compare exact chains and evidence variants before acceptance.

### Recover a retained evidence-node-limit failure without another scan

Run `./scripts/wstatus.ps1 -Recover -Open` and select the failed run (for example `verify-5`). The helper builds the current tool, verifies the source scan checkpoint and the internally linked report hashes, and creates a separate `recovered-reports` bundle beside the original `review` folder. On Windows `-Open` opens its workbench after success. An existing recovery folder is never overwritten; select a new name after a failed recovery.

Recovery is restricted to `WEBFORMS_EVIDENCE_NODE_LIMIT` failures with complete report handoffs. It copies the existing handoffs, renders a recovery-labeled workbench, and rebuilds only the JSON-token query index. It does not rescan source, recollect DLL evidence, rerun graph traversal, change original checkpoints, or declare the original run completed. Hashing and bounded in-memory grouped-handoff validation still consume resources. Partial recovery files remain on failure, without a completed recovery receipt.

New implicit report plans materialize `maxEvidenceNodes=20000000`; explicit report configurations can choose 2 through 50000000 nodes. An absent property in historical/explicit reports retains the old 2000000-node bound. Index construction bulk-loads tokens before building unique lookup indexes; duplicate properties still fail admission. Index byte caps and per-query 2048-node/128-KiB response caps remain unchanged. Old two-million-node contexts remain readable. Capacity is not private-corpus acceptance or a runtime guarantee.

Bounded access to a recovered bundle uses `tracemap webforms-review query-recovery --bundle <bundle>`, with the same `--document`, `--pointer`, `--offset`, `--limit`, and `--depth` options as normal query. It returns a distinct recovery claim level and verifies the recovery receipt/index, not the current source or a completed original checkpoint. The recovery receipt binds the exact current generator and its bounded retained input/artifact hashes. All files remain private.

For a like-for-like handler comparison, run `./scripts/whandler.ps1` and enter the recovered report folder. Its default handler is `BidGroupNamesDDL_Init`; `-Handler <method-name>` selects another literal method name. It invokes `query-recovery --bundle <bundle> --handler <method-name>`, verifies the recovery index, and prints only retained exact-chain/variant counts, distinct source/scan/commit/symbol root identity counts, inspected variants, and the report truncation flag. The indexed projection admits at most 4096 variants, chains, and total chain references; it does not load either full handoff or traverse a graph. Method-name matching may identify multiple roots; this ambiguity is explicit, never merged into a parity claim. A globally truncated result can omit this handler's paths, so even matching historical counts is not complete-path or parity proof. No inputs or failed checkpoints are changed. The query response uses the existing bounded-evidence rule and binds the handler selector and result to its exact generator and indexed input hashes.

### Independent handler requery (no scan)

When the shared-root report is truncated, run `./scripts/whandler.ps1 -Requery -Open`
and enter that same recovered report folder. The helper uses its sibling
`review/run`, builds the current tool, and writes a new sibling `handler-requery`
folder. Existing output is preserved and a new safe folder name is required.
The native command is `webforms-review requery-handler --run <failed-run>
--bundle <recovery-bundle> --handler <literal-method-name> --out <new-folder>`.
It verifies the original manifest/checkpoint commitments, the recovery receipt
and evidence index, and the exact combined-index bytes. One unambiguous exact
source/scan/commit/symbol root is required. It does not load either full recovered
handoff merely to select the root.

This **does** repeat bounded global graph construction and a single-handler
traversal; it does **not** scan source, collect DLLs, rebuild the website, combine
indexes, or rewrite original checkpoints. All global overload/dispatch
competitors remain admitted. Depth, path, work, frontier, graph-input, graph
storage, and projection bounds come from the retained plan. Path/work capacity
is independent for this one-root comparison, not silently raised for the
seven-root report. The legacy `wpath.ps1` also called the C# path reporter; it
selected one exact handler. Synthetic regression fixtures pin equivalent
single-root retained paths and lossless evidence variants for the full reader
and disk-backed reader, including shared-root cap truncation. They are not
private-corpus parity evidence.

Fixed `handlerGraphStage` readbacks identify active graph phases. After graph
completion, per-stage elapsed milliseconds and scratch fact payload-row counts
are printed. Aggregate `logicalFactPayloadBytes` counts application-level
scratch row reads, **not** physical disk I/O; it must not be equated with a
Windows process I/O counter. The separate `handler-requery.local.json` receipt
uses rule `workflow.webforms.retained-handler-requery.v1`, binds the exact CLI
and reporting generators, original input commitments, selected root, bounded
query, observations, and generated artifact hashes. It remains private,
review-only static evidence. Matching a historical count alone does not prove
the same path identities, complete coverage, authenticated build, or runtime
SQL execution. The failed run stays failed. The full-run repeated-I/O cause
remains unproven; indexed typed reconciliation/gap lookups remove avoidable
full-payload passes but are not a demonstrated explanation for that counter.

Recovery bypasses full graph composition. A later public reproduction identified
and fixed repeated whole-table decodes in VB bridge admission using indexed
fact-type reads; the authentic Windows corpus stays below the unchanged logical
read cap. The entire private workload's physical-drive read volume has not been
measured or accepted as solved.

## Fresh compiled command-value validation

IL extractor 0.1.9 / policy v7 handles the synthetic legacy VB patterns that
previously yielded zero candidates: debug field/array writes, byref array reads,
struct/local addresses, checked loops, timeout/transaction setters and table
mappings. Addresses/byref operands remain unknown in exported value facts;
only non-byref scalar command-text origins can cross an exact caller path.
Object stores/address exposure still invalidate affected command state, and
wrong or unknown API contracts remain gaps. These changes require a **new scan**;
they cannot repair the already retained `verify-6` facts. Use a new full output
path such as `<user-profile>\verify-7`, never overwrite the old run.

If the ledger reports zero command bindings, run `./scripts/wcmdfacts.ps1`.
Enter the verification folder, or press Enter for `verify-6` under the Windows
user profile. It streams retained native `facts.ndjson` files and prints operand
and command-candidate fact-type marker counts, plus fixed command/value rejection
gap counts, including explicit zeros. Counts
include all found attempts (at most 32 files / 64 GiB); they are text diagnostics,
not native artifact admission or proof that a selected handler reaches them.
It does not build, rescan, traverse, or modify files.
After a new `verify-7` run, supply that folder explicitly; the helper's Enter
default still selects the older `verify-6` folder.

After pulling the fix, use PowerShell 7 from the repository root:

```powershell
.\scripts\wcmdverify.ps1 -Open
```

Supply the copied review folder, retained proof folder, original published
website folder (parent of `bin`), repository-relative website folder, a **new
full output path that does not exist**, and the literal handler method name.
Do not use an old verification folder as the new output. The helper delegates
retained-proof import and fresh scanning to `wverify.ps1 -Run`, which builds
TraceMap and copies its verified distribution into the new output's `tool`
folder. It does not rebuild the website, execute SQL, clean up old evidence,
resume an old run, or create a new source-to-publish attestation.

All subsequent native operations use that copied `tracemap.dll`; the helper
checks native generator agreement and compares the copied distribution's full
file fingerprint before and after diagnostics. A changed tool or non-admitted
run fails with preserved outputs. Existing retained scans cannot acquire newly
implemented IL operand facts merely by rebuilding a report: this workflow
intentionally starts a fresh scan under the imported retained-proof contract.

For a verified completed report, the ledger filters its retained routes to one
unambiguous handler and prints
`commandValidation.scope=retained-handler-filter;original-query-scope-preserved`.
This filter does not independently traverse a compiled-only graph and does not
change the original report's query scope or truncation.

Only a retained `WEBFORMS_EVIDENCE_NODE_LIMIT` report failure is eligible for
the alternative branch: a separate recovery bundle followed by a new
single-handler `DbDataAdapter.Fill` compiled-IL requery. That branch prints
`commandValidation.scope=new-compiled-il-handler-fill-query;root-attachment-not-il-proof`.
The original run stays failed, root attachment is not IL proof, and recovery or
requery failures stop the workflow. Other scan/report failures are not upgraded.

The private HTML ledger and safe numeric output distinguish command bindings,
hashed constant-text candidates, StoredProcedure enum candidates and unresolved
text candidates. These are **as-supplied static candidates**, not resolved SQL,
procedure identity, runtime dispatch, executed parameters, full coverage or
parity. Exact IL operand/configuration evidence is independently decoded by
Cecil and SRM, joined to body/call/receiver facts, and propagated only across
unique encoded IL call paths. Branches/loops use conservative equality joins;
unknown effects, ambiguous targets, unsupported exception flow, unresolved
root arguments and source-only bridges retain explicit gaps. Hashes do not
reveal or infer the command text. Keep the HTML and native artifacts local;
send only the numeric status/gap lines for initial triage.

The public orchestration regression covers completed and failed/recovery paths,
existing-output refusal, immutable-tool checks and recovery failure stopping.
It uses a fake native command and is not private Windows acceptance. Native
end-to-end tests separately exercise independently decoded synthetic fixtures
and the checked-in VB .NET Framework wrapper's real compiler output.
