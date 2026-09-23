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
rejected, including one created by a failed run. The `FullCorpus` lane requires
`-EnableFullCorpus` and `-BoundedReceiptPath` pointing to a previously passed
bounded receipt for the same exact commits. It writes `kind: FullCorpus` and
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

Supply the smallest viable historical project as `-SliceProject` and its
resulting test assembly as `-TestAssembly`. Declare exactly one concrete test
per category using `-RepresentativeCases` entries of the form
`category=Fully.Qualified.TestName`: `branch`, `switch`, `exception-region`,
`leave`, `instrumentation`, `nested-generic`, and `duplicate-identity`. These
names are deliberately supplied from the authorized pinned checkout rather
than committed into this public repository. A zero-test TRX fails the stage.
Declare relative `-BoundedPaths` globs for only the sources and project files
in the selected scan slice. The bounded scan passes these as `--include`; the
full-corpus lane omits them only with its explicit opt-in.

The order is: preflight; independent public ILAsm smoke; smallest project
build; each named test; Release TraceMap CLI build; pinned scan; required
artifact and provenance checks. Any failure stops the chain and writes a
private receipt with the exact command arguments, working directories, exit
codes, captured output, stage results, selected tests, versions, paths, and
blocker. The scan checks `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
`report.md`, and `logs/analyzer.log`; commit, scanner version, source snapshot
digest, nonempty facts, registered rule IDs, evidence tiers, extractor IDs and
versions, and explicit manifest/fact gaps. It records the SHA-256 of the
invoked CLI DLL. The source snapshot digest is the local bounded-input digest;
it must never be republished from a private scan.

Test the public guards without the historical corpus:

```powershell
pwsh -NoProfile -File scripts/validation/Test-Task11Windows.ps1
```

The synthetic test covers wrong commit, dirty or missing checkout, missing
tool, missing artifact, invalid provenance, and attempted output reuse and
overlap. The extended public Windows workflow runs this test and `PublicSmoke`
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
