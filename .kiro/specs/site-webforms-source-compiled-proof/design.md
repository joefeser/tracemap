# Design

## Route and narrative

Add `/webforms/source-plus-compiled-proof/` as a site-owned demonstration page
using the existing static visual system. The route is a proof walkthrough, not
a source browser and not a serialized report viewer.

The planned narrative is:

1. identify the checked-in synthetic page handler and its exact-main proof
   boundary;
2. show the ordered source/compiled chain through the property getter and
   constructor work;
3. mark the transition into the separate provider DLL;
4. split the walkthrough into the dynamic email, literal audit, and `Fill`
   outcomes;
5. end each outcome at the static database API terminal with its evidence tier,
   rule, coverage, gap, and non-claim still visible; and
6. route the reviewer to the next owner question rather than claiming a runtime
   result.

Short display labels are allowed only as presentation. Each displayed hop must
retain a stable, allowlisted reference to its supporting public evidence. The
page must never join or order methods from display names alone.

## Evidence-layer model

The visual model uses four labeled layers:

| Layer | Intended public meaning | Representative rule families | Required caution |
| --- | --- | --- | --- |
| Compiler-backed source | A compiler resolved a source declaration or call shape in an admitted project context. | `database.operation.call-pattern.v1` and applicable VB semantic rules | Does not prove execution, branch selection, SQL, or runtime dispatch. |
| Compiled metadata and IL | Admitted managed bytes contain exact metadata, body, and encoded call evidence. | `dotnet.compiled.member.v1`, `dotnet.compiled.il-body.v1`, `dotnet.compiled.il-call.v1` | Does not prove freshness, source ownership, deployment, reachability, or execution. |
| Review-tier bridge/value | A bounded deterministic candidate connects otherwise separate evidence layers or substitutes an encoded value candidate. | `combined.paths.compiled-il-bridge.v1`, `combined.paths.projectless-publish-candidate.v1`, `combined.paths.compiled-command-value.v1`, `dotnet.compiled.il-command-binding.v1` | Candidate evidence is not an exact source-to-IL call, runtime value, or provider selection. |
| Explicit gap | Required evidence is missing, ambiguous, unresolved, changed, or beyond a configured bound. | `dotnet.compiled.gap.v1`, `dotnet.compiled.il-gap.v1`, `AnalysisGap` facts from the bridge rules | A gap means reduced observation coverage, never absence. |

The projectless fixture can contain multiple layers in one route. The page must
not flatten the route to its strongest tier; every hop keeps its own tier and
limitations. Compiler-resolved source and projectless source-to-publish
candidate evidence must also remain separate in the explanation. A layer may
appear as a comparison or gap rather than a positive hop when the selected
fixture projection has no supporting fact for it.

## Public proof projection

The preferred implementation is one checked-in, site-owned JSON projection,
for example `/assets/webforms-source-compiled-proof.json`, generated from a
bounded allowlisted projection of the public fixture evidence. Its schema should
be versioned independently of private/local TraceMap artifacts.

Required top-level fields:

- `schemaVersion` and `publicClaimLevel` (`demo`);
- repository identity and exact public commit SHA;
- generator identity plus `generatorSha256`;
- a description of the bounded privacy projection plus
  `boundedInputSha256`;
- extractor/reporter versions used by the selected proof;
- coverage and truncation state;
- ordered path groups for dynamic email, literal audit, and `Fill`;
- explicit gaps and limitations; and
- reproduction metadata limited to checked-in public inputs.

Only allowlisted public-fixture labels, repo-relative paths, line spans,
categorical states, rule IDs, evidence tiers, safe supporting IDs, versions,
counts, booleans, and SHA-256 digests may be admitted. Raw source, raw SQL,
configuration, command bodies, parameter values, connection material, absolute
paths, raw SQLite, analyzer output, arbitrary fact properties, and private
identities are rejected recursively and case-insensitively.

The generator hash identifies the exact generator bytes. The bounded input hash
must be computed from the canonical privacy-projected input, including rejected
or unresolved categorical evidence needed to preserve the claim boundary. It
must not be a hash of a private scan, private binary, private source tree, or
unprojected local artifact.

If implementation determines that a compliant projection cannot be produced
without exposing or hashing unsafe inputs, it must omit the machine-readable
asset, keep the page at `concept`, record the blocker, and not fabricate demo
proof.

## Page composition

Reuse existing page primitives:

- a hero with an explicit `demo` note and exact proof revision;
- an evidence-layer legend;
- an ordered chain/timeline with keyboard-readable text fallback;
- three outcome cards for dynamic email, literal audit, and `Fill`;
- a gap and stop-condition panel;
- a provenance/versions panel;
- a reproducibility panel bounded to public fixtures; and
- next-review links for managers and evidence reviewers.

No client-side graph library or new runtime service is needed. The route remains
static-first and must remain understandable without JavaScript.

## Existing-page overlap and links

- `/webforms/` (planned by #805) owns setup/onboarding and links into this proof
  as the evidence walkthrough.
- `/blog/modernizing-web-forms-without-running-it/` remains a concept overview;
  it should link to this concrete synthetic proof without duplicating the chain.
- `/legacy-dotnet/evidence/` currently marks Web Forms hidden. Implementation
  should promote only the exact public-fixture row supported by this proof and
  leave unsupported Web Forms capabilities hidden or concept-level.
- `/legacy-modernization/evidence-map/` and
  `/legacy-modernization/review-handoff/` keep the broader planning and owner
  handoff roles.
- `/manager-packet/`, `/proof-paths/for-managers/`, `/capabilities/`,
  `/limitations/`, `/limitations/reduced-coverage/`,
  `/static-vs-runtime/`, and `/roadmap/#claim-ledger` provide the existing
  claim and review vocabulary.

The route may later provide proof assets for #807 and walkthrough context for
#808. Those stories must link to this route rather than copy or silently change
its evidence claims.

## Discovery and validation

Add the route to `site/src/_site/pages.json` and `site/src/_site/discovery.json`.
Allow the existing build to generate the route and sitemap; never edit
`site/dist` or `site/output`.

A focused `site/scripts/webforms-source-compiled-proof.test.mjs` regression and
the shared validators should assert:

- canonical/social metadata and one `h1`;
- `demo` claim level and exact-main provenance;
- all four evidence-layer labels and the three required outcomes;
- no bridge is labelled as a proven IL call;
- gaps, limits, non-claims, versions, rule IDs, tiers, and spans remain visible;
- projection fields and SHA-256 values are present and correctly shaped when an
  asset is published;
- the bounded input is explicitly privacy-projected;
- required discovery, sitemap, roadmap, and inbound links exist;
- protected fields/material are rejected recursively; and
- forbidden runtime, compatibility, completeness, customer, release, and
  safety claims are absent.

Implementation validation:

```text
cd site && npm run build
cd site && npm test
cd site && npm run validate
./scripts/check-private-paths.sh
git diff --check
```

Perform browser checks at representative desktop and mobile widths for
`/webforms/` and `/webforms/source-plus-compiled-proof/`. Confirm the ordered
chain remains readable, evidence labels do not rely on color alone, horizontal
content does not overflow, links resolve, and there are no page console errors.
