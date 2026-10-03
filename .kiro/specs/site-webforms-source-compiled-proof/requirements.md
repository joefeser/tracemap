# Requirements

## Purpose

Publish a public-safe, manager-readable Web Forms proof story that shows how
TraceMap keeps source evidence, review-tier source-to-compiled bridges, and
compiled IL evidence distinct while following one synthetic page handler
through VB.NET property getters, constructors, a separate provider DLL, and
database API terminals.

This is a site-only composition of functionality already present on `main`.
It does not add extraction, scanning, graph traversal, binary inspection, or
runtime behavior.

## Requirements

1. The site SHALL expose `/webforms/source-plus-compiled-proof/` as a readable
   ordered evidence story over only the checked-in public fixtures under
   `samples/fixture-build/lazy-constructor/`,
   `samples/messy-dotnet-workspace/vb-lazy-constructor/`, and
   `samples/messy-dotnet-workspace/vb-lazy-logging-provider/`.
2. The page SHALL identify its public claim level as `demo` only when
   implementation produces the compliant public proof projection required by
   this spec; otherwise it SHALL remain `concept` and record the projection
   blocker. Either claim level SHALL bind to the exact `main` revision selected
   during implementation and SHALL NOT silently inherit later `dev` behavior.
3. The story SHALL distinguish these evidence layers on every applicable hop:
   compiler-resolved source evidence (`Tier1Semantic`), metadata/IL structural
   evidence (`Tier2Structural`), review-only source-to-publish and encoded-value
   candidates (`Tier3SyntaxOrTextual`), and unresolved or unavailable evidence
   (`Tier4Unknown`). A candidate bridge SHALL never be presented as a proven IL
   call or exact source-method identity.
4. The primary walkthrough SHALL render a bounded, readable chain rather than
   serialized canonical identities. It SHALL preserve enough safe identity and
   provenance to review the page handler, property getter, constructor,
   provider boundary, and final `ExecuteScalar` or `Fill` terminal in order.
5. The story SHALL contrast three independently evidenced outcomes:
   a dynamic email lookup whose command value remains unresolved, a literal
   audit call with a retained static command-type/text state, and an independent
   `Fill` terminal. It SHALL not publish raw SQL, stored-procedure names,
   parameter values, source snippets, or literal hashes that could be mistaken
   for executable material.
6. Evidence detail SHALL preserve, where supported by the public projection,
   rule ID, evidence tier, coverage label, repository-relative file/span,
   supporting evidence identifiers, exact commit SHA, extractor or reporter
   version, terminal classification, and limitations. Missing fields SHALL be
   represented as gaps rather than inferred or filled from display names.
7. The page SHALL make unresolved value and coverage states first-class,
   including bounded path/work limits, missing or ambiguous evidence,
   source-to-compiled candidate status, virtual-dispatch uncertainty,
   unresolved operands, and reduced projectless coverage.
8. If implementation adds a machine-readable public proof projection, it SHALL
   be synthetic, allowlisted, deterministic, and privacy-projected. It SHALL
   record the exact generator SHA-256 and SHA-256 of the bounded
   privacy-projected input used to generate it. It SHALL never hash a private
   source artifact for a shareable output.
9. Reproduction guidance SHALL operate only on the checked-in public fixture,
   use repository-relative inputs, and state the supported host/toolchain
   boundary. It SHALL not require or imply access to a customer repository,
   private binary, credential, database, connection string, or local absolute
   path.
10. The page SHALL link from the planned Web Forms landing page, the existing
    legacy .NET evidence lane, and the manager packet. It SHALL also connect to
    relevant static-vs-runtime, gaps, limitations, proof-path, capability, and
    review-handoff pages without duplicating their authority.
11. The route SHALL be registered in `site/src/_site/pages.json`, discovery
    metadata, generated navigation/sitemap inputs, and the roadmap claim ledger.
    Discovery metadata SHALL name the actual `concept` or `demo` boundary,
    explicit limitations, preferred proof path, and non-claims.
12. Focused validators SHALL cover route structure, evidence-layer labels,
    ordered-chain shape, required inbound/outbound links, discovery and claim
    ledger entries, exact-main proof identity, projection schema/allowlists,
    hash shape and input-projection rules, sitemap inclusion, and forbidden
    public material.
13. Implementation SHALL run the full site build, test, and validation suites,
    the private-path guard, `git diff --check`, and desktop/mobile browser checks
    for the Web Forms landing page and the new proof route.

## Claim boundary

The public story may say that deterministic static evidence in the checked-in
synthetic fixture retains the displayed evidence relationships on the selected
exact `main` revision. It SHALL NOT claim runtime execution, browser or page
reachability, selected branches, warm/cold property behavior, successful
database calls, SQL text recovery, parameter binding, provider dispatch,
database identity, returned rows, build authenticity, deployed-binary identity,
customer compatibility, migration parity, complete route coverage, release
approval, or operational safety.

The implementation promoted by PR #801 is available on the current selected
`main` baseline. PR #803 is present on `dev` but not on that `main` baseline;
its scan-identity assertions and related hardening SHALL remain labelled
`dev`-only until independently promoted. No public shipped or demo claim may
depend on #803 behavior while that branch boundary remains true.
