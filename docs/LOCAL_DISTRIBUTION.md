# TraceMap Local Distribution Evidence

Status: candidate evaluation; no public distribution is selected or published.

TraceMap must run outside a source checkout before it can claim a packaged
local workflow. This record separates observed package behavior from proposed
host support. Package hashes prove the bytes of one build; they do not prove
publisher identity, authority, or authenticity.

## Candidate Matrix

| Candidate | Runtime | Install / upgrade / remove | Offline shape | Native dependencies | Determinism boundary | Current evidence |
|---|---|---|---|---|---|---|
| .NET tool | Compatible .NET 10 SDK/runtime | Built-in `dotnet tool` lifecycle | Local NuGet source after package creation | Package contains the supported SQLite runtime assets | Tool payload files must be stable; the NuGet container carries generated relationship/core-property identifiers and therefore gets a per-build integrity hash | macOS arm64 install/upgrade/guided-run/uninstall probe passed; Windows and Linux CI pending |
| Framework-dependent archive | Compatible .NET 10 runtime | Extract, invoke with `dotnet`, remove directory | Copyable archive | Runtime assets must be retained in the archive | Archive construction and extracted payload require separate proof | macOS arm64 version/readiness probe passed; Windows and Linux CI pending |
| Self-contained archive | None beyond supported host | Extract, invoke native launcher, remove directory | Per-RID archive | One platform-specific SQLite runtime plus .NET runtime | Each RID requires independent content and host proof | macOS arm64 `osx-arm64` version/readiness probe passed; Windows and Linux CI pending |
| Offline container | Compatible container runtime | Load, run, remove image | OCI archive with no-network execution | Image-specific native assets | Image manifest/layers and mounted output require proof | Not yet probed |

The .NET tool is the leading hypothesis because it provides a conventional
`tracemap` command and explicit install, upgrade, and uninstall operations. It
is not selected until the claimed Windows, macOS, and Linux probes pass.

Tool packaging is refused unless Git reports a clean source tree. The compiled
version contract records `sourceState` as `clean`, `dirty`, or `unavailable` so
a source build cannot silently present its commit as the complete identity of
different uncommitted bytes.

## 2026-08-13 macOS arm64 Probe

Observed against the #666 implementation worktree:

- `dotnet pack` produced `TraceMap.Tool` version `0.1.0` as an opt-in probe;
- the package was 29,261,164 bytes;
- package inspection found the TraceMap assemblies, Roslyn/MSBuild build hosts,
  and SQLite native assets for supported runtime identifiers;
- installation from a local package directory succeeded;
- `tracemap version --json` ran outside the source checkout and reported
  `distributionKind: dotnet-tool`, `sourceState: clean`, and ready Git/MSBuild
  observations;
- a dirty-source packaging attempt failed with
  `TraceMapToolPackageRequiresCleanSource` rather than assigning the last
  commit identity to uncommitted bytes;
- an empty committed synthetic repository scanned outside the source checkout,
  emitted the normal artifacts, and reported two facts with syntax coverage;
- uninstall succeeded and removed the tool command from the isolated tool path.

Two same-source package builds had different outer `.nupkg` hashes because
NuGet generated different OPC relationship/core-property identifiers. A
recursive comparison showed the tool payload itself was byte-identical; only
`_rels/.rels` and the generated core-properties entry differed. Therefore:

- every produced package needs its own integrity hash;
- this probe does not claim byte-reproducible NuGet containers;
- payload determinism and package-container determinism remain distinct gates;
- no signing or publisher-authenticity claim is made.

## Remaining Gates

- run equivalent install/version/scan/uninstall probes on Windows and Linux;
- test upgrade behavior between two explicit local package versions;
- compare framework-dependent, self-contained, and offline/container results;
- inspect package content against a committed allowlist and size budget;
- decide whether payload-stable/per-build-hash NuGet packaging satisfies the
  release policy or whether a reproducible archive is required;
- publish install instructions only for hosts with matching evidence.

The committed `scripts/smoke-local-distribution.ps1` probe performs package
content and size checks, local-feed install, a guided synthetic scan outside
the checkout working directory, explicit-version upgrade, uninstall,
framework-dependent publication, and host-RID self-contained publication. Its
sanitized `local-distribution-smoke.v1` receipt contains no package feed or
absolute path. The `Local distribution validation` workflow runs the same
probe on Windows, Ubuntu, and macOS; host support must remain unverified until
those exact CI jobs pass.
