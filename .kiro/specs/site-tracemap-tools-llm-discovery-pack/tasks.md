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
  - [ ] Include route path or URL, title or label, required public claim level,
    source-of-truth type, short summary, and limitations metadata.
  - [ ] Require `sourceType` as `site-page` or `repo-doc`; entries without it
    fail schema validation.
  - [ ] Require `hintCategory` as `start`, `evidence`, `limitations`, `demo`,
    `repo-doc`, `roadmap`, or `use-case`; invalid values fail schema
    validation.
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
  - [ ] Add validation proving evidence and limitation hints appear before
    roadmap or use-case hints in generated discovery output.
  - [ ] Add validation proving the shared site principle appears in at least one
    generated output.

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
  - [ ] Assert `site/dist/discovery.json` does not exist after build; the
    checked-in discovery source is private build input, not public output.
  - [ ] Assert `site/dist/llms.txt`, `site/dist/docs-index.json`, and
    `site/dist/routes-index.json` exist after build.
  - [ ] Include an empty-input fixture proving all three public outputs are
    still written and valid.
  - [ ] Add `preferredProofPath` fixtures proving absent is allowed and skips
    proof-path resolution, present empty is invalid, and present non-empty
    internal paths resolve against `site/dist`.
  - [ ] Treat empty `preferredProofPath` as `null`, empty string, or
    whitespace-only; unresolved internal paths fail with a sanitized public-path
    message.
  - [ ] Add `llms.txt` parsing tests that split on `## ` H2 headings and allow
    denied phrases only after `## Non-Claims` and before the next H2 heading or
    EOF.
  - [ ] Prove denied-token exceptions apply only to direct string values in a
    `nonClaims` array, not nested structures or other fields.
  - [ ] Include a fixture with a planned or dev-only entry and assert generated
    output uses planned, in progress, future, or dev-only wording and does not
    describe the entry as available or shipped.
  - [ ] Include denied-token exception fixtures proving a forbidden phrase
    passes only inside a `nonClaims` array value or the `## Non-Claims` section
    of `llms.txt`, and fails in title, summary, limitations, or route hints.
  - [ ] Include hint-ordering and shared-site-principle assertions for generated
    discovery outputs.
  - [ ] Use `hintCategory` to validate that evidence and limitation hints appear
    before roadmap and use-case hints.
  - [ ] Include stable-sort fixtures proving repeated builds emit deterministic
    JSON order and source-entry reordering still emits the same canonical
    sorted output.
  - [ ] Add a test asserting `llms.txt` H2 sections appear in this order:
    `Start Here`, `Evidence And Proof`, `Limitations`, `Demo`,
    `Repository Docs`, `Non-Claims`.
  - [ ] Extend validation to inspect `llms.txt`, `/docs-index.json`, and
    `/routes-index.json`, not only HTML, sitemap, and robots files.
  - [ ] Add a denied-token check for public discovery outputs covering private
    paths, raw SQL indicators, connection string fragments, raw fact streams,
    SQLite file names, analyzer logs, and forbidden positioning phrases unless
    they are present as explicit non-claims.
  - [ ] Expose `/llms.txt` through a plain `robots.txt` comment as the baseline;
    add direct site links if useful and record which option was implemented.
  - [ ] Do not add `.txt` or `.json` discovery files to sitemap output in the
    initial implementation.
  - [ ] Verify `llms.txt` and machine-readable indexes include expected routes,
    claim-level labels, non-claims, and no public-unsafe artifacts.
  - [ ] Run `git diff --check`.
  - [ ] Record branch, validation, review findings, and follow-ups in
    `.kiro/specs/site-tracemap-tools-llm-discovery-pack/implementation-state.md`.
