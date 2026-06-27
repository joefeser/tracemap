# UI Field Property Lineage Terminal Context Vault Local Navigation Review Prompts

## Opus Spec Review Prompt

Review `.kiro/specs/ui-field-property-lineage-terminal-context-vault-local-navigation/`
as an implementation-ready Kiro spec for TraceMap.

Focus on Medium+ issues:

- Does the spec stay scoped to hidden/local vault navigation and avoid
  docs-export implementation overlap?
- Does it consume only structured property-flow `terminalContextKind` evidence
  and avoid parsing prose as primary evidence?
- Are public/demo claim-level behaviors explicit enough to prevent accidental
  promotion?
- Are rule IDs, candidate rule names, evidence tiers, limitations, and
  omission/safety gaps specified with enough precision?
- Are stable ID and safety requirements compatible with the current vault
  hidden-safety design?
- Are tests implementation-ready and sufficient for deterministic output,
  public/demo omission, unsafe metadata, generated-file safety, and non-claim
  wording?

Return findings with severity, file, line or section, rationale, and suggested
patch. Prefer no findings over speculative style comments.

## Sonnet Spec Review Prompt

Review `.kiro/specs/ui-field-property-lineage-terminal-context-vault-local-navigation/`
for implementation readiness and overlap risk.

Focus on:

- Ambiguous implementation decisions that should be recorded before product
  edits.
- Missing safety constraints for local paths, raw URLs, SQL/config values,
  snippets, credentials, remotes, production data, or private identifiers.
- Any wording that could imply runtime execution, database execution,
  dependency execution, impact, release safety, or complete coverage.
- Any place the spec could conflict with existing
  `ui-field-property-lineage-terminal-context-consumers`,
  `evidence-graph-vault-export`, or `vault-export-hidden-safety` specs.
- Missing validation tasks or stale task checkboxes.

Return Medium+ actionable findings first. Low findings are useful only if they
are narrow and safe to patch in the spec-only PR.
