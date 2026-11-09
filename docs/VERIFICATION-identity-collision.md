# Verification — one project, one identity (ci-workflow 0.4.7)

The fix: a physical project claimed with two package identities (a `-p:PackageId`
step's declared id plus the filename default from another claim source) used to board
TWO registry producers. Declared evidence now outranks the filename default; the
suppressed claim demotes to a typed `ci-producer-identity-conflict` gap.

## Run order (work machine)

```powershell
# 1) tracemap — this branch, rebuilt, PROVEN fresh (a stale dll re-scans nothing and lies)
cd C:\tracemap
git status                    # must be clean; stash first if not
git fetch origin
git checkout fix/ci-identity-collision
dotnet build src/dotnet/TraceMap.sln -c Release
Get-Item src/dotnet/TraceMap.Cli/bin/Release/net10.0/tracemap.dll | Select-Object LastWriteTime

# 2) full estate rescan — worktree mode; the dll hash changed, so a full pass is EXPECTED
#    (your existing scan-estate runbook command, unchanged)
*> C:\ua-estate\scan-run-log.txt   # capture all streams

# 3) vintage census (expect 0) + registry (your existing runbook steps)

# 4) THE GLANCE — the whole verification is one look at the registry:
#    the multi-identity repo's app package appears ONCE (the declared id),
#    not twice; and the run log carries the named suppression:
Select-String C:\ua-estate\scan-run-log.txt -Pattern 'identity-conflict'
```

Expected: the duplicate package is gone from `boundary.internalPackages`; the scan log
names every suppression in a `ci-producer-identity-conflict` gap (kept X (declared);
suppressed Y (derived-default)) — never silence. Same-tier declared variance across
workflows still passes through as evidence (deliberate, unchanged).
