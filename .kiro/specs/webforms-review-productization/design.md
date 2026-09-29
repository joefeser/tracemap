# Design

Current implementation and operator contract:
[native workflow](../../../docs/WEBFORMS_NATIVE_WORKFLOW.md).
The planned flows below retain design history; completed native execution,
immutable attachment, indexed grouped reporting, query/status and protect-only
retention are recorded in `implementation-state.md`. Private Windows acceptance
still gates replacement of the working PowerShell wrappers.

## Product boundary

TraceMap owns deterministic static evidence. WITS owns human review decisions.
HACP/Routeboard owns authority, approvals, and external-task custody. ACK owns
implementation and review receipts. These layers exchange versioned artifacts;
they do not rewrite each other's evidence.

## Planned public flow

```text
authorized source
  -> focused scan
  -> selected-page packet
  -> evidence-docs corpus
  -> application workbench
  -> optional human/agent review
```

The PowerShell cleanup should introduce:

- `Initialize-FocusedWebFormsReview.ps1`: creates one ignored review root,
  config template, and README;
- `Invoke-FocusedWebFormsPipeline.ps1`: validates config, assigns a run receipt,
  invokes retained stages, and supports resume; and
- compatibility wrappers for the existing scripts during migration.

The config should store the source root, Web Forms root, backend root, controls
root, solution/project selection or projectless mode, output root, and selected
forms strategy. Lists must use JSON arrays rather than semicolon parsing inside
the persisted format.

## Planned private flow

```text
TraceMap candidate evidence
  -> WITS review overlay
  -> alias-only ticket-plan dry run
  -> HACP approval/custody
  -> external tracker write
  -> implementation
  -> ACK review receipt
```

The first ticket-plan version should group systemic coverage work separately
from application-page review, cap fan-out, and require explicit approval before
external writes. Commercial or restrictive licensing may apply to workflow,
approval, and integration layers; it must not retroactively obscure public
evidence formats or scanner claims. License selection requires separate legal
review.

## Privacy

Private artifacts may retain authorized paths, symbols, and source excerpts.
Public/shareable artifacts use aliases, bounded counts, generic classifications,
and privacy-projected provenance only. Never attempt to reverse aliases from
counts or hashes.

## Compiled Web Site integration

The scan CLI accepts `--compiled-input`, `--compiled-dependency`, and
`--compiled-binding-receipt`. The native `webforms-review start` command now
composes explicit preparation, preflight, source-plus-compiled collection or
immutable attachment, and reports. The legacy focused pipeline retains its
separate JSONC contract. A selected-handler proof is not an all-site claim;
native selected/all modes retain their declared membership and coverage gaps.

Implemented flow: validate one private configuration, inspect/hash the declared published
assemblies, establish the admitted source binding, scan source plus compiled
inputs, then generate packet/docs/workbench within one durable run root.
Probing and binding can be separate internal phases without becoming separate
operator commands. No source checkout mutation or automatic publishing.

For an existing scan, compiled-evidence attachment creates a new run/index
bound to the parent scan and exact DLL hashes. The original scan remains
immutable. Do not treat a source hash alone as proof of how a DLL was built.

The run manifest and append-only checkpoint journal own phase states, dependency hashes, selected/all-page scope,
capabilities, budget usage, output paths, and completion/partial markers. The
workbench is the main navigation entry; handoff JSON links its retained
evidence. Supplemental paths keep their rule IDs, tiers, exact identities,
coverage gaps, and review-only status instead of overriding page verdicts.

Scale work must separate streaming file hashing from graph admission and
traversal. Favor indexed selected-root queries and shared identity references
over materializing or repeating the entire graph. Preserve competing-symbol,
overload, and dispatch evidence; optimization must not fabricate uniqueness.
Any sharding must retain cross-assembly edges and global ambiguity checks.
Configure resource budgets explicitly and report incomplete evidence before
considering parallelism. The present combined reader can reject the whole
graph at its admission ceiling; its partial packet is recovery, not scalable
compiled graph integration. The native path now uses bounded indexed scratch
storage and queries, with measured 32/256-page sparse public corpus admission.
That measurement does not establish arbitrary private graph capacity.

The native operator guide documents the implemented command contract. Public
fixtures, migration/resume and representative synthetic scale gates are recorded
in implementation-state and validation notes. Authorized real-run validation
still gates replacement of existing wrappers. Protect-only retention plans
never authorize deleting retained proof dependencies.
