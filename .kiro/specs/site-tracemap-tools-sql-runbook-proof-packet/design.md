# Site SQL Runbook Proof Packet Design

The implementation adds a nested proof route beside the existing manager SQL
handoff story and a checked-in JSON projection under `site/src/assets/`.

The JSON contract is deliberately narrower than the generated core packet. It
contains only allowlisted categorical context, closed status values, owner-safe
questions, public repository provenance, repo-relative synthetic spans, and
extractor metadata. It contains no generic fact bags or statement content.

The example is grounded in the checked-in `samples/sql-operator-runbook`
fixture at a public commit. It accurately retains the scan's reduced coverage.
`postgres_fdw` and `pg_cron` are illustrated; dblink remains
`missing-evidence`; logical replication is partial because publication intent
is illustrated while subscription evidence is not established. These gaps are
features of the packet, not missing presentation work.

The page follows the existing static TraceMap design system. Responsive card
and list layouts avoid a wide packet table. A focused validator checks route,
asset schema, provenance, references, statuses, protected-category omission,
required concepts, overclaims, private data, sitemap, discovery, and inbound
links. The general site validator invokes the focused validator.
