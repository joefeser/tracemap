# Implementation State

Status: not-started
Readiness: ready-for-implementation
Branch: codex/site-llm-discovery-pack
Public claim level: demo

## Summary

This spec queues a future `tracemap.tools` discovery pack for bots, crawlers,
documentation agents, and LLM-based assistants. The intended output is static
discovery metadata: `llms.txt`, `/docs-index.json`, `/routes-index.json`, and
public-safe route hints.

No site implementation, generated output, scanner code, reducer code, language
adapter code, report generation code, LLM calls, embeddings, vector databases,
or prompt-based classification are implemented in this spec-prep PR.

## Scope

- Spec files only under
  `.kiro/specs/site-tracemap-tools-llm-discovery-pack/`.
- Future site source may live under `site/src` when implementation begins.
- Future generated outputs may be produced under `site/dist` by the normal site
  build.
- Public copy remains bounded to deterministic static evidence.

## Scope Decisions

- Treat the discovery pack as a demo-level site feature because it routes to
  public demo/proof/docs surfaces; it does not promote production claims.
- Keep the feature static and reviewable.
- Use discovery metadata to route users and bots, not to infer conclusions.
- Keep repository docs as source-of-truth references and site pages as
  presentation or navigation surfaces.
- Preserve the shared site principle: no public conclusion without evidence.

## Main/Dev Wording Boundary

- `main` evidence may be described as available only when public pages or
  repository docs already support the statement.
- `dev`-only or queued work should be labeled planned, in progress, or future
  implementation.
- Roadmap and concept pages must not be summarized as shipped proof.
- Public-facing discovery text should prefer stable `https://tracemap.tools`
  URLs and public repository links over branch-local implementation details.

## Non-Claims

- Discovery metadata only.
- No AI impact-analysis claims.
- No LLM, embedding, vector database, or prompt-based classification features
  in TraceMap core.
- No runtime traffic, production usage, deployment state, release approval,
  endpoint performance, or absence-of-impact proof.
- No publication of private paths, raw source snippets, raw SQL, config values,
  secrets, raw fact streams, SQLite databases, analyzer logs, or local output
  roots.

## Validation Plan

Spec-only delivery validation:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model claude-opus-4.8 --fresh
node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model claude-sonnet-4.6 --fresh
git diff --check
```

If named Kiro review models are unavailable, run:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model auto --fresh
```

## Spec-Prep Review Log

Current spec-prep branch validation:

- Passed:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model claude-opus-4.8 --fresh`
- `claude-sonnet-4.8` was unavailable locally; subsequent validation uses the
  available Sonnet model documented above.
- Passed fallback:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model auto --fresh`
- Review-loop remediation added `design.md` and normalized `Status:
  not-started` plus `Readiness: ready-for-implementation`.
- Opus spec review then found implementability gaps around discovery metadata
  source authority, sitemap exposure for non-HTML outputs, and validation of
  generated discovery files. The spec was patched to require an authoritative
  discovery source file, robots/direct-link exposure by default, deterministic
  JSON ordering, stable repo-doc refs, and explicit non-HTML output validation.
- Fallback spec review reported no blocking issues. The Medium implementability
  note was patched by documenting existing static-file copy behavior for public
  non-HTML source files.
- Sonnet review found additional implementation ambiguities. The spec was
  patched to make `discovery.json` a private build-time input, keep discovery
  files out of sitemap output for the initial implementation, define structural
  non-claim exceptions, and add concrete validation tasks for hint ordering,
  planned-status labels, deterministic sort order, and implementation-state
  updates.
- Follow-up Sonnet review requested explicit enforcement tasks for
  `site/dist/discovery.json` absence, `preferredProofPath` three-state
  validation, and `llms.txt` non-claim section parsing. The spec now defines
  those validator contracts and fixtures.
- Final Sonnet precision pass requested positive output-existence checks,
  explicit empty-value semantics, direct-string-only non-claim exceptions, and
  unresolved proof-path failure behavior. The spec now captures those details,
  plus `hintCategory`, required output existence, empty-input behavior, and
  robots-comment baseline exposure.

Future implementation validation:

```bash
cd site
npm test
npm run validate
npm run build
```

Then manually inspect `llms.txt` and the machine-readable indexes for expected
routes, claim-level labels, non-claims, public-safe artifact boundaries, and the
absence of AI impact-analysis claims.

## Follow-Ups To Keep Out Of This Slice

- Implementing `llms.txt`.
- Editing `site/src`.
- Editing generated `site/dist` or `site/output`.
- Adding scanner, reducer, language adapter, report generation, or CLI changes.
- Adding LLM calls, embeddings, vector databases, prompt-based classification,
  or AI impact-analysis workflows.
