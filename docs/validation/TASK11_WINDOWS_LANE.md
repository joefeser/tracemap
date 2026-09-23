# Task 11: bounded Windows validation lane

This is an operator-run, local-only lane for issue #768. It does not close Task
10's `IldasmPortablePdbLineOracleUnavailable` gap or issue #769. It records a
pinned scan baseline, not source/metadata/PDB/IL identity-edge validation.

The runner is `scripts/validation/Invoke-Task11Windows.ps1`. Its output tree,
including `private-receipt.json`, logs, TRX files, and scan artifacts, is
private. Put it outside the repository, keep it out of CI artifacts, and do
not attach it to a PR. No source excerpts or secret material belong in a
shareable derivative. A shareable summary would need its own documented
privacy projection, exact generator SHA-256, and SHA-256 of that projected
input; this lane generates no such summary.

## Stages and invocation

All invocations require a clean TraceMap checkout at the exact 40-character
commit passed as `-TraceMapCommit` and a fresh `-OutputRoot`. Paths are
canonicalized and may not overlap either checkout. An existing output tree is
rejected, including one created by a failed run. If preflight rejects the
output before creating it, the runner attempts a separate fresh,
non-overlapping local temporary receipt and includes its path in the error.
The `FullCorpus` lane requires
`-EnableFullCorpus` and `-BoundedReceiptPath` pointing to a previously passed
bounded receipt for the same exact commits and an exact tracked-file selection
within the lane's hard limits. The runner rechecks that selection against the
pinned clean corpus before running the full lane; old wildcard-based bounded
receipts cannot authorize it. It writes `kind: FullCorpus` and
uses its own output tree; it cannot reuse or masquerade as a bounded receipt.

The public smoke uses only a checked-in script's tiny public ILAsm source and
the Windows Framework ILAsm. It does not open the historical checkout:

```powershell
pwsh -NoProfile -File scripts/validation/Invoke-Task11Windows.ps1 `
  -Lane PublicSmoke -TraceMapRoot <clean-tracemap-checkout> `
  -TraceMapCommit <exact-tracemap-sha> -OutputRoot <new-private-output-dir>
```

The bounded private invocation adds `-AuthorizedCorpus`, `-CorpusRoot`, and
`-CorpusRemoteSha256`. The remote digest is SHA-256 of the existing checkout's
`git remote get-url origin` UTF-8 bytes, with no newline. Compute it locally;
the runner compares it without writing the remote to the receipt. This is an
operator attestation of authorized access, not a way to acquire access. The
checkout must already contain the exact pinned corpus commit and have no
tracked or untracked changes. The runner never fetches it or searches for
credentials. It also requires explicit paths to the installed Visual Studio,
MSBuild, test runner, ILAsm, ILDAsm, and Framework assemblies and records their
versions or fails preflight.

`-CorpusProfile` pins the corpus commit and bounded file limit as one contract:

| Profile | Pinned commit | File limit | Purpose |
| --- | --- | ---: | --- |
| `HistoricalMaster` (default) | `db8c3359badfec620ccdc6df062b1756ef9607f8` | 256 | #769 historical corpus |
| `BuildableFix` | `642bdaede0b97a400c24266e30670ed5c1c98689` | 427 | Distinct Windows build/scan baseline |

Both profiles retain the 64 MiB source-byte and 4,096 candidate-entry limits.
The fix profile requires a complete eligible inventory; selecting only 256
files is not a passing bounded run. Its receipt records the profile, exact
commit, effective limits, complete exact-file selection, and exact runner
SHA-256. A `FullCorpus`
invocation must use a passed bounded receipt from the same profile and limits;
old, cross-profile, or different-runner receipts fail closed. The fix profile does not change
the #769 historical pin or claim the build and tests have passed. Select it
explicitly with `-CorpusProfile BuildableFix` and keep all private arguments,
outputs, and receipts local.

Supply the smallest viable historical project as `-SliceProject` (absolute or
relative to the corpus root) and its resulting test assembly as
`-TestAssembly` (absolute or relative to the fresh slice build output).
Declare exactly one concrete test
per category using `-RepresentativeCases` entries of the form
`category=Fully.Qualified.TestName`: `branch`, `switch`, `exception-region`,
`leave`, `instrumentation`, `nested-generic`, and `duplicate-identity`. These
names are deliberately supplied from the authorized pinned checkout rather
than committed into this public repository. A zero-test TRX fails the stage.
Declare relative `-BoundedPaths` as exact tracked source and project **files**.
For a bounded run, this selection must equal the scanner's complete eligible
inventory for the pinned checkout. Unsupported selections and omitted eligible
files fail closed. Directories and globs (including `*`) are rejected:
the scanner's literal directory include would otherwise admit a whole subtree
under a `Bounded` receipt. The runner rejects duplicate, missing, and reparse
point selections and applies the selected profile's file limit and 64 MiB of
selected source
bytes before building or scanning. The bounded scan passes each admitted file
as `--include` and enables the scanner's exact-source-scope check; the
full-corpus lane omits them only with its explicit opt-in. The scanner checks
inventory equality and limits before semantic extraction, then rejects any
newly discovered local semantic input outside that inventory before hashing
the authoritative source snapshot. The runner counts candidate directory/file
entries using the scanner's enumeration exclusions before the private build and
again before the scan, with a 4,096-entry limit for either profile. Exceeding it is
an explicit non-passing error, not a complete inventory. These caps bound
repository source inputs retained by the scanner, not external SDK/package imports, MSBuild's process
memory, or the representative test run. A project-load gap produces a
non-passing `partial` receipt and cannot authorize the full lane.

The order is: preflight; independent public ILAsm smoke; smallest project
rebuild; each named test; non-incremental Release TraceMap CLI build; pinned
scan; required artifact and provenance checks. Any failure after safe output
creation stops the chain and writes a
private receipt with the exact command arguments, working directories, exit
codes, captured output, stage results, selected tests, versions, paths, and
blocker. The scan checks `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
`report.md`, and `logs/analyzer.log`; commit, scanner version, source snapshot
digest, nonempty facts, registered rule IDs, evidence tiers, extractor IDs and
versions, and explicit manifest/fact gaps. The analyzer log may legitimately be
empty; its presence is required. The required index must pass SQLite
integrity, schema, commit, and fact-count checks. The receipt retains the exact
CLI DLL SHA-256 and separately records a deterministic SHA-256 of the complete
built CLI output payload; a later full run must reproduce both digests before
scanning. The source snapshot digest is the local bounded-input digest;
it must never be republished from a private scan.

Test the public guards without the historical corpus:

```powershell
pwsh -NoProfile -File scripts/validation/Test-Task11Windows.ps1
```

The synthetic test covers the unchanged historical profile, complete 427-file
fix-profile admission, 428-file rejection, ignored generated input rejection,
cross-profile receipt rejection, exact bounded file/count/byte limits, exact rule-ID
registration, wrong commit, dirty or missing checkout, missing tool, missing
artifact, invalid provenance, and attempted output reuse and overlap. The
extended public Windows workflow runs this test and `PublicSmoke`
without access to the historical corpus. Default CI remains independent of
the historical corpus.

## C++/CLI feasibility inventory

| CLR shape | Added evidence beyond public C#/VB/F#/ILAsm fixtures | Current lane |
| --- | --- | --- |
| `modreq`/`modopt` signatures | C++/CLI syntax can generate realistic modifiers, but public ILAsm already covers exact metadata shape and operand preservation. | Reproduce with ILAsm; no C++/CLI dependency. |
| Tracking references (`%`, `^`) | Compiler lowering and managed handle usage could add language-specific provenance beyond equivalent managed ref/byref IL. | Defer until C++/CLI compiler is present. |
| Native interop and IJW transitions | Mixed native/managed method bodies and native import boundaries are distinct from ordinary P/Invoke declarations. | Unsupported input; require explicit gap. |
| Mixed-mode metadata | PE/CLR coexistence and native sections are not equivalent to a pure managed ILAsm assembly. | Unsupported input; require explicit gap. |
| Unusual value types and explicit layout | Pure managed layout, byref fields, and modifiers can be constructed in public ILAsm. Compiler-specific layout emission would add a separate oracle. | Public ILAsm first; C++/CLI comparison deferred. |

On the 2026-09-22 Windows host, Visual Studio 2022 Enterprise 17.10.35201.131,
.NET SDK 10.0.303, Framework ILAsm 4.8.9221.0, and SDK ILDAsm 4.8.3928.0
were found. The C++/CLI component query through `vswhere -requires
Microsoft.VisualStudio.Component.VC.CLI.Support` and `cl.exe` discovery both
returned no tool. This is a feasibility inventory, not a C++/CLI execution
claim. Do not add mixed-mode scanner support to satisfy this lane.
