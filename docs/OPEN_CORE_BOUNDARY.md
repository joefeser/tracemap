# TraceMap Open-Core Boundary

This document is the authoritative boundary between TraceMap's open evidence
engine and any future commercial or services layer. When another product or
positioning document is ambiguous, this boundary controls.

## Principle

TraceMap must not charge users for trustworthy evidence. Anything required to
avoid false evidence, lost paths, incorrect identity joins, misleading
provenance, or hidden partial coverage belongs in the open product.

The commercial value is operating the evidence safely at organizational scale,
not making the evidence more correct.

## Open Evidence Engine

The Apache-2.0 core includes:

- deterministic scanners, reducers, and language adapters;
- source, metadata, PDB, and IL identities plus evidence-backed links between
  those identities;
- fact schemas, rule IDs, evidence tiers, limitations, and coverage labels;
- local CLI workflows and machine-readable and human-readable artifacts;
- explicit gaps, truncation, partial-analysis, and failed-build behavior;
- provenance, receipt, redaction, and artifact-integrity behavior required to
  trust an individual result;
- public and synthetic validation fixtures, cross-language equivalence cases,
  hostile or legacy input fixtures, and reproducible validation instructions;
- correctness fixes discovered through private samples, once reproduced with
  safe public or synthetic fixtures; and
- reasonable local execution on supported environments.

An experimental analysis that does not meet this correctness bar must be
labeled preview, partial, or unsupported. It must not become a paid claim merely
because its behavior is uncertain.

## Commercial or Services Layer

A future paid layer may provide:

- hosted, managed, distributed, or fleet-scale scanning;
- repository portfolio orchestration and scheduled rescans;
- evidence history, long-term retention, and cross-repository change views;
- team dispositions, approvals, policy gates, audit workflows, and custody;
- enterprise identity, SSO/RBAC, repository integrations, and compliance
  exports;
- managed Windows legacy-analysis workers and managed private-corpus execution;
- service-level commitments, onboarding, consulting, custom adapters, and
  dedicated support; and
- licensed evidence feeds for other review systems.

Commercial workflows may add convenience, scale, governance, and human-approved
decisions. They must preserve the open engine's rule provenance, coverage, and
limitations rather than replacing them with opaque conclusions.

Paid custom-adapter work means implementation, integration, and support for a
customer-specific environment. Generic adapter logic required to make a
publicly supported language or framework correct belongs in the open engine.

## Validation and Private Samples

Validation infrastructure is part of product correctness even when a fixture
cannot be redistributed. Access-controlled external corpora such as the
historical `dotnetperf` repository may inform validation on authorized private
workers, but they are not public reproducibility fixtures. Public repositories,
synthetic language-equivalence projects, and sanitized reproductions of real
failures should pin the open behavior wherever licensing and safety allow.

Private customer or employer samples remain private. They may reveal a defect,
but a public fix requires an independently reproduced fixture with no private
source, identifiers, paths, configuration, credentials, or data.

## Decision Test

Ask these questions when classifying work:

1. Could omitting this work make a local TraceMap result wrong, incomplete
   without warning, incorrectly attributed, or unsafe? If yes, it is open-core
   correctness.
2. Does the work primarily add organizational scale, hosting, retention,
   workflow, identity, policy, or support? If yes, it may be commercial.
3. Does a commercial feature consume evidence? It must not weaken or hide the
   open evidence contract.

Short form: **the open product proves the evidence; paid offerings operate,
retain, and govern that evidence at scale.**
