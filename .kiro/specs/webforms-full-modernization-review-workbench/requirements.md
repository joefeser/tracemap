# Requirements

## Goal

Generate a bounded private review workbench for every selected surface in an
existing `webforms-modernization-packet.v1`. The workbench navigates retained
evidence; it does not rescan source, infer business intent, or generate a BRD.

## Requirements

1. Generate a root index and one linked report/handoff pair per selected
   surface.
2. Group and order pages deterministically by retained source path and stable
   surface identity.
3. Show controls, event chains, downstream boundaries, identity/state,
   data-movement candidates, structural candidates, gaps, and citations when
   retained by the packet.
4. Accept an evidence-docs root as a read-only corpus locator. Never delete,
   truncate, rewrite, or copy `chunks.jsonl`.
5. Include source excerpts only after explicit opt-in and only from a bounded
   repository-relative path under the supplied source root.
6. Generate into a staging directory and publish atomically.
7. Keep human review metadata separate from scanner evidence.

