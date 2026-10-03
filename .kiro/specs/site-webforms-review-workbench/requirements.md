# Requirements

## Purpose

Publish a public-safe Web Forms review-workbench walkthrough that teaches a
reviewer how to inspect one checked-in synthetic handler, its readable
source/compiled route, supporting evidence, unresolved command candidate, and
coverage gap without mistaking static evidence for observed application
behavior.

This is a site-only composition over existing exact-`main` behavior and the
public proof asset planned by issue #806. It does not add or change scanner,
reporter, workbench, query, or review authority.

## Requirements

1. The site SHALL expose `/webforms/review-workbench/` as a static-first guided
   review of one existing public synthetic Web Forms handler.
2. The walkthrough SHALL consume or link to the public-safe proof asset and
   route produced by #806. It SHALL NOT copy, regenerate, reinterpret, or
   silently strengthen #806 evidence. Until the #806 asset is present on the
   implementation base and passes its validators, this route SHALL remain
   `concept`; after that gate, the walkthrough may use `demo` only for the
   checked-in synthetic projection it actually displays.
3. The walkthrough SHALL present a fixed review sequence: establish scope and
   provenance, select the synthetic handler, read the ordered route, inspect
   one evidence detail, inspect an unresolved command candidate, inspect one
   coverage gap, and record the next owner question or stop condition.
4. The readable route SHALL distinguish source evidence, published/compiled
   evidence, review-tier candidate bridges, and explicit gaps. It SHALL NOT
   present a source-to-publish candidate, callvirt candidate, display-name
   match, or type-only guess as an exact compiled call or runtime dispatch.
5. Every positive evidence detail SHALL retain its public-safe rule ID,
   evidence tier, coverage label, repository-relative file/span when available,
   supporting evidence reference, exact selected commit, extractor/reporter
   version, limitation, and source/index namespace needed to distinguish
   independently produced evidence. Missing detail SHALL become a visible gap,
   not a filled-in display value.
6. The unresolved command example SHALL preserve its categorical static state
   and gap reasons while withholding raw SQL, command text, procedure names,
   connection material, parameters, configuration values, source snippets, and
   executable command bodies. Reaching `ExecuteScalar` or `Fill` SHALL remain a
   static terminal observation, not proof of execution or success.
7. The page SHALL explain separately what a reviewer does when evidence is:
   partial, report-truncated, query-slice omitted, path/work limited,
   source-to-compiled ambiguous, or missing a required input. Each state SHALL
   name the next bounded question and owner or a stop condition; none SHALL be
   converted into absence or clean coverage.
8. Where the walkthrough discusses Web Forms client/server behavior inventory,
   it SHALL state that the packet independently retains at most 10,000 client
   behavior rows and at most 10,000 server behavior rows in deterministic
   order. Overflow SHALL name
   `WebFormsModernizationClientBehaviorLimitReached` or
   `WebFormsModernizationServerBehaviorLimitReached`, mark packet truncation,
   and SHALL NOT be described as a compiled-path, page, total-fact, or general
   workbench limit.
9. Generated public screenshots or projections, if added, SHALL be derived only
   from checked-in public synthetic evidence. Every new derived
   machine-readable artifact SHALL record the exact generator SHA-256 and a
   SHA-256 of its bounded privacy-projected input; a shareable artifact SHALL
   never hash private source, private binaries, customer evidence, or an
   unprojected local workbench.
10. The walkthrough SHALL publish no customer images, raw workbench outputs,
    raw handoffs, raw indexes, raw facts, analyzer output, source values, SQL,
    credentials, secrets, local paths, private infrastructure identities,
    private validation detail, or arbitrary artifact properties.
11. The route SHALL link to `/webforms/source-plus-compiled-proof/`, the Web
    Forms landing page, manager packet, manager proof paths, documentation and
    output orientation, static-vs-runtime guidance, limitations, evidence gaps,
    the generic review-room demo path, and the legacy modernization review
    handoff. The linked pages retain their existing authority and SHALL NOT be
    duplicated.
12. The route SHALL be registered in `site/src/_site/pages.json`, discovery
    metadata, generated navigation/sitemap inputs, and the roadmap claim
    ledger. Discovery SHALL state the actual public claim level, proof
    dependency, limitations, preferred proof path, and explicit non-claims.
13. Focused validation SHALL cover route structure, fixed step order, evidence
    detail fields, provenance identity, unresolved/gap language, independent
    inventory bounds, #806 dependency, #744 scope boundary, discovery,
    claim-ledger and sitemap records, required links, and forbidden public
    material and claims.
14. Implementation SHALL run the complete site build, tests, validation,
    private-path guard, and `git diff --check`, plus desktop and mobile browser
    checks for `/webforms/`, `/webforms/source-plus-compiled-proof/`, and
    `/webforms/review-workbench/`.

## Scope and branch boundaries

PRs #796, #797, and the #801 promotion place the readable compiled proof and
native private workbench/reporting workflow on the selected exact `main`
baseline. Issue #744 remains open and asks for a broader full-application
workbench contract. This public walkthrough SHALL NOT claim that #744 is
closed, that every requested private workbench feature shipped, or that every
application surface has a report.

PR #803 merged into `dev` but is not an ancestor of the selected `main`
baseline. Its Web Forms safety, diagnostic, and scan-identity hardening SHALL be
labelled `dev`-only until separately promoted. The independent 10,000-row
client/server behavior caps are present in exact-main implementation and may be
explained from that evidence; #803's later documentation or regression repairs
must not be cited as main-shipped proof.

## Non-claims

The walkthrough SHALL NOT claim runtime execution, page activation, browser or
user reachability, selected branches, effective command values, SQL execution,
database success, runtime dispatch, source/build authenticity, deployed-binary
identity, current external-input validation, complete application coverage,
arbitrary graph capacity, customer compatibility, migration parity, migration
effort, release approval, or operational safety. It SHALL NOT replace source,
database, runtime, migration, release, or application-owner review.
