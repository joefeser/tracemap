# Access Parallels Source Runner Requirements

## Goal

Provide a source-only, deterministic way for a macOS TraceMap checkout to
build and validate the existing Microsoft Access workflow inside an isolated
local Parallels Windows VM. The runner is orchestration only. It must not add
or broaden Access extraction.

## Requirements

1. The host runner must fail closed unless the selected VM is running with
   every configured network adapter disabled and exactly the established
   scoped input/output shares present with read-only/read-write modes
   respectively.
2. The guest runner must use a guest-local repository, offline .NET SDK, Git,
   and package cache. It must reject a dirty checkout, an unexpected commit, or
   any configured Git remote.
3. The runner must support bounded `doctor`, `build`, and `synthetic`
   operations without requiring Codex or network access inside Windows.
4. `build` must build the .NET solution from source and run the focused Access
   tests.
5. `synthetic` must delegate to the checked-in Access smoke harness and retain
   only its sanitized local review bundle and allowlisted checkpoint evidence.
6. Host-visible output must be categorical and must not expose guest paths,
   database names, object identities, SQL, VBA, macros, connections,
   credentials, or raw tool output.
7. The runner must not enable networking, alter VM sharing, launch a
   representative database, or add form/report, VBA, or macro extraction.
8. Guest checks must pin the validated Git/.NET launchers, reject required
   reparse-point path chains, and label guest identity/toolchain results as
   attestations rather than independent host proof.

## Non-claims

The runner does not prove that a representative database is correct, safe,
compatible, operational, or approved. It does not inspect rows, execute
queries, render forms/reports, execute VBA/macros, or replace explicit
representative-input authorization.
