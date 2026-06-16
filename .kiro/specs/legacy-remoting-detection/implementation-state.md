# Legacy Remoting Detection Implementation State

Status: ready-for-implementation
Branch: codex/legacy-remoting-detection-spec
Public claim level: hidden

## Scope

This spec defines an implementation-ready plan for deterministic .NET Remoting
evidence extraction in legacy .NET codebases. It is intentionally a spec-only
branch with no scanner, reducer, report, or rule catalog implementation changes.

The detector is scoped as a sibling to WCF/SVC legacy boundary detection. It
does not merge Remoting evidence into WCF fact types or rules, though future
reports may group both under generic legacy service-boundary summaries.

## Scope Decisions

- Initial Remoting evidence may be `Tier2Structural` and
  `Tier3SyntaxOrTextual` heavy.
- Roslyn semantic success improves evidence quality but is not required for
  syntax/config extraction.
- `MarshalByRefObject` inheritance is Remoting-capable object-shape evidence,
  not proof of hosting, reachability, deployment, or production use.
- Channel setup and registration APIs are static configuration evidence only.
- `<system.runtime.remoting>` config values such as URLs, object URIs, ports,
  application names, provider properties, and arbitrary attributes must be
  hashed or omitted.
- Future public smoke candidates are named only as neutral repositories for
  possible local/manual validation. This spec commits no raw clone paths, raw
  remotes, generated outputs, or snippets from those repositories.

## Review State

- Opus spec review: completed first pass; important findings patched in spec
  text.
- Sonnet spec review: completed first pass; blocking XML parser safety finding
  and important findings patched in spec text.
- Sonnet re-review: completed with reduced coverage because Kiro attempted a
  denied shell command, then reviewed from prompt content; no blockers found and
  clarification findings patched in spec text.
- PR review loop: Qodo optional delimiter finding patched by clarifying
  `supportingFactIds` canonical Remoting output versus legacy consumer
  compatibility.
- PR review loop: Codex P2 indirect `MarshalByRefObject` finding patched by
  requiring Remoting-specific context before emitting indirect-inheritance
  Remoting object facts.

## Validation

- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Repo spec validation commands: `node scripts/kiro-review.mjs --self-test`
  passed; `node scripts/kiro-review.mjs --phase legacy-remoting-detection
  --kind spec --model auto --dry-run` passed and generated review prompt
  metadata without invoking a model.
- Implementation validation such as `dotnet build`, `dotnet test`, and
  `tracemap scan` is deferred to the future implementation branch because this
  branch only creates spec files.

## Follow-Ups For Implementation

- Choose exact fact property names to match existing `FactRecord` and report
  conventions.
- Keep report output citing source Remoting extraction rules unless a future
  implementation emits independent report summary facts and adds a documented
  report rule.
- Keep syntax-only channel/registration evidence at `Tier3SyntaxOrTextual` in
  v1; use `Tier2Structural` primarily for parseable config and other explicitly
  structured non-name-only evidence.
