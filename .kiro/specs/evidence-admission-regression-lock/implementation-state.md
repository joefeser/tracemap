# Implementation state

Branch: `codex/evidence-admission-hardening`.
Starting head: `32794a42`.
Goal: lock recent evidence-admission debugging into durable public-safe regression
coverage before PR review, then fix only demonstrated defects.

Ledger: `docs/validation/EVIDENCE_ADMISSION_DEBUGGING_LEDGER.md`.
Tracking: https://github.com/joefeser/tracemap/issues/871
Current stage: pre-PR validation completed; regular PR and bounded ACK review authorized. Merge is not authorized.
Implementation commit: `10b65897`; integration assertion correction: `39e38e2d`.

New matrix: 16 harness cases plus one real net48/System.Web test covering eight
query roots. All 17 tests pass, including ordinary/bounded paths and terminal
inventory. New public fixture builds with zero warnings/errors. Python diagnostic suites: 20 + 17 + 6
passed. The earlier focused 440-test run preceded the final nonvirtual-dispatch
fix and is supporting evidence, not final-code acceptance.

Reproduced fixes: source-defined nonvirtual read-only getter call edges;
MyBase/MyClass nonvirtual dispatch suppression through graph projection and both
terminal-prewalk modes. Extractor 0.10.4, path algorithm 1.8. Neither fix proves
the cause of the private unresolved markup/property relationships. No runtime
ASP.NET lifecycle edge was fabricated.

Initial full-suite session 36568 wrote `/tmp/tracemap-evidence-lock-full.log`.
It was stopped after a concurrent rebuild invalidated runtime-hash fixtures;
the log preserves runtime-changed preparation failures, a query setup failure,
and an IL collection repeatability failure. Not a clean run; do not assume every
failure has the same cause until a stable-output retry verifies it. No acceptance
claim is based on this interrupted run. Freeze source and binaries for the retry.
Final full-suite retry log: `/tmp/tracemap-evidence-lock-full-stable.log`.
That frozen run completed in 27m12s: 4,333 passed, one Windows-only test skipped,
one failed. The sole failure was a stale `1.3` algorithm expectation in
DeepProjectlessNativeWorkflowTests; the actual algorithm is `1.8`. The assertion
was updated without changing production code or any path/evidence assertion.
The complete affected workflow class plus the new HTTP matrix then passed 18/18
in 46s (`/tmp/tracemap-evidence-lock-final-targeted.log`). This is a full run plus
a targeted corrective retry, not a claim of a second all-green full-suite run.
The interrupted run's runtime/IL-repeatability failures did not recur in the
frozen full run; their original cause is not conclusively attributed.
CLI smoke: `/tmp/tracemap-http-entry-smoke-final-20261015`; an initial positional repo
argument was rejected, then the documented `--repo` invocation succeeded.
The first smoke receipt captured the pre-commit SHA. A second smoke at clean
commit `39e38e2d71ccaf0f2b1d8ca2f95cc6dd0d486c8d` succeeded with 107 facts,
BuildStatus Succeeded and Level1SemanticAnalysis; all five required scan outputs
were produced. Receipt root: `/tmp/tracemap-http-entry-smoke-committed-20261015`.
Pinned OSS, Windows/private scans and non-.NET adapter suites are deferred;
the local fixtures do not establish those results. Optional schema work remains
deferred under #870. No PR has been created or merged.

## Windows pre-PR green gate (2026-10-15)

Worktree branch: `codex/evidence-admission-green-windows`, based on `b931be91`.
The Windows context CI failure was reproduced with a portable projectless test:
SemanticAnalysisUnavailable omitted messageHash. Syntax extractor 0.4.18 now hashes
only the fixed sanitized message. The fixed-message privacy assertion remains.
The newly gated real net48 HTTP fixture also reproduced a reduced compilation on
Windows. Its project now explicitly disables the compiler My template
in both regular and design-time compiler inputs; the scan-success assertion and
all eight path-root expectations remain unchanged. The combined 28-test targeted
run passed after these fixes, including all 17 HTTP boundary cases.

CI now runs the HTTP entry class/sample through the Windows and Ubuntu gap-family
gate. SQL/HTTP compatibility is manually dispatched on the hardening branch before
a PR; its original push branch filter remains unchanged. Historical
baseline test failures are visible non-gating diagnostics; candidate failures are
still fatal. Final frozen full-suite and CI results are recorded below.

The first hosted Windows retry passed the portable hash guard but exposed the
legacy-sample diagnostic test's dependence on installed .NET 3.5 tooling. That
test now copies the same three legacy source files into a net10.0 diagnostic
harness with an explicit missing-type error; its partial Tier1/no-duplicate and
categorical diagnostic/privacy assertions remain. This is not legacy framework
compatibility acceptance. The first full personal-Windows run also exposed
Windows-only fixture cleanup, separator, pooled-file and symlink privilege
assumptions, plus a CRLF header check that bypassed docs-export hashing. These
were fixed under the pre-PR gate; earlier failures are not acceptance receipts.
Adapter Validation can now be manually dispatched on the branch before a PR.

### Final Windows gate retry

Intermediate code/test checkpoint: `2857d10d9db97d5af509750739ad198926f46d67`.
The targeted memory/scale retry passed 43/43 with no skipped tests. Its changes
release mutation-connection handles and clean owned read-only Git fixtures; memory
limits, evidence assertions and traversal budgets remain unchanged. Clean-CI HTTP
fixture compilation now has a compile-only test-project reference, so its actual
net48 reference closure is restored before design-time scans. Both Ubuntu and
Windows focused WebForms jobs passed at preceding commit `95888294`.

That Windows build used SDK 10.0.302 and passed with warnings as errors,
zero warnings/errors. A public modern-sample smoke at that code commit
produced 27 facts, Level1SemanticAnalysis, BuildStatus Succeeded, no known gaps,
and passed validate-adapter-artifacts.py. CLI SHA-256:
`6c0474fa10cbf90c99efa89b9e0f33754a462e8d082816b7b29315a86e2755d9`.

The final late Windows fixture failure required symlink privileges for a linked
GitHub workflow parent. DirectoryLinkFixture now provides an equivalent Windows
junction; the full affected class passed 65/65. The macOS distribution lane then
exposed canonical /private/var temp roots, now accepted only by resolving the
trusted OS temp root. Requested cleanup paths are not resolved; linked-root and
outside-temp rejection guards remain. The affected cleanup/relocation set passed
7/7 on Windows, and macOS distribution passed on the corrected code.

An intermediate Windows retry was stopped after provenance/repeatability failures
while its own worktree HEAD was being advanced. This is not acceptance and does
not establish a scanner defect. The final detached worktree freezes HEAD, files
and binaries throughout the full run; all subsequent documentation and ACK
consumer work happens in separate checkouts.

Owner authorization now includes a regular PR to dev after green validation,
followed by the ACK PR loop with at most six review-request rounds. No merge is
authorized. ACK 0.5.6 at tag commit d8a78c7e4a1c7157e64f8bfdb19946e1e18dec28 was
verified in Ubuntu on this Windows host: 2,517 ACK tests, RELEASE_READY receipt
readback, lane doctor satisfied, and 11 consumer/lane tests passed. Native Windows
release validation could not run the POSIX shell assumptions; no weakened release
receipt or changed ACK source was substituted.

Earlier failed/stopped runs remain diagnostic evidence, not acceptance.

Final boundary follow-up: commit `5b2a4d32a8e6538cd5169fc6e4abfa404250d0f3`
normalizes only Path.DirectorySeparatorChar. Both literal-backslash storage-root
negatives failed on native Linux before the fix (incorrect public-safe result).
All 28 baseline tests then passed on Linux and Windows; the Windows rows prove
the same spellings remain allowed when backslashes are real separators. The
intermediate Windows run at 2857d10d was superseded by the final frozen run below.
A new CLI smoke at 5b2a4d32 succeeded with 27 facts and valid
adapter artifacts. CLI SHA-256:
`34afac91d36c6139576d5e7ea3e8a79ee2d3214194b979f45a1ab50b7f4f4c88`.

### Final pre-PR acceptance receipt

Tested code commit: `5b2a4d32a8e6538cd5169fc6e4abfa404250d0f3`.
The final receipt update changes documentation only.

- Frozen Windows solution run: **4,344 passed, zero failed/skipped**, 35m13s.
  Build: SDK 10.0.302, warnings as errors, zero warnings/errors. Local receipt:
  `%TEMP%/tracemap-green-5b2a4d32-results/full-windows.trx`.
- [Adapter Validation](https://github.com/joefeser/tracemap/actions/runs/38738915809)
  at the same code commit: Linux .NET **4,342 passed, one Windows-only skip,
  zero failures**, 13m15s. All five adapters, artifact validators and the
  five-adapter combine passed.
- [Local distribution](https://github.com/joefeser/tracemap/actions/runs/38736119795)
  passed on Windows, Linux and macOS at 2857d10d, including the authentic Windows
  existing-publish proof. The subsequent production change is the baseline
  separator guard, verified by 28 tests on both Windows and Linux.
- Supporting CI at 542d5c72 passed:
  [compiled metadata on three OSes](https://github.com/joefeser/tracemap/actions/runs/38734735349),
  [authentic deep ASP.NET publishing](https://github.com/joefeser/tracemap/actions/runs/38734741212),
  [SQL](https://github.com/joefeser/tracemap/actions/runs/38734548480), and
  [outbound HTTP](https://github.com/joefeser/tracemap/actions/runs/38734753308).
  These earlier receipts are supporting evidence, not final-head full-suite runs.
- Final-code focused Windows/Ubuntu WebForms and VB context jobs, candidate
  baseline comparison, and private-path guard all passed. Historical baseline
  failures remain diagnostic and never count as candidate acceptance.
- Final-code Python guards: Linux **60 passed**; Windows **59 passed, one file
  symlink-privilege skip**, with that negative control exercised on Linux.
  ACK consumer/lane regression: **11 passed** against verified 0.5.6.
- Public CLI scan: 27 facts, Level1SemanticAnalysis, BuildStatus Succeeded,
  no known gaps, all required outputs and adapter artifact validation passed.
  CLI hash is recorded immediately above.

The native Windows full run includes the five same-name provider classes,
cross-DLL exact overload selection, framework 4.0/2.0 candidate boundaries,
ordinary/bounded report parity, and the immutable native start/resume chain.
Private application rescans and broader pinned OSS checks remain separate;
no runtime execution or complete application coverage is inferred. Optional
schema work under #870 remains deferred. Open the owner-authorized regular PR
to dev and follow ACK for at most six Codex request rounds; do not merge.

### PR #872 first review correction (2026-10-15)

PR: https://github.com/joefeser/tracemap/pull/872, targeting dev.
Reviewed head: `4788b3afbf17370efa78a7def7e9de2d1fe7e072`.
ACK 0.5.6 released the settled nine-finding batch with PATCH_AUTHORIZED.
One Codex request has been used; six is the owner ceiling, not a request target.

The findings are mixed: source/metadata admission ordering, exact registration
identity, retained artifact compatibility, consumer scope and stale acceptance
instructions. Fixes keep metadata-only bridges Tier3, bind same-line registrations
by their selected hash and prefix, and project canonical redacted location markers
to Tier4 gaps without navigation. Raw unsafe artifact locations and malformed
markers remain rejected. Exact legacy inventory wording remains readable while
new output includes persistence. The saved-query helper accepts both complete
database scopes and rejects narrow or unrelated scopes.

The request to admit the non-Git `unknown` sentinel conflicts with the documented
database provenance contract and AGENTS.md. It remains rejected; rehashed path and
inventory regressions now pin that refusal alongside valid 40/64-hex commits.
The old af2d00ff implementation commit exists; acceptance instructions now require
the current branch tip containing the recorded pre-PR baseline 5b2a4d32 rather
than attributing newer validation to that historical ancestor.

Local pre-push validation of the correction tree: warnings-as-errors build;
412 review/legacy/native/framework tests passed (3m36s), 151 adjacent compilation,
HTTP and source-metadata tests passed (1m27s), 12 Python context-preparation tests
passed, and Test-WebFormsCapShortcut.ps1 passed. The 26-case vault safety subset
also passed separately. No failures or skips in these focused runs. Earlier
red regressions and fixture-development runs are diagnostic, not acceptance.
These focused results do not replace the earlier full-suite receipts above or
claim that the updated PR head has completed hosted CI/review yet.

### Redacted-gap provenance follow-up

The next settled ACK batch on bdac073c authorized one Baz finding: canonical
redacted-location gaps lacked originating supporting fact IDs. Strengthened all
12 combined/path/reverse, hidden/public-safe and native/canonical marker rows to
compare the exact supporting-fact union with the retained inputs. All 12 failed
before the correction. The collector now merges IDs per marker and claim with
one deterministic final sort, retaining node/edge/surface provenance while
continuing to omit navigation. Full VaultExportTests: 62 passed, zero failed or
skipped, 37s; warnings-as-errors build clean. This is a narrow export repair,
not a claim that current-head hosted CI or fresh Codex review has completed.
Round-one dispositions are confirmed. The original Codex request remains the
only request so far; the second patch batch was returned by exact-head Baz.
