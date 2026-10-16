# Validate the public VB compilation-context gate on Windows

Validate the test/CI coverage change on `codex/evidence-admission-hardening`.
Do not rescan the private application, create a project for it, install framework
references, execute application code, or rerun the full solution suite.

1. Record `git status --short`, branch and full HEAD. Fetch origin and verify the
   intended branch is current without discarding or stashing unrelated work.
   If the checkout differs or is dirty in relevant paths, stop and report it.
2. Read `docs/validation/VB_COMPILATION_CONTEXT_MATRIX.md`. Build the test project
   once: `dotnet build src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --nologo`.
   Bound the build to eight minutes. Missing prerequisites: report, do not install.
3. Run the exact test command from the `Compilation context and interaction gate`
   step of `.github/workflows/vb-compilation-context-matrix.yml`, once. Bound the
   process to ten minutes; retain the two-minute test-host hang timeout. On timeout
   stop only processes owned by this run and report blocked, not passed.
4. Report exact HEAD, SDK/OS, build warnings/errors, passed/failed/skipped counts,
   elapsed time and any failing test names. No raw private paths/source or logs in
   the shareable reply. Do not retry automatically or launch background test runs.

Acceptance requires all selected tests passing with zero skipped. The 36 new
cases must retain the projectless semantic gap and avoid Tier1 promotion despite
configured imports/references. Existing semantic and path interaction tests must
also pass. These are public synthetic boundaries, not proof of private application
compatibility or .NET Framework execution. Preserve the last accepted private scan
unchanged: this commit claims no reduction in its gaps or unresolved calls.
