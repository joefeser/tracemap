# Site TraceMap Tools Property-Flow Schema Gap Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

## Implementation Tasks

- [x] Inspect existing site proof-path patterns, metadata patterns, validators,
      and adjacent pages before editing.
- [x] Verify current `dev` source evidence for property-flow route-flow schema
      unsupported/unavailable behavior before writing public copy.
- [x] Create `requirements.md`, `design.md`, `tasks.md`, and
      `implementation-state.md`.
- [x] Record selected placement and rejected alternatives in
      `implementation-state.md`.
- [x] Add `/proof-paths/property-flow-schema/` using existing static-site
      layout, typography, metadata, and claim-boundary patterns.
- [x] Include visible `Public claim level: concept`.
- [x] Include visible `No public conclusion without evidence`.
- [x] Explain `RouteFlowUnavailable`, empty route-flow schema,
      `UnsupportedRouteFlowSchema`, and available route-flow schema.
- [x] Explain that unsupported schema does not prove route-flow evidence is
      absent and must not silently promote endpoint context.
- [x] Preserve evidence fields: rule ID, evidence tier, classification,
      supporting IDs, commit evidence, observed schema context, extractor
      versions, limitations, and owner follow-up.
- [x] Add sitemap metadata in `site/src/_site/pages.json`.
- [x] Add discovery metadata in `site/src/_site/discovery.json`.
- [x] Add adjacent inbound link from `/proof-paths/`.
- [x] Add focused validator and tests for the new route.
- [x] Wire focused validator into aggregate site validation.
- [x] Run `cd site && npm test`.
- [x] Run `cd site && npm run validate`.
- [x] Run `cd site && npm run build`.
- [x] Run desktop and mobile browser sanity checks if local tooling is
      available, or record blocker.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Update `implementation-state.md` with validation results, route
      decisions, oddities, and follow-up items.
