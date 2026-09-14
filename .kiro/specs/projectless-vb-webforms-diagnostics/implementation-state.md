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
