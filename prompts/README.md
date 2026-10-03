# Private exchange prompts

These prompts produce small, aggregate-only maintainer handoffs. They must be
run locally in the private environment. Never commit source labels, repository
identities, private commit SHAs, run IDs, paths, values, indexes, reports, SQL,
credentials, connection material, logs, or business identifiers.

For restricted work environments, this repository is the transport boundary.
Prompts may invoke only tooling already committed in the checked-out TraceMap
head. If a required diagnostic projection does not exist, the prompt must stop
with a typed capability-missing result so the capability can be built and
published from the normal development environment first.

- `collect-maintainer-edge-case-followup.md`
- `collect-migration-extraction-summary.md`
- `collect-webforms-extraction-readiness.md`
- `collect-focused-webforms-gap-extractor-summary.md`
- `collect-webforms-scope-discovery.md`
- `review-focused-webforms-coverage-gaps.md`
- `run-webforms-monorepo-correction.md`
- `run-focused-webforms-monorepo-scan.md`
- `run-focused-webforms-one-repo-windows.md`
- `guided-corporate-claude-launcher-setup.md`

## Authorized agent-review prompt

`review-webforms-modernization-evidence.md` is a generic, ready-to-paste prompt
for a work-machine Claude Code review of an already generated packet, evidence
corpus, and application workbench. It asks the agent to preserve provenance,
static-evidence limitations, aliases, and fact/inference/unknown boundaries.
See `docs/WEBFORMS_AGENT_HANDOFF.md` for the matching commands. Agent output is
downstream planning material, never TraceMap scanner evidence.

## Corporate Claude launcher discovery

`guided-corporate-claude-launcher-setup.md` asks the work-machine Claude session
to inspect an approved BAT/Python launcher locally and return only a sanitized
argument-forwarding contract. It does not ask for launcher source, corporate
configuration, or secrets. Use its result before adding a launcher override to
the TraceMap PowerShell handoff. A verified launcher can then be supplied with
`Start-FocusedWebFormsClaudeReview.ps1 -ClaudeLauncherPath`.
