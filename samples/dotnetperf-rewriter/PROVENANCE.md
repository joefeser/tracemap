# dotnetperf oracle corpus provenance

This directory is a vendor import of the **dotnetperf** project (NetPerf), a
production IL-rewriting profiler built on Mono.Cecil, preserved as a real-world
oracle corpus for TraceMap's IL rewrite evidence lanes.

| | |
| --- | --- |
| Origin | `bitbucket.org:metadatadashboard/dotnetperf.git` (private history; a private staging mirror exists at `github.com/joefeser/dotnetperf`) |
| Pinned commit | `db8c3359badfec620ccdc6df062b1756ef9607f8` (2014-06-28, "README.md edited online with Bitbucket") |
| History | 560 commits, 2010-10-25 → 2014-06-28 |
| Original authors | Joe Feser and Stephen Ward (authorship across the `MAINDEVSERVER\joe`, `MAINDEVSERVER\stephen`, `Stephen Ward <spworwell@gmail.com>` and `Administrator` identities; roughly 232 / 276 commits respectively) |
| License | Apache License 2.0 — the license of this repository (see root `LICENSE`) |
| Superseded license | The historical `DotNetPerfEULA` commercial license is superseded in its entirety by Apache 2.0 for this import and is deliberately not included |

## Why only the pinned tree

Full git history is **not** imported and must not be: a history scan (2026-10-14,
recorded on tracemap issue #856) found approximately eight strong-name private
keys (per-project `.snk` files and a vendored `mono.snk`), commercial license
strings (CryptoLicensing, SmartAssembly) and a vendor invoice URL with an
embedded password in historical revisions of the setup documentation. History
remains available in the private staging repository for audit.

## Exclusions applied to the pinned tree

- `src/NetPerfKey.snk` — strong-name private key (treat the key as burned; it
  exists in the private history)
- `Licensing/` — LogicNP CryptoLicensing generator and the `.netlicproj`
  license-generation project (commercial tooling; possible customer material)
- `Libraries/` — vendored third-party binaries of mixed licenses, including
  commercial components (LogicNP, WyBuild); if a revived build needs them they
  return as package references, never vendored DLLs
- `External Tools/ilasm.exe` — Microsoft binary, not redistributable
- `Ants/*.app6` — RedGate ANTS profiler session files (third-party product artifacts)
- `Documentation/licensing strategy.txt`, `Documentation/ReleaseBuilds/`,
  `Documentation/latestversion.xml` — commercial-era business material
- `Documentation/README_setup_instructions.txt` and
  `src/README_setup_instructions.txt` — contained commercial license strings
  and a vendor invoice URL with an embedded password
- `Documentation/DotNetPerfEULA.{html,rtf}` — superseded commercial EULA
- `Documentation/azure.txt` — a 2011 Azure staging-deployment dump containing a
  storage-account connection string with an access key (caught by GitHub push
  protection on first import attempt; treat the `netperftest` account key as
  burned and rotate or confirm-dead)
- `Documentation/Errata/Ecma-335.pdf` — the ECMA-335 specification PDF itself:
  third-party copyrighted standard material outside the co-author release
  authorization ("your code and mine"); freely and permanently available from
  ecma-international.org, so exclusion costs nothing
- `src/NetPerf.UI/Infrastructure/Reflector.cs` — copied source from the
  separate "Front-End for DOSBox" project (`code.google.com/p/fed/`, per its
  own header), used for a Vista-style folder dialog; third-party source outside
  the co-author authorization. Removing it leaves `NetPerf.UI` uncompilable
  as-pinned; a revival replaces it with a supported folder-dialog API. A
  header-attribution sweep confirmed this is the only foreign-origin source
  file in the tree.
- `src/.nuget/` — the solution NuGet bootstrap: the third-party `NuGet.exe`
  binary, stock Microsoft `NuGet.targets` MSBuild integration, and the default
  template `NuGet.Config` (nothing first-party; flagged independently by the
  owner and by review)
- `src/PostSharp.Custom.targets` — contained a plaintext PostSharp license key
  (caught by review after both the import sweep and push protection missed it;
  treat the PostSharp key as burned; the file only wired the already-excluded
  vendored PostSharp binaries)

## Notice retention

In-file copyright notices (e.g. `CommonAssemblyInfo.cs`: "Copyright ©
2010-2013 Joe Feser and Stephen Ward") are retained verbatim per Apache-2.0
section 4; the Apache grant applies on top of, not instead of, the authors'
existing notices.

## Retained first-party material in `Documentation/Errata`

The `Documentation/Errata/` recordings (`.m4a`) and their accompanying notes
are co-author Stephen Ward's own design-thinking materials, walking through
the CLI errata problem areas (event support, tail-call handling, property
support) — owner-confirmed 2026-10-15 and retained under the release
authorization. Only the third-party `Ecma-335.pdf` is excluded from that
folder.

## Corpus byte pin

`CORPUS-SHA256.txt` records the SHA-256 of exactly the **405 vendored files**.
The provenance record, the manifest and `verify_corpus.py` are repository
metadata and sit deliberately OUTSIDE the pin, so metadata edits never
invalidate it. Any byte divergence in the vendored corpus — regeneration,
accidental edit, corpus swap — fails verification, tying oracle conclusions
to the exact imported bytes. (The golden suites were generated by the
project's own 2010-era tooling whose binary is not imported; the content hash
of the suite files is the durable pin.)

Verify with either mode:

```sh
python3 samples/dotnetperf-rewriter/verify_corpus.py              # committed objects (line-ending-proof)
python3 samples/dotnetperf-rewriter/verify_corpus.py --worktree   # working tree (tracked files only)
```

The committed-objects mode reads blobs via `git cat-file` and is immune to
`core.autocrlf` checkout transforms; the worktree mode hashes tracked files
as they sit on disk (untracked junk never fails it) and falls back to plain
directory enumeration when no git repository is present — so an exported
archive of this directory verifies standalone with `--worktree`.

## Pin deviations via this repository's ignore rules

17 files present at the pinned commit are excluded by this repository's
`.gitignore` rather than by the authorization scope: 15 Visual SourceSafe
source-control binding files (`.vspscc` / `.vssscc`) and 2 test-assembly
`.pdb` files. No source files are excluded by this mechanism; the pinned
source tree is otherwise byte-complete.

## Authorization

Co-author release approval, in writing:

> **From:** Stephen Ward \<spworwell@gmail.com\>
> **To:** Joe Feser \<joseph.feser@gmail.com\>
> **Date:** Tue, 13 Oct 2026 05:39:02 -0400
> **Message-ID:** \<CACi46xJJH0s+J+v2sh_gNB0mKO3XQmx7DLRT=Dmwbw342Y9AvQ@mail.gmail.com\>
> **Subject:** Re: dotnetperf going open source — need your one-line OK
>
> "I co-authored the dotnetperf project
> (bitbucket.org/metadatadashboard/dotnetperf) and approve its release as open
> source under the Apache License 2.0, including import into the public
> tracemap repository with original authorship credited."

The approved release scope (quoted in the authorizing reply thread) explicitly
names these exclusions: "Not included: the vendored third-party libraries
(Autofac, Moq, the commercial licensing tooling, etc.), the ANTS session files,
the strong-name key, and the old business docs. Basically your code and mine,
nothing belonging to anyone else."

## Oracle role

`src/NetPerf/ILInstrumenter.cs` (SimplifyMacros-then-insert rewriting with
branch/switch jump-target fixup), `src/NetPerf/ILValidator.cs`, and the
generated golden suites (`SwitchInstrTest.cs`, `SingleTargetBranchesTest.cs`,
`ProtectedBlocksTest.cs`, `LeaveInstrTest.cs` — ~264k lines of exact
before/after IL assertions) form an oracle for `IlRewriteEvidenceExtractor`
coverage: if TraceMap decodes what the era's own rewriter accepted and its
validator verified, that region of ECMA-335 is covered by test, not by claim.
Wiring the oracle into the evidence lanes is tracked on issue #856.
