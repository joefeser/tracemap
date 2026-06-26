# Adapter Runway

TraceMap's next adapter work should finish the already-scoped legacy .NET lane
before starting a new ecosystem. The goal is not perfect framework analysis; the
goal is evidence-backed change-risk discovery with clear confidence, gaps, and
limitations.

## Legacy .NET V0 Finish Line

Legacy .NET v0 is complete when the already-specified cleanup work is closed or
explicitly deferred in the relevant spec state files:

- finish the route-flow service/data composition safety gate, then explicitly
  close or defer the remaining UI field lineage, legacy data model, and static
  dispatch candidate slices that are already specified;
- maintain a known unsupported and approximation register for old framework
  patterns that TraceMap recognizes but cannot safely resolve;
- ensure reports and exports explain confidence, evidence tier, rule ID, source
  coverage, and limitations for approximate evidence;
- run representative public-safe legacy sample scans and record the validation
  evidence without storing private local paths or raw source snippets;
- mark the legacy .NET lane as v0 complete only after the lane can explain what
  it found, what it could not prove, and what requires review.

Known approximation boundaries should remain explicit. TraceMap may emit
evidence for WCF/SVC, ASMX, remoting, WebForms/WinForms events, legacy route
patterns, old data mappings, and static dispatch candidates, but it must not
claim runtime behavior, branch feasibility, dynamic binding, serializer runtime
contracts, dependency-injection resolution, reflection targets, or complete UI
navigation unless the evidence proves that specific claim.

### Current Legacy .NET Gate

As of the route-flow Task 9 merge recorded in
`.kiro/specs/route-flow-service-data-composition-final/implementation-state.md`,
`dev` has completed the route-flow service/data composition implementation
through deterministic JSON/Markdown compatibility. The remaining route-flow
gate is the public-safe validation slice in
`.kiro/specs/route-flow-service-data-composition-final/` Task 10. Do not mark
legacy .NET v0 complete until that Task 10 slice is merged or explicitly
deferred with evidence in that spec's `tasks.md` and `implementation-state.md`.

After Task 10, the next legacy .NET completion order is:

1. Reconcile route-flow spec state and representative validation evidence.
2. Run public-safe legacy sample scans and record what each sample proves, what
   is partial, and what remains unsupported.
3. Decide whether UI field/property lineage and static dispatch approximation
   are required for legacy .NET v0 or should be deferred behind Swift v0. Record
   the decision in each affected spec's `tasks.md` and
   `implementation-state.md`.
4. Update the unsupported/approximation register before any `dev` to `main`
   promotion that claims legacy .NET v0.

## Swift V0 Candidate Scope

Swift should be the next adapter family after legacy .NET v0. Swift v0 should
be narrow and evidence-first:

- project and package discovery for Swift packages and common Xcode project
  shapes;
- symbols for types, functions, classes, structs, enums, extensions, and
  protocols when deterministic evidence is available;
- deterministic call or navigation edges where parser/compiler evidence can
  support them safely;
- SwiftUI view and route-like surfaces only when they are statically
  evidence-backed;
- UIKit controller and action surfaces only for simple deterministic patterns;
- package and dependency surfaces suitable for combined portfolio and
  dependency reporting;
- explicit gaps for dynamic runtime behavior, reflection, Objective-C bridging,
  storyboard complexity, macros, generated code, conditional compilation, and
  unsafe inference.

Swift v0 should not promise perfect Swift analysis, complete app navigation, or
runtime UI behavior. Any Swift-specific fact types must follow the
[language adapter contract](LANGUAGE_ADAPTER_CONTRACT.md): deterministic IDs, evidence tiers, rule
IDs, extractor versions, file spans, coverage labels, sorted metadata, and safe
redaction.

## Product Framing

TraceMap is the risk and evidence discovery engine. Its value is showing what a
static scan can prove, what it can approximate, and where the analysis must
stop. Legacy .NET v0 proves the engine can handle difficult old code with
honest gaps. Swift v0 is the next adapter-family proof that the same evidence
model can cross into mobile application code without becoming a rewrite or a
giant analyzer promise.

Adapter output should stay useful for future what-is-the-spec (WITS) and
Agent Control Kit (ACK) overnight classification: stable artifacts, rule IDs,
evidence tiers, coverage labels, machine-readable gaps, and human-readable
limitations. The classifier should be able to consume TraceMap evidence without
needing private repo paths, raw code snippets, raw SQL/config values, secrets,
or runtime-only claims.
