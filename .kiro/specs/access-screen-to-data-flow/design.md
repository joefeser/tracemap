# Access Screen-to-Data Static Flow Design

## Decision

Add a dedicated `tracemap-access flow` read-side command and
`AccessScreenDataFlowReporter`. Existing generic route-flow models assume web
route roots and endpoint semantics; reusing them would blur Access event,
binding, and startup limitations. The new report reuses their bounded,
deterministic path posture without changing either reducer.

## Input and graph

The reader accepts a completed standard `index.sqlite` and reconstructs only
cataloged Access facts plus their safe properties and provenance.

```text
form candidate / autoexec candidate
  -> control ownership
  -> static event-binding candidate
  -> VBA procedure
  -> same-module call or DoCmd navigation/query/report candidate
  -> saved-query dependency
  -> table / report / external-boundary terminal
```

`AccessBindingDeclared` adds declared UI-to-query/table/field edges.
`AccessExternalLinkDeclared` adds a synthetic opaque boundary terminal keyed
from the supporting fact ID and boundary category. Analysis gaps remain gaps,
not edges.

## Roots and coverage

Every declared form is a `ui-root-candidate`. An `autoexec` macro is a
`startup-candidate`, but its protected body cannot create outgoing edges.
Absent autoexec evidence always emits `AccessStartupIdentityUnavailable`.
Absent item-level form/event/VBA facts emits
`AccessDesignFlowEvidenceUnavailable`.

## Traversal

Roots and outgoing edges sort by opaque node/edge ID. Breadth-first state
contains node and edge IDs. Re-entry into a state path produces a cycle-ended
path plus a Tier4 gap. A node with no outgoing edge is terminal. Reports and
tables are terminal even if other declaration metadata exists. Bounds never
silently omit branches.

The path tier is the weakest upstream tier: Tier4, Tier3, then Tier2. Coverage
is `partial` when any edge is partial, a scoped gap exists, or a bound is hit.

## Provenance

Each edge contains one exact evidence reference constructed from the supporting
fact. Paths union and stably order fact IDs, rule IDs, tiers, coverage labels,
and limitations. No new conclusion is inferred from adjacency alone.
Composition gaps use `legacy.access.screen-data-flow.v1`, whose limitations
state that roots and traversal are static candidates only.

## Presentation

The command writes `<out>.json` and `<out>.md` atomically through temporary
siblings. JSON schema is `tracemap.access-screen-data-flow.v1`. Markdown shows
summary, roots, paths, gaps, and limitations. Opaque Access stable keys are
allowed; display names and generic property bags are not.

## Privacy

Only allowlisted categorical properties are read. Raw SQL, source text,
expressions, hashes that encode protected text, and display names are neither
projected nor rendered. Failures are classification-only in the Access CLI.

