# Collect a migration extraction summary

```text
Read docs/MIGRATION_EXTRACTION_TRIAGE.md and the sanitized examples. Use only
existing local artifacts and tooling committed in this TraceMap checkout; do
not rescan unless separately authorized. Do not create an ad hoc parser,
diagnostic script, or one-off command pipeline on this machine.

Produce a JSON summary and a Markdown summary capped at 700 words. Preserve the
failed/partial run state and keep WebForms, Razor, WinForms, and Angular evidence
separate. Zero matches means not-proven, not no interaction. Cite rule IDs and
evidence tiers, but do not output private source labels, repository identities,
run IDs, source commit SHAs, route values, paths, source values, SQL,
credentials, connection material, logs, or business identifiers.

Use opaque source/snapshot references only when correlation is essential. If
exact snapshot provenance cannot survive sanitization, record
`unavailable-in-sanitized-handoff`; never invent a commit SHA or use a phrase
such as `multi-source` in a commit-SHA field.

Before returning output, search it for prohibited fields. If any remain, stop
without committing or pushing. Return only aggregate counts, categorical
states, limitations, and one bounded next action for each E1-E4 lane.

If committed TraceMap tooling does not expose enough categorical data to
answer a requested question safely, return only
`diagnostic-capability=missing`, followed by the names of the missing
categorical fields. Do not inspect or return raw source material to fill the
gap. The coordinator must implement and publish the missing diagnostic
capability before this machine is asked to run it.
```
