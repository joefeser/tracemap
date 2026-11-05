# Read and run a complete public workspace locally

This lab generates a public synthetic website, dependency source/DLL, loose SQL,
wizard state, a verified workspace, reports and nonempty SQLite outputs. It also
measures ordinary SQL size boundaries without increasing limits. No work inputs,
database connection or SQL execution are involved.

From this TraceMap checkout, with Python 3, .NET 10, Git and PowerShell 7:

```powershell
python scripts/tests/New-WorkspaceLocalLab.py --output-root C:/reviews/tracemap-public-lab
```

Use `python3` where that is the Python command. Choose a fresh folder; an existing
lab is refused and preserved. Open its `README.md` first. Everything needed to
inspect that run is inside the selected folder:

- `example/workspace/`: actual root/project/answer/SQL/workspace configurations,
  SQL input, completed reports and SQLite evidence.
- `example/web`, `dependency`, `published`: authored source and built fixture.
- `tool/`: frozen tested tool copy for continuing the public example.
- `size-cases/`: exact boundary files, manifests and observed pass/refusal logs.
- `Continue-Wizard.ps1`, `Run-Size-Cases.ps1`, `Inspect-SqlInputs.ps1`: readable
  scripts for continuation, measured boundaries and local size/path inspection.
- `Rebuild-Public-Example.ps1`: regenerate a fresh public lab using an explicit
  TraceMap checkout on another host; generated wizard paths must not be relocated.

The ordinary definition path permits 262144 bytes per SQL file and 1048576 bytes
in total; the input manifest has a separate 4194304-byte cap. The lab verifies
exact-limit success and one-byte-over failure, plus aggregate-limit success and
failure. A large export is not silently truncated or selected. The definition
selection contract has separate limits and requires explicit selections.

The inspector prints local paths, existence and byte counts, not SQL contents or
hashes. It does not replace scanner validation and does not send anything. Public
lab success does not diagnose the owner's private file or establish Windows/IIS,
runtime, SQL binding or migration acceptance. Evidence remains explicitly partial.

## Sibling repos and a .NET 2 dependency

The terminal replay also supports this explicit topology:

```powershell
python scripts/tests/Test-WorkspaceWizardLauncher.py --output-root C:/reviews/tracemap-layout-copy --legacy-layout
```

It creates sibling `source code/web project repo` and `source code/dll project repo`,
a relative `..\dll project repo\Dependency.csproj` reference, a genuine net20
dependency, separate `publish/web project compiled`, a local TraceMap checkout
where commands execute, and `tracemap-output` with manifest/SQL/project/runs.
The web build target defaults explicitly to net472; use `--legacy-web-target` for
a different supported synthetic target. The owner's web target was not specified.
The web folder is selected as a projectless website despite its build helper.
The .NET Framework reference assemblies enable synthetic compilation on this host;
this remains a managed publication with authored maps, not aspnet_compiler/IIS
acceptance. The local tool checkout shares public Git objects with its source
checkout; regenerate the copy on another host instead of relocating its configs.
