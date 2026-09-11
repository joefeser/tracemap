# Requirements: Web Forms agent evidence handoff

## Goal

Make a private focused Web Forms review set self-describing for authorized
humans and downstream tools. The handoff identifies the smallest retained
evidence to read first and the closed TraceMap queries that can retrieve more
evidence without rescanning application source.

## Requirements

1. Every generated private case review SHALL have an adjacent, versioned,
   deterministic machine-readable handoff.
2. Every generated review set SHALL have a root handoff that inventories its
   cases and describes the matching TraceMap index when one is supplied.
3. Private HTML SHALL expose the handoff through a collapsed human-readable
   section. Anonymous HTML and JSON SHALL contain no handoff link or private
   handoff content.
4. A case handoff SHALL bind scan, commit, inspection schema, case, handler,
   surface, retained evidence, rules, tiers, coverage-relative limitations,
   and stopping state when those values were retained.
5. Retrieval hints SHALL reference only recipes from the closed
   `tracemap-evidence-query-recipes.v1` catalog and SHALL carry bounded,
   already-known parameters.
6. When an index is supplied, the root handoff SHALL open it read-only and
   require one matching `scan_manifest` row for the inspection scan and commit.
7. When an evidence-docs corpus is supplied, the root handoff SHALL validate
   its manifest provenance and deterministically select matching chunks from
   `chunks.jsonl`.
8. Application-database guidance SHALL be limited to retained structural
   evidence and explicit authorized follow-up questions. It SHALL NOT contain
   credentials, connection strings, configuration, raw SQL, or database
   execution instructions.
9. Handoffs SHALL preserve static-analysis non-claims and SHALL NOT modify
   scanner facts, the retained index, the inspection snapshot, or human review
   overlays.
10. Output publication SHALL be transactional and fail closed on collisions,
    provenance mismatch, unsupported schemas, unsafe or oversized inputs,
    unknown recipes, or invalid parameters.

## Product boundary

The handoff exposes deterministic evidence discovery. It does not generate a
BRD, infer business intent, select a target architecture, call an LLM, or
generate Angular, .NET, PostgreSQL, or migration code.
