# Design

## Route and claim posture

Add `/webforms/review-workbench/` using the established static HTML layout and
site visual vocabulary. The page simulates the reading order of a bounded
review; it is not a browser-hosted TraceMap workbench, live query interface,
upload surface, generated report, or private artifact viewer.

The preferred claim posture is `demo` only after the #806 public-safe proof
projection is available on the implementation base and the page consumes that
exact validated projection. If the dependency is absent, incomplete, or unsafe
to reuse, publish only concept-level guidance or defer the route; do not create
substitute evidence.

## Walkthrough model

The route uses seven numbered steps in one contiguous region:

1. **Scope and provenance** — show the public fixture label, exact selected
   commit, public projection schema/hash commitments, extractor/reporter
   versions, coverage, truncation state, and limitations.
2. **Choose the handler** — select the one authored synthetic handler supplied
   by #806. The selection is display/navigation state, not runtime entry proof.
3. **Read the route** — render the ordered page/property/constructor/provider/
   terminal evidence with per-hop source, compiled, candidate, or gap labels.
4. **Open evidence detail** — expand or link one hop's rule ID, tier,
   repository-relative span, supporting reference, namespace, and limitation.
5. **Inspect the unresolved command** — show the safe categorical
   `unresolved-operand`/call-result state and its retained gap kinds without
   command text or source.
6. **Inspect a coverage gap** — explain one missing, ambiguous, limited, or
   candidate-only transition and why it blocks a stronger conclusion.
7. **Ask or stop** — name the smallest next question for the evidence, source,
   database, runtime, or application owner, or stop when no public-safe evidence
   supports continuation.

The full sequence must remain readable without JavaScript. Native disclosure
elements may provide optional detail, but expanded/collapsed state must not hide
the claim level, coverage, gap, or non-claim from assistive technology.

## Evidence vocabulary

Use the #806 proof model directly:

- compiler-resolved source evidence is `Tier1Semantic` only where a supporting
  public fact exists;
- metadata and encoded IL observations are `Tier2Structural`;
- projectless source-to-publish bridges, callvirt targets, command bindings, and
  encoded value substitutions remain `Tier3SyntaxOrTextual` review candidates;
- missing, changed, ambiguous, over-budget, or unresolved evidence remains
  `Tier4Unknown` or an explicit gap.

Do not assign one route-wide tier. Every displayed hop retains its own evidence
class. Readable labels are projections over exact identities; they are never
join keys.

## Provenance display

The provenance card should render only fields already admitted by the #806
public projection:

- repository identifier and exact public commit;
- projection schema and public claim level;
- generator SHA-256 and bounded privacy-projected input SHA-256;
- extractor/reporter versions;
- source/index namespace or equivalent public-safe origin key;
- safe supporting evidence IDs;
- repository-relative paths and spans; and
- coverage/truncation/gap state.

Do not expose local source roots, raw binary paths, private scan IDs, private
DLL fingerprints, raw index locations, or local run roots. Exact provenance
means identity fields agree; it does not authenticate a build, deployment, or
current external input.

## Partial, truncated, and missing evidence

Render a reviewer decision table:

| Evidence state | What remains known | Next bounded question | Stop condition |
| --- | --- | --- | --- |
| Reduced/partial source analysis | Retained lower-tier evidence and named gaps | Which project/toolchain/input evidence is missing, and who owns it? | Stop before semantic, clean, or complete wording. |
| Packet truncated | Retained deterministic prefix plus exact truncation reason | Which configured packet inventory or graph bound was reached? | Stop before absence or complete inventory claims. |
| Query slice omitted | Returned children plus explicit omission marker | Which exact child pointer or smaller slice is needed? | Stop before treating an omitted child as absent. |
| Path/work limited | Retained paths and limit gap | Can the owner authorize a narrower question or justified bound change? | Stop before complete reachability claims. |
| Candidate bridge only | Static review candidate and its support | Is exact PDB/source identity or unambiguous compiled evidence available? | Stop before calling the candidate an IL call or runtime target. |
| Required input missing | Existing independent evidence plus input gap | Can the evidence owner supply the exact authorized bounded input? | Stop; never substitute a nearby/newer artifact or infer absence. |
| Command value unresolved | Terminal call shape and categorical value-origin gap | Does a source/database owner have separate evidence for the value question? | Stop before publishing or guessing command text. |

## Behavior-inventory bound

When applicable to the displayed modernization-packet summary, render client
and server behavior inventories as two independent bounds:

- client behavior inventory: deterministic first 10,000 rows, then
  `WebFormsModernizationClientBehaviorLimitReached`;
- server behavior inventory: deterministic first 10,000 rows, then
  `WebFormsModernizationServerBehaviorLimitReached`.

Either overflow marks the packet truncated. The inventory-specific gap is
retained only when the configured packet gap budget has room for it. If that
budget is already saturated, `WebFormsModernizationGapLimitReached` can replace
the later client/server overflow classification, so the page and validators
must not require a specific overflow gap independently of gap-budget state.
These are inventory bounds, not a 10,000-handler, page, compiled-route,
graph-node, or overall workbench promise. If the #806 fixture projection
contains neither behavior inventory nor overflow, the page may explain the
bound in a limitations panel but must not invent an observed limit event.

## Public asset strategy

Reuse `/webforms/source-plus-compiled-proof/` and its planned public JSON asset
instead of adding a second serialized route. The authored workbench page should
reference allowlisted fields from that asset at build/validation time.

If a screenshot is materially useful, generate it only from the public
synthetic page and inspect it for private material. If a new JSON or other
machine-readable projection is added, version it, bind its generator SHA-256
and bounded privacy-projected input SHA-256, validate it recursively, and
document why #806's asset was insufficient. A screenshot or projection is
illustration, not independent product proof.

## Relationship to existing pages

- `/webforms/` owns setup and workflow entry.
- `/webforms/source-plus-compiled-proof/` owns the evidence projection and
  exact chain claim.
- `/review-room/demo-path/` remains the generic concept-level public review
  sequence; this route specializes it for one Web Forms synthetic proof.
- `/manager-packet/` and `/proof-paths/for-managers/` own manager framing.
- `/outputs/`, `/docs/`, and `/proof-paths/` own artifact and proof-path
  orientation, not raw private workbench access.
- `/legacy-modernization/review-handoff/` owns the broader owner handoff.
- `/static-vs-runtime/`, `/limitations/`,
  `/limitations/reduced-coverage/`, and `/evidence/gaps/` own cross-product
  boundary language.
- `/roadmap/#claim-ledger` records the route's actual concept/demo state and
  must not generalize it to complete Web Forms support.

Issue #744 remains the broader private full-application workbench tracker. This
route does not close it or claim every acceptance criterion in that issue.

## Discovery and validation

Register the route in `site/src/_site/pages.json` and
`site/src/_site/discovery.json`; let the existing build create route and sitemap
output. Do not edit `site/dist` or `site/output`.

Add a focused `site/scripts/webforms-review-workbench.test.mjs` regression and
shared validation coverage for:

- canonical/social metadata, one `h1`, and static-first markup;
- conditional claim level and verified #806 dependency;
- exact seven-step order and required step fields;
- readable route ordering and per-hop evidence classifications;
- public-safe provenance, rule, tier, span, version, coverage, limitation, gap,
  owner-question, and stop-condition fields;
- unresolved command copy that contains no raw value;
- independent client/server 10,000-row bounds, packet truncation, and the
  gap-budget-dependent specific/generic gap labels only in the packet-inventory
  context;
- explicit #744 and #803 boundaries;
- pages/discovery/roadmap/sitemap records and required inbound/outbound links;
- recursive rejection of raw/private workbench material and sensitive fields;
  and
- forbidden runtime, complete-coverage, compatibility, migration, release, and
  safety claims.

Implementation validation:

```text
cd site && npm run build
cd site && npm test
cd site && npm run validate
./scripts/check-private-paths.sh
git diff --check
```

Perform browser checks at representative desktop and mobile widths for
`/webforms/`, `/webforms/source-plus-compiled-proof/`, and
`/webforms/review-workbench/`. Confirm the numbered steps remain in order,
evidence/gap labels do not depend on color, details are keyboard-operable, wide
identity content wraps without horizontal page overflow, links resolve, and no
page console errors occur.
