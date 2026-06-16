# Site TraceMap Tools LLM Discovery Pack Tasks

Public claim level: demo
Status: ready-for-implementation / not started

- [ ] 1. Add the static discovery entry point.
  - [ ] Add `llms.txt` to the published site using existing static site
    patterns.
  - [ ] Link public-safe routes for evidence, outputs, validation, limitations,
    demo proof, capabilities, docs, and roadmap where those routes exist.
  - [ ] Include concise non-claims for AI impact analysis, runtime behavior,
    production usage, deployment state, endpoint performance, and release
    approval.

- [ ] 2. Add concise machine-readable docs indexes.
  - [ ] Add `/docs-index.json` for public-safe source-of-truth repository
    documents.
  - [ ] Add `/routes-index.json` for public-safe site route discovery.
  - [ ] Include route path or URL, title or label, public claim level where
    applicable, source-of-truth type, short summary, and limitations metadata.
  - [ ] Keep repository docs distinct from site presentation pages.
  - [ ] Exclude private paths, raw source snippets, raw SQL, config values,
    secrets, raw fact streams, SQLite databases, analyzer logs, and local output
    roots.

- [ ] 3. Add public-safe navigation hints.
  - [ ] Route bots toward evidence, limitations, generated public-safe
    artifacts, and source-of-truth docs before roadmap or use-case copy.
  - [ ] Preserve demo/concept/hidden/planned status labels where applicable.
  - [ ] Keep the shared site principle visible in source metadata or generated
    discovery text: no public conclusion without evidence.

- [ ] 4. Preserve the main/dev wording boundary.
  - [ ] Describe `main` evidence as available only when public pages or
    repository docs already support it.
  - [ ] Describe `dev`-only or queued work as planned, in progress, or future
    implementation, not shipped proof.
  - [ ] Avoid stronger claims than the linked evidence supports.

- [ ] 5. Keep implementation static and product-safe.
  - [ ] Do not edit scanner, reducer, language adapter, or report generation
    code.
  - [ ] Do not add runtime services, product LLM calls, embeddings, vector
    databases, prompt-based classification, or AI impact-analysis workflows.
  - [ ] Keep source metadata under `site/src` and generated output under
    `site/dist`.

- [ ] 6. Validate.
  - [ ] Run `npm test` from `site/`.
  - [ ] Run `npm run validate` from `site/`.
  - [ ] Run `npm run build` from `site/`.
  - [ ] Verify `llms.txt` and machine-readable indexes include expected routes,
    claim-level labels, non-claims, and no public-unsafe artifacts.
  - [ ] Run `git diff --check`.
  - [ ] Record branch, validation, review findings, and follow-ups in this
    spec's implementation-state note.
