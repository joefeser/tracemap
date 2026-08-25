# WebForms, Razor, WinForms, and Angular migration triage

This workflow extracts a small migration-planning summary from existing private
TraceMap artifacts. It keeps technology families separate and preserves the
failed/partial coverage envelope.

The reconstructed example under `docs/examples/` is based only on sanitized
metadata and visible aggregate fields supplied by the owner. It is not a
byte-for-byte copy of the private result.

## Interpretation rules

- Zero endpoint matches means alignment is `not-proven`; it does not prove no
  interaction.
- Razor binding diagnostics describe reduced coverage, not a complete binding
  inventory.
- A WinForms-named rule remains WinForms evidence. It is not WebForms proof.
- TypeScript build/configuration noise may obscure Angular route, service, and
  HTTP evidence and must not be presented as an application defect.
- Private source labels, repository names, run IDs, source commit SHAs, route
  values, and business identifiers are never valid public exchange fields.
- Use opaque source/snapshot references only when correlation is necessary.
- If exact source snapshot identity is unavailable after sanitization, say so.
  Never substitute values such as `multi-source` into a commit-SHA field.

The immediate goal is not a master migration plan. It is to distinguish usable
evidence, candidate relationships, reduced coverage, and the one missing input
that would most improve the next decision.

See `prompts/collect-migration-extraction-summary.md` for the private-side
collection instructions.
