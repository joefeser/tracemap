# Site SQL Runbook Proof Packet Requirements

## Goal

Implement issue #467 with one public-safe, deterministic proof packet that
demonstrates the completed SQL operator handoff contract without publishing
statement text or private infrastructure details. This extends the manager
story delivered for #466.

## Requirements

1. Add `/sql/operator-handoff/proof-packet/` plus a checked-in public JSON asset
   derived from `samples/sql-operator-runbook` and
   `sql-operator-runbook-packet/v2`.
2. Show public repository, commit, scan identity, reduced coverage, categorical
   contexts, transition checkpoints, prerequisite status, protected categories,
   milestones, validation-step presence, stop conditions, gaps, owner questions,
   limitations, rule IDs, evidence tiers, repo-relative spans, and extractor
   versions.
3. Cover `postgres_fdw`, `dblink`, logical replication, and `pg_cron` while
   labeling unillustrated or partial fixture surfaces as gaps.
4. Preserve the closed permission statuses: `present-in-scripts`,
   `missing-evidence`, `conflicting-evidence`, `unknown`, and
   `needs-owner-review`.
5. Publish no executable statements, credentials, connection strings,
   protected values, scheduled command bodies, machine-local paths, private
   infrastructure names, database output, or internal ticket details.
6. Make runtime, authorization, validation, rollback, execution, safety, and
   approval non-claims explicit.
7. Link the proof packet from the manager story, manager packet, outputs,
   limitations, manager proof paths, packet indexes, examples, and demo index.
8. Register sitemap and discovery metadata and add focused schema, claim, link,
   discovery, provenance, private-data, and leak validation.
9. Validate the site build, focused/full tests, desktop/mobile layout, private
   paths, and diff hygiene.

Related issues: #467, #466, #453, #454, #455, #456, #457, and #458.
