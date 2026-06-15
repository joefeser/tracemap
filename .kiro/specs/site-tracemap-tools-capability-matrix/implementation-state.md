# Implementation State

Status: implemented
Last verified: 2026-06-15
Branch: codex/site-capabilities-matrix
Source of truth: working tree
Public claim level: demo

## Summary

This spec adds a static `/capabilities/` page to `tracemap.tools`. The page
summarizes TraceMap workflow groups, command names, language adapter maturity,
demo-safe artifacts, local-only artifacts, row-level status/proof text, and
explicit non-claims.

The page uses the repository README, validation guide, acceptance plan, rule
catalog, and coordinator capability inventory as source material. It keeps
TraceMap framed as deterministic static analysis and avoids runtime proof,
AI-impact-analysis, vulnerability-scanning, telemetry, or release-approval
claims.

Capabilities that exist only on `dev` must be labeled as dev-only or omitted
until dev-to-main promotion lands. After promotion, qualifying rows can be
upgraded from demo/dev language to shipped language with proof paths.

## Validation

- `npm test`
- `npm run validate`

## Follow-ups

- Consider a deeper public demo walkthrough once generated sample reports are
  stable enough to summarize without publishing local-only artifacts.
