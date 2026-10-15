# Implementation state

Branch: `codex/evidence-admission-hardening`.
Starting head: `32794a42`.
Goal: lock recent evidence-admission debugging into durable public-safe regression
coverage before PR review, then fix only demonstrated defects.

Ledger: `docs/validation/EVIDENCE_ADMISSION_DEBUGGING_LEDGER.md`.
Tracking: https://github.com/joefeser/tracemap/issues/871
Current stage: fixtures and two reproduced fixes implemented; full-suite validation running.

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
CLI smoke: `/tmp/tracemap-http-entry-smoke-final-20261015`; an initial positional repo
argument was rejected, then the documented `--repo` invocation succeeded.
Pinned OSS, Windows/private scans and non-.NET adapter suites are deferred;
the local fixtures do not establish those results. Optional schema work remains
deferred under #870. No PR has been created or merged.
