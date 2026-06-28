# Adapter Runway

TraceMap's already-scoped legacy .NET lane has reached v0. The goal was not
perfect framework analysis; the goal was evidence-backed change-risk discovery
with clear confidence, gaps, and limitations.

## Legacy .NET V0 Completion

Legacy .NET v0 is complete as of the route-flow Task 10 validation and follow-up
hardening merged through `dev`. The lane now has deterministic static evidence
for old .NET project/toolchain diagnostics, ASP.NET routes, WebForms and
WinForms events, WCF/SVC, ASMX/SOAP, .NET Remoting, legacy data metadata,
legacy ORM descriptor evidence, route-flow, property-flow, evidence packs, and
public-safe reporting/export paths.

The v0 completion boundary is:

- route-flow service/data composition safety gates are complete;
- representative public-safe legacy sample validation is recorded through the
  legacy sample evidence pack, smoke catalog, route-flow, data metadata, and
  property-flow slices;
- UI field lineage, legacy data model depth, static dispatch approximation, and
  export polish remain valid follow-ups, not blockers for Swift v0;
- the known unsupported and approximation register remains active for old framework
  patterns that TraceMap recognizes but cannot safely resolve;
- reports and exports explain confidence, evidence tier, rule ID, source
  coverage, and limitations for approximate evidence;
- future legacy .NET work should be selected as focused depth or polish slices,
  not treated as unfinished v0 foundation.

Known approximation boundaries should remain explicit. TraceMap may emit
evidence for WCF/SVC, ASMX, remoting, WebForms/WinForms events, legacy route
patterns, old data mappings, and static dispatch candidates, but it must not
claim runtime behavior, branch feasibility, dynamic binding, serializer runtime
contracts, dependency-injection resolution, reflection targets, or complete UI
navigation unless the evidence proves that specific claim.

### Current Legacy .NET State

The route-flow final spec and follow-up hardening now record the validation
evidence that previously blocked v0 completion. Remaining unchecked items in
legacy specs are continuation work unless their implementation-state file says
otherwise. Use those follow-ups for targeted depth after Swift v0 planning or
when a specific customer/sample needs the extra precision.

## Swift V0 Completion

Swift v0 is implemented on `dev` through the Swift adapter delivery series:

- #401: scaffold CLI and required output contract;
- #405: inventory and project/package discovery;
- #411: SwiftSyntax declarations, calls, and construction candidates;
- #412: source-local symbol identity and direct relationships;
- #414: package/dependency surfaces;
- #416: HTTP/API client surfaces;
- #420: reduced-coverage toolchain diagnostics;
- #421: SwiftUI/UIKit UI surfaces;
- #423: storage/data surfaces.

Swift v0 is narrow and evidence-first. It now includes:

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

Swift v0 does not promise perfect Swift analysis, complete app navigation, or
runtime UI behavior. Swift-specific fact types follow the
[language adapter contract](LANGUAGE_ADAPTER_CONTRACT.md): deterministic IDs, evidence tiers, rule
IDs, extractor versions, file spans, coverage labels, sorted metadata, and safe
redaction.

Remaining Swift work should be treated as post-v0 depth or polish, not as a
blocker to the adapter-family proof:

- SourceKit/sourcekit-lsp or compiler semantic enrichment;
- SwiftPM/Xcode semantic project loading or build execution;
- simulator/device/runtime instrumentation;
- protocol witness resolution, Objective-C dispatch, overload selection, and
  runtime dynamic dispatch;
- deeper SwiftUI state/navigation and Interface Builder wiring;
- cross-repo mobile-client to backend route alignment;
- public-site copy that must wait for dev-to-main promotion.

## Product Framing

TraceMap is the risk and evidence discovery engine. Its value is showing what a
static scan can prove, what it can approximate, and where the analysis must
stop. Legacy .NET v0 proves the engine can handle difficult old code with
honest gaps. Swift v0 proves the same evidence model can cross into mobile
application code without becoming a rewrite or a giant analyzer promise.

Adapter output should stay useful for future what-is-the-spec (WITS) and
Agent Control Kit (ACK) overnight classification: stable artifacts, rule IDs,
evidence tiers, coverage labels, machine-readable gaps, and human-readable
limitations. The classifier should be able to consume TraceMap evidence without
needing private repo paths, raw code snippets, raw SQL/config values, secrets,
or runtime-only claims.
