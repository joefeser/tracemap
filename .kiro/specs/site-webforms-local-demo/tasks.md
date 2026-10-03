# Site Web Forms Local Demo Tasks

## Specification

- [x] Read issue #807 and its acceptance, dependency, and claim boundaries.
- [x] Inspect the exact `origin/main` `wlocal` runner, validation wrapper,
  synthetic fixture documentation, existing Web Forms site surfaces, and site
  metadata/validator patterns.
- [x] Verify PR #801's public Windows result and tree-equivalent `main` merge
  evidence without copying its raw retained artifacts into the site.
- [x] Verify PR #803 is an ancestor of `origin/dev` but not `origin/main`, and
  record the resulting public-claim gate.
- [x] Define the route, artifact-map contract, proof reuse, clean-clone/failure
  matrix, claim vocabulary, privacy boundary, discoverability, and validation
  plan.

## Future implementation

- [x] Reverify `origin/main`, PR #801's exact-tree durable result, and PR #803's
  ancestry immediately before implementing public copy.
- [x] Confirm issue #806's public-safe projection is merged and has the fields
  needed by the selected examples; stop or separately specify any new
  machine-readable projection rather than importing raw run artifacts.
- [x] Add `/webforms/local-demo/` with the replay choice, fresh-clone recipe,
  artifact map, layout/query examples, receipt provenance, failure matrix,
  gaps, limitations, and owner next steps.
- [x] Add page/discovery/sitemap metadata with explicit claim levels,
  limitations, and non-claims.
- [x] Update the site claim ledger and focused secondary links from guided
  setup, source-plus-compiled proof, validation, examples, outputs,
  limitations, capabilities, roadmap, and later the workbench.
- [x] Add focused route, provenance, hash, Windows-boundary, failure-matrix,
  claim, link, responsive-layout, and private/raw-data validators with mutation
  regressions.
- [x] Verify the documented instructions from a disposable clean clone or
  equivalent exact-tree worktree and exercise the cheap fail-closed guard
  cases; record Windows-only cases from their exact durable runner evidence.
- [x] Run focused tests, `cd site && npm test`, `npm run build`,
  `npm run validate`, `./scripts/check-private-paths.sh`, and
  `git diff --check`.
- [x] Perform desktop and mobile browser checks for `/webforms/local-demo/`
  and its guided-setup/source-proof link path.
- [x] Record the implementation branch, exact base/head, validation, browser
  results, public claim levels, limitations, PR, and ACK state in
  `implementation-state.md`.
