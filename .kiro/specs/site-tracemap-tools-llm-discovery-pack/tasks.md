# Site TraceMap Tools LLM Discovery Pack Tasks

Public claim level: demo
Status: not-started
Readiness: ready-for-implementation

- [ ] 1. Add the static discovery entry point. Requirements: 1, 3, 4.
  - [ ] Add `llms.txt` to the published site using existing static site
    patterns.
  - [ ] Link public-safe routes for evidence, outputs, validation, limitations,
    demo proof, capabilities, docs, and roadmap where those routes exist.
  - [ ] Include concise non-claims for AI impact analysis, runtime behavior,
    production usage, deployment state, endpoint performance, and release
    approval.

- [ ] 2. Add concise machine-readable docs indexes. Requirements: 2, 4, 5.
  - [ ] Add a checked-in authoritative source file such as
    `site/src/_site/discovery.json` for title, summary, public claim level,
    preferred proof path, limitations, and non-claims.
  - [ ] Add `/docs-index.json` for public-safe source-of-truth repository
    documents.
  - [ ] Add `/routes-index.json` for public-safe site route discovery.
  - [ ] Include route path or URL, title or label, public claim level where
    applicable, source-of-truth type, short summary, and limitations metadata.
  - [ ] Keep repository docs distinct from site presentation pages.
  - [ ] Sort generated JSON entries deterministically by path or URL.
  - [ ] Pin repository document URLs to stable public refs such as `main` or a
    release tag.
  - [ ] Exclude private paths, raw source snippets, raw SQL, config values,
    secrets, raw fact streams, SQLite databases, analyzer logs, and local output
    roots.

- [ ] 3. Add public-safe navigation hints. Requirements: 1, 3, 5.
  - [ ] Route bots toward evidence, limitations, generated public-safe
    artifacts, and source-of-truth docs before roadmap or use-case copy.
  - [ ] Preserve demo/concept/hidden/planned status labels where applicable.
  - [ ] Keep the shared site principle visible in source metadata or generated
    discovery text: no public conclusion without evidence.

- [ ] 4. Preserve the main/dev wording boundary. Requirements: 2, 3, 4.
  - [ ] Describe `main` evidence as available only when public pages or
    repository docs already support it.
  - [ ] Describe `dev`-only or queued work as planned, in progress, or future
    implementation, not shipped proof.
  - [ ] Avoid stronger claims than the linked evidence supports.

- [ ] 5. Keep implementation static and product-safe. Requirements: 4, 5.
  - [ ] Do not edit scanner, reducer, language adapter, or report generation
    code.
  - [ ] Do not add runtime services, product LLM calls, embeddings, vector
    databases, prompt-based classification, or AI impact-analysis workflows.
  - [ ] Keep source metadata under `site/src` and generated output under
    `site/dist`.

- [ ] 6. Validate. Requirements: 5.
  - [ ] Run `npm test` from `site/`.
  - [ ] Run `npm run validate` from `site/`.
  - [ ] Run `npm run build` from `site/`.
  - [ ] Add or update `site/scripts/*.test.mjs` coverage for output
    generation, JSON field schemas, deterministic ordering, route/proof-path
    resolution, claim-level preservation, main/dev labeling, and required
    non-claims.
  - [ ] Extend validation to inspect `llms.txt`, `/docs-index.json`, and
    `/routes-index.json`, not only HTML, sitemap, and robots files.
  - [ ] Add a denied-token check for public discovery outputs covering private
    paths, raw SQL indicators, connection string fragments, raw fact streams,
    SQLite file names, analyzer logs, and forbidden positioning phrases unless
    they are present as explicit non-claims.
  - [ ] Expose `/llms.txt` through `robots.txt` or direct links; only add
    `.txt` or `.json` discovery files to sitemap output if sitemap validation
    is explicitly updated for those exact file paths.
  - [ ] Verify `llms.txt` and machine-readable indexes include expected routes,
    claim-level labels, non-claims, and no public-unsafe artifacts.
  - [ ] Run `git diff --check`.
  - [ ] Record branch, validation, review findings, and follow-ups in this
    spec's implementation-state note.
