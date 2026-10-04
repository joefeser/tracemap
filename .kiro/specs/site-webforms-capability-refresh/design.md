# Site Web Forms Capability Refresh Design

## Claim ladder

| Surface | Public claim level | Boundary |
| --- | --- | --- |
| Guided terminal setup | `shipped` | Explicit target/scoping, persisted state, consent, pause/resume, and stop conditions on `main`; Windows-only build/publication gates remain separate. |
| Source + compiled projection | `concept` | Privacy-projected illustration with candidate bridges and unresolved values; not independently projected extractor output. |
| Reproducible local demo | `demo` | Exact-tree public synthetic validation and artifact orientation; not runtime or customer evidence. |
| Review-workbench walkthrough | `demo` | Authored seven-step reading workflow over the concept projection; not a live workbench or complete application view. |

## Page changes

- Add a four-part evidence ladder and explicit future-work boundary to
  `/capabilities/`.
- Keep separate claim-ledger rows on `/roadmap/` and add one future automation
  row rather than a broad Web Forms promotion.
- Add exact setup and walkthrough rows to the legacy .NET and modernization
  matrices while keeping the broad Web Forms row hidden.
- Add a Web Forms evidence-ladder handoff row for owners.
- Cross-link all four stories from manager and proof-path surfaces.
- Replace stale forward-looking workbench copy in the local demo.
- Update discovery descriptions and limitations without changing mixed-page
  route-level claim levels.

## Validation

`webforms-capability-refresh.mjs` validates the eight audited public surfaces,
the four exact story links, the claim ladder, the future-work boundary,
discovery metadata, privacy restrictions, and prohibited overclaims. Focused
tests plant claim drift, missing links, and forbidden public material.
