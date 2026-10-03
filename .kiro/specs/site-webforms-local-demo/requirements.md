# Site Web Forms Local Demo Requirements

## Goal

Publish issue #807 as a reproducible, public-safe Web Forms local demo and
artifact map. The story must help an evaluator run the checked-in synthetic
corpus and understand which retained artifacts answer which review questions,
without publishing the retained raw artifacts or turning static evidence into
runtime, compatibility, migration, or approval claims.

## Audience question

“Can I reproduce the public Web Forms evidence locally, and which artifact
should I inspect for attached, separate-provider, compiled-only, missing,
bounded, and repeated evidence?”

## Requirements

1. The site SHALL expose `/webforms/local-demo/` as the canonical evaluator
   route for the one-command `scripts/wlocal.ps1` public synthetic workflow.
2. The route SHALL name PowerShell 7 and the documented .NET/toolchain
   prerequisites, link to `docs/VALIDATION.md`,
   `docs/WEBFORMS_NATIVE_WORKFLOW.md`, and
   `samples/fixture-build/lazy-constructor/README.md`, and distinguish the
   default local replay from `-RequireWindowsPublish`.
3. The route SHALL present an artifact map for the `attached`, `separate`,
   `separate-dll-only`, `reversed`, and `missing` layouts and the `all`,
   `fill`, `capped`, and `repeat` query views. It SHALL explain the validation
   receipt, test receipt, report, path, comparison, unresolved-value, and gap
   roles without publishing raw indexes, raw facts, logs, source, or local
   filesystem locations.
4. The route SHALL show selected public-safe examples for:
   - an attached source-plus-compiled route;
   - a separately scanned provider route;
   - a compiled-only separate-DLL route;
   - missing-provider refusal with an explicit gap;
   - a bounded result with its cap/truncation gap;
   - repeatability of admitted projected paths and gaps; and
   - reversed input order retaining the same admitted static route shape.
5. Each example SHALL preserve its evidence class, rule ID, evidence tier,
   coverage label, source/compiled/candidate distinction, explicit gap or
   unresolved state, limitations, and provenance reference. The page SHALL
   reuse the public-safe proof projection created for issue #806 rather than
   copying raw `wlocal` output. If that projection is unavailable when #807 is
   implemented, the implementation SHALL stop or introduce a separately
   reviewed privacy projection that satisfies Requirement 10.
6. Validation counts SHALL be reported only with an exact tested commit or an
   explicitly verified tree-equivalent merge commit, a durable public result
   URL, platform, command mode, generator SHA-256, bounded privacy-projected
   input SHA-256, pass/fail/skip counts, and Windows publication status.
   Historical counts SHALL remain labeled historical and SHALL NOT be reused as
   proof for a different tree or current head.
7. The authentic ASP.NET mapped/mapless publication row SHALL be Windows-only.
   A non-Windows skip, ordinary SDK build, or external fixture harness SHALL
   not be displayed as an authentic ASP.NET publication pass.
8. Clean-clone instructions SHALL start from a fresh checkout of the exact
   advertised commit, require a fresh output root, and preserve failure
   artifacts. The story SHALL explain at least these fail-closed outcomes:
   unsupported Windows-required invocation, non-fresh output root, failed test
   receipt, missing required layout, changed bounded input or generator, cap or
   repeat mismatch, and missing Windows acceptance when requested.
9. The route SHALL state that a successful repeated run proves only the
   admitted synthetic static corpus at its tested tree. It SHALL NOT claim that
   a prior intermittent failure was fixed unless the failing condition has its
   own reproduced cause and exact-head regression evidence.
10. Any new public machine-readable derivative SHALL include the exact
    generator SHA-256 and a SHA-256 of its bounded privacy-projected input.
    The input hash SHALL cover only the public projection, never a raw/private
    source artifact. Public JSON SHALL be allowlisted and SHALL exclude raw
    SQLite, `facts.ndjson`, logs, source snippets, raw SQL or command text,
    credentials, connection material, customer data, private identities,
    machine-local paths, and unpublished analyzer output.
11. The route and metadata SHALL use `demo` only for statements directly
    supported by checked-in synthetic evidence and an admitted durable result.
    Workflow explanation without a matching public proof SHALL remain
    `concept`; unavailable or unsafe-to-publish evidence SHALL remain `hidden`.
12. The route SHALL explicitly deny runtime execution, database execution,
    branch feasibility, runtime dispatch, customer compatibility, private
    application coverage, migration parity, complete cross-service tracing,
    physical drive-I/O measurement, release approval, and safety-to-run claims.
13. The route SHALL cross-link with the guided setup route from issue #805, the
    source-plus-compiled proof route from issue #806, the workbench route from
    issue #808 when available, and the existing Web Forms overview, legacy
    .NET evidence, evidence map, review handoff, validation, examples, outputs,
    limitations, capabilities, and roadmap surfaces.
14. The implementation SHALL update `site/src/_site/pages.json`, discovery
    metadata with limitations and non-claims, sitemap/navigation metadata,
    the site claim ledger, and only the focused secondary links needed for
    discoverability. It SHALL not add primary-navigation clutter.
15. Focused validation SHALL cover route structure, responsive artifact-map
    behavior, discovery and sitemap membership, inbound/outbound links,
    claim-level vocabulary, exact-result provenance, Windows/non-Windows
    separation, hash fields, required gaps, and forbidden private/raw material.
16. The implementation SHALL run the focused validator and tests, full site
    tests/build/validation, `./scripts/check-private-paths.sh`,
    `git diff --check`, and desktop/mobile browser checks for
    `/webforms/local-demo/` and the linking Web Forms routes.

## Shipped-state boundary

The implementation promoted by PR #801 is on `main`. PR #803's diagnostics and
scheduling repairs are on `dev` but are not ancestors of `main` at this spec's
base. The future page MUST NOT describe those repairs as shipped until a fresh
implementation-time ancestry check proves they reached `main`.
