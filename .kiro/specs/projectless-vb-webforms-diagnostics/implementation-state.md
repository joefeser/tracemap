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
