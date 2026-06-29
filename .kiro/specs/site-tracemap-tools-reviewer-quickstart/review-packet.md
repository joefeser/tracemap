# Reviewer Quickstart Review Packet

Status: implemented
Readiness: implemented
Public claim level: concept

## Purpose

This packet defines a future public reviewer quickstart for inspecting a
TraceMap evidence packet without reading the whole site first. It is an
orientation guide for humans. It is not scanner behavior, reducer behavior,
runtime proof, production proof, release approval, operational safety,
AI/LLM analysis, embeddings, vector database analysis, prompt classification,
autonomous approval, or a replacement for tests, code review, source review,
runtime observability, or human judgment.

Shared principle: No public conclusion without evidence.

## Final Placement

Selected future route: `/reviewer-quickstart/`.

Rejected alternatives:

- `/review-room/quickstart/`: too closely tied to a meeting-room context.
- Section on `/review-room/`: would mix first-visit orientation with the
  review-room agenda.
- Section on `/packets/assembly/`: packet assembly is about preparing
  handoff material; this quickstart is about inspecting it.

## Required Sections

- `Start Here`
- `Five-Minute Review`
- `Evidence Fields`
- `Stop Conditions`
- `Safe Review Language`
- `Escalation Owners`
- `Non-Claims`

The future implementation must show `Public claim level: concept` and
`No public conclusion without evidence`.

## Five-Minute Review Steps

| Step | Reviewer check |
| --- | --- |
| identify the claim | Name the exact claim before reading supporting material. |
| find the proof path | Locate a public-safe route, summary, documented trail, or named private review location. |
| check public claim level | Keep concept, demo, shipped, hidden, or other supported vocabulary from being upgraded by repetition. |
| read rule ID/family | Confirm the deterministic rule basis and documented limitation. |
| inspect evidence tier and coverage label | Check tier and coverage together before repeating a claim. |
| check commit/extractor context | Confirm source revision and extractor context when public-safe and available. |
| read limitations/non-claims | Carry limitations and non-claims with the claim. |
| name next owner | Assign the remaining question to a human owner category. |
| stop on missing evidence | Do not repeat, upgrade, or approve unsupported claims. |

## Evidence Fields

Expected public-safe fields:

- claim
- proof path
- public claim level
- rule ID or rule family
- evidence tier
- coverage label
- commit SHA or source revision context
- extractor version or extractor family
- file path and line span when public-safe
- limitation
- non-claim
- validation evidence
- unresolved gap
- next owner

## Stop Conditions

Stop when the packet lacks proof path, rule ID or rule family, evidence tier,
coverage label, limitation, public claim level, validation evidence, next
owner, or commit/extractor context without an explicit limitation.

Stop when private-only support is presented as public proof.

Stop when review wording claims runtime behavior, production traffic, endpoint
performance, outage cause, release approval, release safety, operational
safety, complete coverage, AI/LLM analysis, embeddings, vector database
analysis, prompt classification, autonomous approval, or a replacement for
tests, code review, source review, runtime observability, or human judgment.

Stop when raw or private material appears, including raw facts, raw SQLite
content, analyzer logs, raw source snippets, raw SQL, config values, secrets,
local paths, raw remotes, generated scan directories, private sample names,
raw command output, hidden validation details, or credential-like values.

## Safe Review Language

Safe verbs: `inspect`, `check`, `follow`, `review`, `compare`, `label`,
`record`, `route`, `escalate`, and `cannot conclude from this packet`.

Safe sentence shape:

> This packet shows the reviewer where to inspect the public-safe proof path,
> rule basis, tier, coverage, context, limitations, and next owner before
> repeating the claim.

Do not use blame language around vendors, consultants, teams, or code quality.

## Escalation Owners

Use owner categories rather than names:

- reviewer owner
- source review owner
- code owner
- service owner
- database owner
- test owner
- validation owner
- telemetry or runtime owner
- release owner
- manager or decision owner

## Neighboring Surface Map

- `/review-room/`: meeting agenda for known, partial, and missing evidence.
- `/packets/assembly/`: workflow for preparing public-safe packet material.
- `/review-claim-checklist/`: decision ritual for whether a sentence may be
  repeated, downgraded, held, internal only, or not repeated.
- `/proof-paths/tour/`: guided reading tour for one proof path.
- `/proof-paths/`: proof-path overview and proof trail orientation.
- `/questions/`: stakeholder question routing.
- `/demo/manager-script/` or live equivalent: presenter wording, not reviewer
  inspection.
- `/demo/runbook/`: operator checklist for the public demo.
- `/limitations/`: broader claim and coverage boundaries.
- `/validation/`: public-safe validation expectations.

The reviewer quickstart is the entry point before deeper inspection. It should
route to these pages without becoming another packet assembler, review-room
agenda, claim ledger, proof catalog, question index, demo script, or runbook.

## Validation Expectations

Future implementation validation must cover required copy, links, route
metadata, sitemap metadata, discovery metadata, forbidden claims, private or
raw material, word count bounds, and desktop/mobile browser sanity.

Spec-only validation should include `git diff --check`,
`./scripts/check-private-paths.sh`, and focused text checks over the new spec
files.
