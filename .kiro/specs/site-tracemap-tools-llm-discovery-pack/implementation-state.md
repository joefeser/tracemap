# Implementation State

Status: ready-for-implementation / not started
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
node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model claude-sonnet-4.8 --fresh
git diff --check
```

If the named Kiro review models are unavailable, run:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model auto --fresh
```

## Spec-Prep Review Log

Current spec-prep branch validation:

- Attempted:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model claude-opus-4.8 --fresh`
- Attempted:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model claude-sonnet-4.8 --fresh`
- Attempted fallback:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model auto --fresh`
- Result: the repo wrapper stopped before model invocation because `--kind spec`
  currently requires `design.md`; this spec-prep slice intentionally contains
  only the requested `requirements.md`, `tasks.md`, and
  `implementation-state.md` files.
- Fallback review performed: checked owned-file scope, not-started task status,
  public claim level, non-claims, main/dev wording boundary, shared site
  principle, concrete discovery output paths, and public-safe artifact
  exclusions.

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
