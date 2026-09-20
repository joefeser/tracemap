# Implementation state

- Branch: `codex/vb-webforms-battle-test`
- Status: implementation and validation complete; branch pushed
- Implementation commit: `72b7470c`
- Private input: anonymous aggregate counts only; no private source, route, or
  file identity is retained in this spec or fixture.

## Decision

Never generate a `.vbproj` from the inspected Web Site. Project membership,
references, build actions, configuration transforms, and deployment behavior
cannot be reconstructed safely from projectless static evidence.

## Validation

- Focused extractor, packet, and projectless fixture tests: 81/81 passed.
- Full .NET solution: 1,941/1,941 passed.
- Focused application-workbench PowerShell regression: passed, including
  private/raw-source modes, alias-only outliers, and WITS review validation.
- Two fresh projectless fixture scans produced byte-identical `facts.ndjson`.
- Fixture packet: three resolved handlers; direct-call handlers retained 2 and
  3 calls respectively, including the framework-shaped grid call; the UI-only
  handler retained zero calls. Generated terminal-free gaps split into two
  `DownstreamWithoutSupportedTerminal` and one `NoBackendEvidence` rows.
- `scripts/check-private-paths.sh` and `git diff --check`: passed.

## Anonymous stress rerun

An alias-only 23-page rerun retained 123 additional calls after `72b7470c`:
projections increased from 208 to 331, unique facts from 106 to 229, and
normalized sites from 70 to 193. The same 123 newly admitted VB syntax call
facts exposed a producer provenance omission: each lacked `coverageLabel`,
creating 123 `EvidenceCoverageLabelUnavailable` packet gaps. The follow-up
adds the explicit `syntax-only` label at the VB syntax producer. No page or
repository identity was retained.

Follow-up validation: focused VB/projectless tests passed 25/25; the public
fixture retained all five calls with no `EvidenceCoverageLabelUnavailable`
gap; the full .NET solution passed 1,941/1,941; privacy and diff guards passed.

Final external validation used the receipt-validated alias-only summary helper
against the same 23-page projectless VB application. It retained 331 call
projections, 229 unique call facts, and 193 normalized source sites. The 123
spurious coverage-label gaps were absent; the remaining 52 terminal-free gaps
split exactly into 9 `BoundedTraversalTruncated`, 32
`DownstreamWithoutSupportedTerminal`, and 11 `NoBackendEvidence` rows. Both
sanitized generator and input provenance hashes were present and validated.

## 2026-09-19 receiver precision follow-up

- `vb-syntax/0.3.18` retains a syntax-only receiver type when exactly one
  explicit caller parameter, in-scope local, or field declaration supplies it.
- The projectless receiver bridge may use that retained type, explicit
  `Me`/`MyClass`, or one exact namespace-qualified retained type. It still
  fails closed on ambiguous provenance, type identity, overload, or target
  evidence.
- This follow-up was prompted by private page-scoped diagnostics dominated by
  typed parameters/locals and Web Forms/framework property chains. It does not
  infer framework properties or chained property return types.
- Validation: the focused extractor/receiver-bridge/memory-budget tests passed
  4/4; the full .NET solution passed 1,957/1,957; `git diff --check` passed.
- Because the producer version changed, external validation requires fresh
  source scans before recombining and regenerating the Web Forms packet.
