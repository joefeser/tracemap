# Migrate existing private Web Forms configs

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
