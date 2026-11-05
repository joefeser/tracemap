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
