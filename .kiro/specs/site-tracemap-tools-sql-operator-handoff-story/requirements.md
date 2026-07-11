# Site SQL Operator Handoff Story Requirements

## Goal

Explain the completed SQL evidence runway to managers and database owners using
public-safe deterministic evidence language. This implements issue #466 and
leaves the generated proof-packet expansion in #467 as follow-up work.

## Requirements

1. Add `/sql/operator-handoff/` and link it from `/manager-packet/`, `/outputs/`,
   `/limitations/`, `/proof-paths/for-managers/`, and `/packets/`.
2. Answer which categorical contexts, prerequisites, protected steps,
   validation expectations, stop conditions, and gaps are visible before a
   DBA/operator acts.
3. Cover PostgreSQL FDW, dblink, logical replication, `pg_cron`, manual-client
   context transitions, permission statuses, validation-step boundaries, rule
   IDs, tiers, spans, extractor versions, coverage, and owner questions.
4. Render no raw SQL, credentials, connection strings, scheduled command
   bodies, private infrastructure names, local paths, validation output, or
   internal ticket details.
5. Make all runtime, safety, permission-effectiveness, validation, rollback,
   and approval non-claims explicit.
6. Register sitemap/discovery metadata and deterministic focused validation.
7. Validate build, links, discovery, claims, private data, desktop/mobile
   layout, private paths, and diff hygiene.

Related issues: #466, #467, #453, #454, #455, #456, #457, and #458.
