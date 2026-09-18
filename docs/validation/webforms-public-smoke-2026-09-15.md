# Public Web Forms smoke baseline — 2026-09-15

This record captures a local rerun of the current Web Forms tooling against one
cached public legacy UI sample containing Web Forms artifacts. Raw outputs
remain ignored under `.tmp/legacy-codebase-validation/`; no source paths or
repository names are published here.

## Provenance and coverage

- Source commit: `4015b695e0d0641b817df276009cf7788098a29d`
- TraceMap commit: `9457fec3`
- Source snapshot digest:
  `9bf7ad8307006d9aeedea45ea431f5b6b1d5af64f65c38e4d218fc0d319d9fe6`
- Scanner version: `tracemap-milestone16`
- Web Forms extractor: `legacy-webforms/0.13.3`
- Scan outcome: `partial`
- Scan coverage: `semantic-reduced`
- Build status: `FailedOrPartial`

The labels above are part of the result. This baseline must not be described as
a clean build or complete semantic analysis.

## Observed retained evidence

The scan emitted 199,324 total facts and 347 Web Forms facts:

| Fact type | Count |
| --- | ---: |
| `WebFormsPageDeclared` | 21 |
| `WebFormsControlDeclared` | 25 |
| `WebFormsDesignerControlDeclared` | 33 |
| `WebFormsUserControlRegistered` | 20 |
| `WebFormsEventBindingDeclared` | 19 |
| `WebFormsHandlerResolved` | 60 |
| `WebFormsEventFlowProjected` | 60 |
| `WebFormsClientHttpHandlerDeclared` | 41 |
| `WebFormsClientHttpRequestCandidate` | 41 |
| `WebFormsCompositionDeclared` | 23 |
| `WebFormsLogicSignalDetected` | 4 |

The modernization packet retained 21 surfaces, 60 event chains, 7 downstream
boundaries, 32 identity/state declarations, 2 batch/data-movement declarations,
and 61 gaps. It was explicitly truncated under its configured bounds.

The generated application workbench retained 4,075 chain-associated call
projections, 1,299 unique retained call facts, 943 normalized call sites, and
54 page-associated gaps. These are static accounting values, not runtime call
counts.

## Regression use

This is an observed smoke record, not a golden exact-count test. A future run
should preserve nonzero page, binding, resolved-handler, event-flow, client HTTP,
and downstream-boundary evidence. Count changes require review of extractor
versions, source commit, coverage, ceilings, and gaps before being called a
regression or improvement.

Run the public corpus harness with the ignored local manifest, then generate a
Web Forms packet and workbench from that exact scan. Preserve the resulting
receipt and compare evidence families and coverage labels before exact totals.
