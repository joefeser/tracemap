# Microsoft Access Parallels Source Runner

The Parallels source runner lets a macOS TraceMap checkout invoke bounded
source build and synthetic Access validation inside an isolated local Windows
VM. Codex and network access are not required inside the guest.

This workflow orchestrates the existing Access adapter and validation harness.
It adds no extraction capability.

## Boundary

The host runner refuses to continue unless:

- the selected VM is running;
- its `net0` device is disabled;
- a scoped `access_input` share is read-only;
- a scoped `access_output` share is read/write.

The guest runner refuses to continue unless:

- the source checkout is at the explicitly supplied commit SHA;
- the checkout is clean;
- it has no Git remote;
- Microsoft Access is registered;
- the offline .NET and Git tools are present.

Neither script changes VM settings. Host-visible output is a single
categorical status line. Raw build and harness output is suppressed.

## Guest-local source setup

Provision source and tools outside the repository. The validated ARM64 setup
uses:

- .NET SDK 10.0.302 for Windows ARM64;
- MinGit 2.55.0.3 for Windows ARM64;
- Git LFS 3.7.1 for Windows ARM64;
- a local NuGet feed containing the solution's locked dependency packages;
- a complete Git bundle created from the exact branch to validate.

Verify downloaded tool hashes against their official release metadata before
placing them on the read-only input share. Extract them into a guest-local
tool root, clone the Git bundle into a guest-local checkout, remove the bundle
remote, and restore only from the offline NuGet feed.

The runner expects this default guest-local shape:

```text
C:\TraceMapDev\
  tools\
    dotnet\dotnet.exe
    mingit\cmd\git.exe
  packages\
  tracemap\
```

Tool archives, package caches, Git bundles, databases, scan output, and local
review bundles are operator-local. Do not commit them.

## Run from macOS

PowerShell 7 and Parallels `prlctl` must be available on the host.

```powershell
./scripts/access-validation/Invoke-AccessParallelsSource.ps1 `
  -Action doctor `
  -ExpectedHead <40-character-commit-sha>

./scripts/access-validation/Invoke-AccessParallelsSource.ps1 `
  -Action build `
  -ExpectedHead <40-character-commit-sha>

./scripts/access-validation/Invoke-AccessParallelsSource.ps1 `
  -Action synthetic `
  -ExpectedHead <40-character-commit-sha>
```

Override `-VmName` and `-GuestRoot` only when the VM uses different
operator-local names. Those values are never included in successful output.

## Representative databases

This source runner intentionally does not accept a representative database.
Use `Invoke-AccessRepresentativeSmoke.ps1` only after the owner identifies the
local file and explicitly authorizes that input. Keep the representative
database, its path, and raw outputs inside the established isolated workflow.

## Limitations

The workflow does not inspect rows, execute queries, render forms or reports,
execute VBA or macros, prove runtime behavior, or approve a release. A
successful source build and synthetic smoke proves only the bounded contracts
reported by those operations at the supplied commit.
