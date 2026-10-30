# Bounded scan diagnostics

This is operational containment and phase identification, not a fix or causal
attribution for the reported 0.2.22 memory regression. The unguarded InfoMessage
run protocol remains suspended. No SQL/provider/application execution is added.

## Live phases

Ordinary `scan --diagnostic-progress <fresh-file>` uses the existing bounded
`tracemap-scan-progress/v1` reporter. Choose an existing directory outside the
source repository and authorized scan output. The checkpoint, `.performance.json`
and `.binding.json` sidecars must all be fresh. Existing files are never reused.
The binding contains exact CLI/Core generator hashes and a hash of a bounded
privacy projection (option booleans/counts only; private values/paths are neither
stored nor hashed). It does not bind the full private invocation or prove success.

The checkpoint is created before ScanEngine, printed immediately to stderr and
updated atomically with a two-second heartbeat. Closed categorical stages now
include source-semantic-analysis (with existing project/compilation substages),
compiled-metadata, il-decode, il-materialization, il-lifecycle,
il-callback-inspection and il-result-naming. A stopped process leaves its last
checkpoint. An observed `completed` phase means that operation returned, not
that SQL lineage, application execution or whole-scan acceptance was proven.
No symbols, source, SQL, command values or paths enter progress stage names.
Diagnostic events do not change facts; byte-identical facts are tested.

## Guard

Run `scripts/Invoke-BoundedScan.ps1` from PowerShell 7, never dot-source it. It
spawns a dedicated guard worker; the interactive shell is not placed in a job.
Production runs require Windows. Before starting the scanner, the worker creates
and joins a job with no breakaway flags, kill-on-close, 16 active processes and an
aggregate committed-memory limit (default 2048 MiB). Job setup failure launches
no scanner. The handle is rooted until worker exit so remaining job children are
terminated even after the scanner root exits.

The worker also samples aggregate job working sets every 250 ms and stops when
that sum exceeds the configured MiB limit. The OS hard limit is **committed
memory**, not working set/RSS; sampled working sets may overshoot between polls
and shared pages can be counted more than once. These are distinct metrics.
Already running unrelated processes/servers and non-inheriting external services
are not members of this job. This is not an adversarial sandbox.

[Windows job inheritance and cleanup](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)
and [job memory limits](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_extended_limit_information)
define these OS semantics. The implementation requires 64-bit Windows; unsupported
platform/job assignment conditions fail closed without starting a private scan.

Default wall budget is 60 seconds, including worker setup; an outer timeout at
budget + 15 seconds bounds a wedged worker. The guard requires a fresh phase
checkpoint argument. Missing or stale phase output after ten seconds also stops
the child. Timeout/stale/memory/child failures are incomplete operations; limits
must not be raised or automatically retried. A zero child exit still does not
prove a completed scan: require all native artifacts and valid scan status.

Guard checkpoints use `bounded-scan-guard/1`, an exact script SHA-256 and a SHA-256
of their bounded privacy-projected configuration, with wall time, PIDs and memory
metrics. They never store/hash private argument values and are not evidence facts.
Only caller-selected fresh checkpoints are written. Application arguments are
passed with ProcessStartInfo.ArgumentList, not shell interpolation. The private
request is transported to the dedicated worker in its environment, removed before
launching the scanner, and never logged.

## Public self-test gate

```
pwsh -NoProfile -File scripts/Test-BoundedScanGuard.ps1
```

Five synthetic controls check success, timeout, child memory growth, parent exit
with an orphaned descendant, and stale phase checkpoints, including cleanup and
checkpoint retention. Artifacts stay in an OS temporary directory. On macOS/Linux,
only the repository public worker can use sampled-test mode; production fails.
The Windows job interop compiles on macOS, but **runtime OS enforcement is not
claimed tested there**. The exact public self-test must pass on Windows before a
private diagnostic run. Never use `-PublicTestMode` for private inputs.

The old `Invoke-BoundedReportProcess.ps1` samples only its parent process and is
not a substitute for this job-backed diagnostic protocol.
