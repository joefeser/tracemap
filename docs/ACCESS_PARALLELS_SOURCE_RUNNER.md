# Microsoft Access Parallels Source Runner

The Parallels source runner lets a macOS TraceMap checkout invoke bounded
source build and synthetic Access validation inside an isolated local Windows
VM. Codex and network access are not required inside the guest.

This workflow orchestrates the existing Access adapter and validation harness.
The `metadata` action additionally validates the separately bounded synthetic
form/report metadata producer. It does not widen representative scanning.

## Host-verified boundary

The host runner refuses to continue unless:

- the selected VM is running;
- every configured network adapter is disabled;
- the only enabled host shares are a read-only `access_input` share and a
  read/write `access_output` share, and both target the expected canonical host
  directories;
- the guest emits the exact ordered categorical fields for the requested
  action, with no additional output.

## Guest-attested boundary

The guest runner checks and attests that:

- the source checkout is at the explicitly supplied commit SHA;
- the checkout is clean;
- it has no Git remote;
- Microsoft Access is registered;
- the pinned offline .NET and Git launchers match their expected SHA-256
  digests and required path chains contain no reparse points;
- the offline NuGet package cache and synthetic output parent directories are
  guest-local and have reparse-free paths;
- freshly rebuilt CLI executables do not change during synthetic validation.

These guest attestations detect accidental or local tool/source substitution;
they are not independent host attestation of an uncompromised Windows kernel,
PowerShell runtime, complete .NET SDK tree, or Parallels Tools service. Neither
script changes VM settings. Host-visible output is a single categorical status
line. Raw build and harness output is suppressed.

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

The default runner pins the validated Windows ARM64 launchers:

- MinGit 2.55.0.3 `git.exe` SHA-256:
  `b05b2d7eb80933c602272b5ddf132adf288cf78ad8e32a7a47ca7e200076b9f3`
- .NET SDK 10.0.302 `dotnet.exe` SHA-256:
  `05602a1b5eff9cd0be076c25ac9ab31c5e2f76df824a35b8bc9a16ab340767b6`

An operator may supply different expected digests only when deliberately
validating another separately verified toolchain.

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

./scripts/access-validation/Invoke-AccessParallelsSource.ps1 `
  -Action metadata `
  -ExpectedHead <40-character-commit-sha>
```

The metadata action creates only the checked-in synthetic zero-row fixture,
serializes saved form/report definitions with invisible force-disabled
`SaveAsText`, verifies unchanged loaded state/source hashes and clear canaries,
then deletes the protected bundle and all scratch. No protected output crosses
the host runner.

Override `-VmName` and `-GuestRoot` only when the VM uses different
operator-local names. The default expected share targets are
`$HOME/AccessAnalysis/input` and `$HOME/AccessAnalysis/output`; pass
`-ExpectedInputSharePath` and `-ExpectedOutputSharePath` when the two scoped
directories deliberately live elsewhere. Override tool hashes only after
separately verifying the replacement binaries. Those values are never included
in successful output.

## Representative databases

This source runner intentionally does not accept a representative database.
Use `Invoke-AccessRepresentativeSmoke.ps1` only after the owner identifies the
local file and explicitly authorizes that input. Keep the representative
database, its path, and raw outputs inside the established isolated workflow.

## Limitations

The workflow does not inspect rows, execute queries, render forms or reports,
execute VBA or macros, prove runtime behavior, or approve a release. A
successful source build and synthetic smoke proves only the bounded contracts
reported by those operations at the supplied commit. Guest-side identity and
toolchain checks are not hardware-backed or independently re-proven by the
host.
