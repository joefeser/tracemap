# Design

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

