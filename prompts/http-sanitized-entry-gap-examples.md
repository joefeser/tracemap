# Sanitized HTTP entry-link reproduction examples

Prepare public-safe synthetic reproduction descriptions for the remaining HTTP entry-link gaps: H2/H3 and H4. This is a bounded evidence handoff, not another acceptance run.

Read-only; use existing evidence. No build, scan, preflight, requery, live HTTP, database work, source changes, or commits. Limit the work to five minutes and report any unresolved items when the limit is reached.

For each gap:

1. Identify the first unresolved hop. Distinguish a missing retained fact, an identity mismatch, an unqueried relationship, and a genuinely unknown caller. Do not classify an uninvestigated relationship as an extractor defect.
2. Provide a minimal renamed VB/WebForms example preserving the relevant structure: event wiring, control/page relationship, constructor invocation, inheritance, and HTTP wrapper call, as applicable. H2/H3 concerns the upstream constructor-to-entry relationship; H4 concerns the control-method-to-entry relationship.
3. Label each part as observed structure or hypothetical scaffolding. Do not invent an upstream caller to complete the chain. If scaffolding is needed to make an eventual fixture compile, identify it explicitly as synthetic, not application evidence.
4. State which facts and edges are retained today and which expected relationship needs testing, including rule IDs and evidence tiers where available. Separate desired fixture behavior from behavior demonstrated by the existing index.
5. Give a proposed regression assertion and one negative case that must not create a false connection. Include the minimum source files and reference/framework requirements needed to reproduce the shape, if known.

Use invented names and dummy URLs only. Exclude private source snippets, business identifiers, paths, hashes, credentials, and payloads. Reconstruct minimal synthetic code rather than copying private source and merely renaming identifiers. If a safe reproduction cannot be derived, describe the missing evidence instead.

H1 is already verified; do not reopen it. Database schema/DDL is deferred and is not a blocker. Preserve partial-analysis and static-only limitations: a retained call chain does not prove runtime execution.

Return the sanitized examples in the response. Do not commit or upload private artifacts. Stop after this handoff; do not start another diagnostic loop or implement a fix.
