# Site TraceMap Tools Proof Source Catalog Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: demo

## Design Scope

This design is for a future site implementation. It records page structure,
catalog schema, anchors, hidden-row handling, and cross-link strategy so future
implementers do not have to infer them from requirements prose. This branch is
spec-only and does not implement site code.

The catalog's distinguishing axis is route-to-source mapping: it answers which
route is allowed to make a claim and which source material backs that route's
wording. It is not a claim-wording ledger, a capability status matrix, a
roadmap gate explanation, or an evidence-trail index. For those, link outward.

## Placement Decision

Preferred first evaluation: add a section to `/proof-paths/`, because that page
already maps public routes to evidence trails.

Standalone fallback: `/proof-source-catalog/`.

The implementation must record the final placement and rejected alternatives in
`implementation-state.md`. If the standalone route is chosen, it uses existing
site page metadata, sitemap metadata, discovery metadata, and validation
patterns.

## Catalog Schema

Each catalog row is a public-safe object with these required fields:

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `route` | public route path or `hidden` | Yes | Use a public route such as `/docs/`; hidden aggregate row may use `hidden`. |
| `claimLabel` | stable text label | Yes | Human-readable label for the claim family. |
| `allowedPublicWording` | bounded phrase or claim family | Yes | Must not include forbidden runtime, production, safety, AI, LLM, or complete-coverage claims. |
| `publicClaimLevel` | enum | Yes | Exactly one of `shipped`, `demo`, `concept`, or `hidden`. |
| `evidenceStatus` | enum | Yes | Exactly one allowed evidence-status label. `not-yet-backed` is a pre-publication blocker: any candidate row with this status must be removed or rewritten before the page publishes; it must not appear in any published catalog row. |
| `proofPath` | public-safe route, source-doc link, or sentinel | Yes | If no public-safe proof path exists, use exactly one sentinel: `future-only` (no resolvable source yet), `hidden` (internal-only, no public path), or `blocked-pending-validation` (exists but not yet safe to publish). Free-text `not available` is not an accepted value. |
| `sourceArtifactOrDoc` | source family or doc reference | Yes | Names the source-of-truth family without publishing raw private artifacts. |
| `ruleIdOrFamily` | rule ID or rule-family text | Yes | Use a direct rule ID only when public-safe. |
| `evidenceTierOrCoverage` | tier, coverage label, or explicit gap | Yes | Preserve source labels such as `Tier2Structural`, `PartialAnalysis`, or `unavailable`. |
| `limitation` | non-empty text | Yes | Explains the exact proof boundary. |
| `nonClaims` | non-empty text or list | Yes | States what the row must not be used to claim. |

## Row Anchors

Rows use deterministic anchors:

```text
proof-source-{route-slug}-{claim-slug}
```

Examples:

- `proof-source-docs-repository-doc-navigation`
- `proof-source-proof-paths-public-evidence-trails`
- `proof-source-hidden-aggregate-placeholder`

Route slugs remove leading and trailing slashes, replace remaining slashes with
hyphens, and use `home` for `/`. Claim slugs are lowercase ASCII words joined
with hyphens.

Route slugs strip any fragment identifier and query string before applying slug
rules. Trailing `/index` is treated as equivalent to the bare route by dropping
it before slug derivation. Validation must assert the derived slug is non-empty
after stripping.

The claim slug is derived from `claimLabel`: lowercased; non-ASCII characters,
punctuation including apostrophes, slashes, and parentheses, and leading or
trailing hyphens removed; digits retained; and runs of whitespace or removed
characters collapsed to single hyphens. A slug consisting entirely of hyphens
after this process is invalid and must be corrected at authoring time.
Validation derives the expected anchor from `route` and `claimLabel` and asserts
it matches the row's published anchor.

Reserved exception: the single hidden aggregate placeholder uses the fixed
reserved anchor `proof-source-hidden-aggregate-placeholder`. Validation permits
this reserved anchor explicitly and does not derive it from `claimLabel`.

## Hidden Aggregate Placeholder

Hidden material is omitted by default. If the future implementation needs a
hidden row, it may publish at most one aggregate placeholder with:

- anchor `proof-source-hidden-aggregate-placeholder`
- `route: hidden`
- `Public claim level: hidden`
- `evidenceStatus: hidden-or-internal`
- `claimLabel: Internal-only aggregate placeholder`
- `allowedPublicWording: none`
- `proofPath: hidden`
- `sourceArtifactOrDoc: hidden`
- `ruleIdOrFamily: none`
- `evidenceTierOrCoverage: hidden`
- a limitation stating that details are not disclosed publicly
- a non-claims field stating that the row does not represent any specific
  capability, route, private sample, count, cadence, sequence, or in-flight work

The placeholder still carries every required catalog field. It must not name
unreleased capabilities, hidden routes, private samples, counts, cadence,
sequencing, or in-flight status.

## Cross-Link Strategy

The catalog links outward instead of duplicating neighboring surfaces:

- Link to `/proof-paths/` for evidence-trail rows.
- Link to the future claim ledger (`/claims/` or `/claim-ledger/`) only once it
  is a live route and when claim wording, claim level, evidence status,
  limitations, or non-claims already exist there. Until then, reference the
  `site-tracemap-tools-claim-ledger` spec without a hyperlink so link-resolution
  validation stays green.
- Link to `/roadmap/` for claim-gate explanations.
- Link to `/capabilities/` for capability status rows.
- Link to `/docs/`, `/validation/`, and `/limitations/` for source-doc,
  validation, and boundary context.

Cross-link text must not upgrade concept or hidden rows into available
capabilities.

## Validation Design

Future validation should parse or inspect the rendered catalog data and assert:

- page-level `Public claim level: demo`
- all required row fields are present
- `limitation` and `nonClaims` are non-empty
- row-level `publicClaimLevel` uses only `shipped`, `demo`, `concept`, or
  `hidden`
- public rows do not use `not-yet-backed`; any candidate row with that status is
  removed or rewritten before publish
- `publicClaimLevel` and `evidenceStatus` pairs match the allowed matrix from
  requirements, and `hidden-or-internal` appears only on the hidden aggregate
  placeholder with route `hidden`
- hidden rows are absent or collapse to the single aggregate placeholder format
- proof paths are public-safe and resolvable or use exactly one allowed sentinel
- row anchors are unique
- forbidden raw/private artifact text and forbidden overclaims are absent

Forbidden public wording patterns apply to affirmative claim fields only:
`claimLabel`, `allowedPublicWording`, and any non-disclaimer body copy that
asserts a capability. They must not be applied to `limitation`, `nonClaims`, or
the global non-claims section, where these terms appear intentionally as negated
boundaries, such as `does not perform AI impact analysis or LLM analysis`.
Validation must detect affirmative use, not negated disclaimer use.

Affirmative public wording must reject these patterns or their variants:

- raw artifact disclosure: `facts.ndjson`, `index.sqlite`, `analyzer.log`,
  `logs/analyzer.log`
- private path indicators: absolute paths, `~`, `/Users/`, `/home/`, Windows
  drive patterns matching `[A-Za-z]:\` with any drive letter, and UNC paths
  (`\\`)
- private sample names or repository remotes: any `.git` URL, any `git@`
  remote, or any private sample, project, or app name listed in the canonical
  denylist used by `scripts/check-private-paths.sh`. That script is the single
  source of truth for private name tokens; the forbidden-name check must reuse
  it rather than maintaining a separate list.
- hidden-work disclosure: numeric counts of hidden capabilities or repositories,
  such as a digit followed by `hidden`, `internal`, `private`, or `in-flight`;
  cadence references such as `weekly`, `monthly`, or `sprint` in hidden-row
  context; and sequencing indicators such as `next`, `upcoming`, or `phase N`
  on hidden rows
- runtime proof claims: `proven in production`, `runtime-safe`,
  `production-verified`, `traffic-proven`
- safety or release claims: `release-safe`, `operationally safe`,
  `outage cause`, `release approval`
- AI or LLM claims: `AI impact analysis`, `LLM analysis`, `embedding-backed`,
  `prompt-classified`
- completeness claims: `complete coverage`, `full product coverage`,
  `all endpoints proven`

This list is a starting set; future validation may extend it, but must not
remove existing entries without recording the reason in `implementation-state.md`.

The word-count bound should be applied to row fields, such as `limitation` and
`allowedPublicWording`, rather than only to the page total, so bot-oriented row
inspection remains useful. The implementation records chosen bounds in
`implementation-state.md` before marking word-count validation complete.

Implementation validation is deferred to the future site implementation phase.
