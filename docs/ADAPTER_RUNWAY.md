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

## Next Direction: Deepen Before Broadening

The next major product direction is to make the current evidence lanes sharper
before adding another language family. TraceMap should feel boringly credible
for the ecosystems it already supports: C#/.NET including legacy .NET, Swift,
TypeScript, Python, and JVM. That means better proof packets, sample validation,
route-flow examples, public-safe reports, and "how to read this evidence"
documentation before chasing a wider adapter matrix.

SQL and data-surface evidence should be treated as part of this deepening work,
not as a separate product detour. It connects existing route, service, client,
config, package, and dependency evidence to the data-risk conversations people
actually have during incidents, migrations, audits, rewrites, and ownership
reviews.

### Adapter conformance foundation

The adapter family now has a machine-enforced verification foundation:

- CI runs the .NET, TypeScript, Python, JVM, and Swift suites;
- every lane generates a real scan and validates the shared artifact contract;
- manifest/fact JSON schemas, canonical minimum SQLite DDL, and a shared
  redaction corpus live under `contracts/artifacts/`;
- SQLite facts preserve extractor ID/version across every adapter;
- a final CI job combines all five adapter indexes and generates the combined
  dependency report.

This foundation enforces artifact compatibility without pretending five
language implementations can share one runtime library. Fact-ID formula
standardization remains a versioned compatibility decision: v1 requires stable
adapter-local IDs and preserved source IDs through combine, not identical
cross-language identity formulas.

### Microsoft Access design-evidence runway

Microsoft Access is a bounded data-design evidence follow-up, not a new language
family and not a departure from the deepen-before-broadening direction. A local
feasibility pass established that installed Access/DAO automation can expose
deterministic structural metadata without reading rows or executing saved
queries. The proposed v0 runway is specified in
`.kiro/specs/microsoft-access-adapter-v0-runway/`.

Implementation remains gated by stronger product controls than the feasibility
prototype: a clean Git-tracked database at a concrete commit, a verified private
working copy, forced startup/macro suppression, hostile non-execution canaries,
standard TraceMap artifacts, rule-backed gaps, and raw SQL/connection/VBA/macro
suppression. The first slice is schema, relationships, saved-query shape, and
external-boundary hashes only. Forms/reports, VBA flow, macros, composition, and
public claims remain separate follow-ups.

The near-term priority order is:

1. Strengthen current evidence lanes.
   - keep .NET/legacy .NET, Swift, TypeScript, Python, and JVM evidence
     consistent with rule IDs, evidence tiers, coverage labels, limitations,
     and explicit gaps;
   - improve proof packets, validation harnesses, representative sample scans,
     and public-safe "how to read the evidence" pages;
   - make route-flow, property-flow, dependency, release-review, vault/RAG, and
     site stories easier to demo without raw source, raw SQL, local paths, or
     runtime claims.
2. Build SQL/data-surface depth across existing adapters.
   - detect static SQL/query candidates, ORM/query-builder surfaces, schema or
     migration files, table/procedure/view/function references, and connection
     or config surfaces where deterministic evidence supports them;
   - preserve safe identifiers, hashes, rule IDs, evidence tiers, coverage
     labels, spans, and limitations;
   - report data-surface gaps when dynamic SQL, runtime mapping, provider
     behavior, hidden schema, generated code, or unsupported ORM metadata
     prevents a credible conclusion.
3. Compose endpoint-to-service-to-data reports.
   - show route or client entry evidence, selected service/method symbols,
     dependency edges, SQL/data/config/package surfaces, source files, line
     spans, supporting fact IDs, gaps, and limitations in one review packet;
   - keep the output explicitly static: no SQL execution, database existence,
     endpoint reachability, production traffic, dependency-injection runtime
     resolution, branch feasibility, auth behavior, or release-safety claims.
4. Add another language family only after the code-to-data story is solid.
   - prefer a language because it unlocks a real buyer workflow or validation
     sample, not because broad language count looks better;
   - keep any new adapter narrow, deterministic, and contract-compatible before
     adding language-specific depth.

This sequencing keeps TraceMap's product story coherent: it is not merely a
code indexer and not a chatbot over source files. It is an evidence map that can
show how static code, route, dependency, and data-surface facts connect, while
making the unknowns visible.

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
