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


## Explicit compiled-only source exclusion

`scan --repo <repo> --compiled-only --compiled-input <dll> ...` retains Git
identity and the normal explicit compiled-input admission, hashes, limits, root
selectors and lifecycle refusals. It excludes source enumeration, MSBuild,
C#/VB semantic extraction and syntax extraction. Source inventory and selected
projects/solutions are empty. All three source inventory captures use the same
excluded scope; the source snapshot digest hashes the empty inventory and MUST
NOT be interpreted as binding the private source tree. Compiled artifacts retain
their separate content bindings. Scan identity distinguishes this mode.

The existing repository-manifest rule emits a Tier4Unknown AnalysisGap containing
`SourceAnalysisExcludedByCompiledOnly`; coverage is reduced and build is NotRun.
This is neither source/assembly correspondence nor whole-repository acceptance.
It does not fix general Roslyn memory growth. Default scans remain unchanged.
The mode requires explicit compiled inputs and refuses restore, source scope,
framework/binlog, VB context and source CI/dependency indexing switches. It is
not allowed on the immutable-parent compiled attachment route.

Use the same external Windows job guard and limits for the private diagnostic.
No lifecycle admission or unknown-value policy changes accompany this mode.


## InfoMessage framework profiles (IL 0.2.23 / policy v24)

The explicit InfoMessage boundary policy is now `explicit-sql-info-message/2`.
Its finite method catalog admits the existing signed framework-4 identities and
an explicit framework-2 profile: System.Data/mscorlib/System.Xml 2.0.0.0 with
public key token b77a5c561934e089, and the existing VB callback helper signatures
under Microsoft.VisualBasic 8.0.0.0, b03f5f7f11d50a3a. Only predeclared complete
catalog signatures are expanded. Actual input identities are never normalized;
arbitrary versions, tokens, cultures, methods and parameter signatures refuse.

Legacy SQL command/parameter/adapter/Fill catalog resolution is enabled only
inside the explicit InfoMessage boundary. Default object-path and SQL lifecycle
routes retain their previous profile admission. Delegate construction requires
an inspected callback of the same framework profile; connection creation,
command connection use, Open/Close, subscription and removal retain the profile
alongside allocation identity and reject mismatches. Callback bounds, tracked
reference escape refusals, event dependence and unknown naming effects remain.
Receipt schema is unchanged; the policy and extractor versions identify this
intentional admission expansion. No framework runtime or application execution
is performed or proven.

Tests retarget only independently authored public C#/VB compiled fixture copies
in temporary storage, then decode them with both readers. These are synthetic
encoded-metadata controls, not .NET 2 runtime compatibility or private acceptance.
Native CLI controls hash/admit the copied inputs, trace parameter attachment and
Fill/naming identity, and retain ordinary required outputs in compiled-only mode.


## Bounded callback XML catalog additions (IL 0.2.24 / policy v25)

InfoMessage policy `explicit-sql-info-message/3` adds exactly two signatures:
`System.Xml.XmlAttributeCollection.get_ItemOf(Int32) -> XmlAttribute` and
`System.Xml.XmlNode.get_InnerXml() -> String`, with the existing signed
System.Xml 4.0.0.0 and 2.0.0.0 profiles. These are external, unmodeled XML
operations inside the existing opt-in inspected callback. The reference result
stays external; the declared string result stays symbolic text, never a parsed
or assumed name. No XML is evaluated. Index validity, exceptions, callback
execution, output contents and naming success remain unproved.

No method-name matching, new overload family, public API invocation, collection
mutation admission or tracked-reference escape is added. Existing receiver and
argument origin checks, instruction/work/heap limits and framework-profile
attachment checks apply. Wrong signing key/version/call shape refuses. Public
C#/VB fixtures now exercise both ordinal attribute access and InnerXml alongside
the existing named attribute access, on normal and synthetic legacy profiles.
Default SQL route contracts and receipt schema remain unchanged; explicit policy
and extractor versions distinguish this bounded catalog expansion.


## Bounded lifecycle equality (IL 0.2.25 / policy v26)

The lifecycle interpreter models `ceq` only for concrete Int32 values, null/null,
known non-null literal/allocation versus null, the same tracked reference, and
different proven fresh allocations. It does not compare string contents or treat
symbolic IDs of distinct collection/property views as proof of non-aliasing.
Inputs, opaque values, unknowns, floating/native-pointer values and unresolved
reference relationships refuse with `lifecycle-equality-unresolved:<kind>:<kind>`.
The existing opcode rejection includes the body, offset and actual invocation
path; category labels contain no operand value. No equality value is assumed.

No relational-branch opcode, loop inversion, indirect pointer comparison or
unbounded enumeration support is added. Existing control-flow/loop/heap/work
limits remain. C#/VB compiled fixtures cover known scalar/null/alias/allocation
comparisons, unknown input nullness and potentially aliasing property views.
Legacy/normal profiles and native compiled-only CLI output remain covered.
The equality operation is available to the bounded lifecycle interpreter; root
admission, SQL route contracts and receipt schemas are unchanged. InfoMessage
policy remains /3 because callback catalog admission is unchanged.


## Runtime connection State recognition (IL 0.2.26 / policy v27)

InfoMessage policy /4 recognizes only exact signed SqlConnection/DbConnection
get_State()->ConnectionState under the existing profiles and receiver/profile
checks. Each read is tied to its connection allocation/site, capped at 16/root,
and returned as connection-state-unbound. Optional ConnectionStateReads receipts
explicitly record unbound external observation; no enum value or Open/Close
transition is inferred. The claim includes runtime-state-control-unproven.
Unknown equality still refuses with operand categories and exact opcode path;
direct state-dependent branches refuse. An unused state read does not prohibit
otherwise proven static lineage. This is typed-unknown recognition, not completion
of state-dependent lifecycle support. Default routes remain unchanged.

Microsoft describes the getter as the state during the most recent network
operation, not a static source constant:
https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection.state

The private continuation prompt is suspended pending the whole-protocol map and
synthetic batch implementation. GLM takeover state and a bounded read-only map
prompt are under prompts/. Do not substitute an assumed Open/Closed value.
