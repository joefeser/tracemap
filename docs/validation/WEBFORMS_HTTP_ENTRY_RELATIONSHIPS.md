# WebForms HTTP entry relationships (#873)

Starting baseline: `25c4f6475db9b92b3f08dafafd5117a554e3020a` from freshly fetched
origin/dev. This is a bounded static entry inventory, not runtime reachability.

## Coverage matrix

| Family | Supported | Partial / unavailable | Evidence and first unresolved boundary |
| --- | --- | --- | --- |
| Containment | One static Src registration to an inventoried .ascx; one namespace/assembly registration to a scoped C# or VB source type; existing App_Code namespace candidate | Multiple competing registrations, duplicate control IDs, missing targets or ambiguous ownership refuse links. DLL-only controls, dynamic registration and runtime naming containers unavailable. | Tier2 structural, or existing Tier3 App_Code candidate. Page surface → exact markup control identity; construction/execution unproven. |
| Declarative property | `<%# controlId.Property %>` with optional whitespace; each identifier matches `[A-Za-z_][A-Za-z0-9_]*`; unique registered control and one directly declared semantic property with a public parameterless getter on a public containing-type chain in its exact containing type | VB names case-insensitive, C# case-sensitive. No inherited property resolution, chains, calls, indexers, Eval/Bind, render expressions, escaping or general expression evaluation. Any Template markup refuses instance binding; duplicate IDs, missing types and members produce gaps. A unique linked page type is required; known conflicting page/base members refuse receiver binding. Explicit receiver assembly tokens must match. Source base traversal is capped at 16 types; external metadata members are not inventoried. | Tier3 even with Tier1 declaration support. Control instance → exact property identity; binding evaluation/runtime receiver identity/getter invocation unproven. |
| Framework lifecycle | Direct compiler override of metadata-backed `System.Web.UI.Control`/`TemplateControl`/`UserControl`/`Page` member `CreateChildControls()`, or `OnInit`, `OnLoad`, `OnPreRender` with one by-value `System.EventArgs`; void instance methods; System.Web token `b03f5f7f11d50a3a` | Source intermediary overrides, other lifecycle members, same-name ordinary methods and unsigned lookalikes are excluded. Syntax-only scans cannot establish this family. Signed identity is not an authenticity claim. | Tier2 framework-entry candidate supported by Tier1 override and exact declaration. Framework member → source override; dispatch/construction/execution unproven. |

The initial ordinary-property positive fixture is VB. C# properties without a retained
semantic PropertyDeclared fact remain unavailable; this slice does not broaden
C# declaration extraction. C# framework override witnesses are separately tested.

The baseline already retained event/constructor/helper HTTP chains, explicit read-only
getter calls, override bodies, and registered-control composition. New containment
projects the selected composition evidence; it does not reclassify it as a call.
New binding extraction adds the missing exact control-instance/property link.
Compiler extraction adds the bounded framework witness; it does not create a call.
VB registration additionally needed VisualBasicProject inventory admission and
compiler global-qualifier normalization. Relative Src at the scan root needed
empty-directory normalization. Semantic registrations require the exact owning
project and assembly name; syntax project ties remain unresolved.

## Storage, graph and reports

`WebFormsEntryRelationship` is additive. Three `legacy.webforms.entry-*.v1` rules
retain source spans, supporting fact IDs, extractor versions, commit and scan identity,
exact generator SHA-256 and bounded-input SHA-256. No source snippets are retained.
The input digest covers the evidence span, relationship parameters and bounded
supporting facts. Those are local evidence hashes, not a shareable projection.

Scan Markdown has a separate entry section. Ordinary `paths` Markdown/JSON adds
`webFormsEntries` (`webforms-entry-inventory.v1`) with source-index identity,
original supporting fact IDs scoped by that source index, commit, rule, tier,
span and first unresolved boundary. Report projection hashes its exact serialized
rows and the Reporting assembly. Ordinary and bounded combined readers retain the
same inventory; existing fact/index schemas and call-path algorithm are unchanged.
The inventory covers retained input and is always labeled partial. Ordinary call-path
selectors do not filter it; bounded single-index StartingFactIds closures explicitly
label it selector-limited, including an empty retained inventory. Input-budget overflow still fails closed. Depth/work/path
truncation remains visible on call paths; terminal evidence never upgrades entries.

These relationships are deliberately absent from graph edge construction, root
selection and call traversal. Query an exact lifecycle/body method separately to
inspect its retained HTTP calls. A page's containment/property inventory alone
cannot establish that body's execution. No mixed route is synthesized.

The privacy-projected `legacy-flows` report omits this local inventory and its input
hashes. No new shareable artifact hashes private source input. Grouped handoff
continues to describe the existing call paths and does not admit these candidates.

Versions: legacy-webforms **0.14.2**, vb-semantic **0.10.6**, csharp-semantic
**0.22.4**. A fresh scan is required. Requery alone cannot reconstruct missing
instance/property/framework witnesses from earlier scans. Historical version
receipts remain historical; existing path classifications and algorithm are unchanged.

## Verification

`HttpEntryBoundaryRegressionTests` extends the real signed-System.Web sample with
Src containment, two instances, declarative-only references and unsupported binding
forms. Matching explicit page fields remain candidates; an inherited field of an unrelated type refuses the reference. Its mutation matrix covers ambiguous registrations, duplicate IDs, unrelated
control types, missing controls/members/types, wrong assemblies, calls, chains,
Eval and template scope, through SQLite/combine and both readers. Existing event,
constructor, helper, explicit getter, NameOf, lambda, ByRef, invalid write, virtual,
writable, MyBase/MyClass, inert and ordinary virtual-dispatch controls remain.
C# metadata-backed framework signatures are independently checked with net48 refs.

Run the focused HTTP/VB/WebForms, explicit-reference, grouped/path suites; relevant
Python diagnostics; full Debug .NET suite and public CLI scan on frozen binaries.
See the spec-local validation receipt for actual commands/outcomes and explicit
platform/corpus deferrals. Public realism checks are documented separately in
[the pinned corpus readback](WEBFORMS_HTTP_ENTRY_PUBLIC_CORPUS.md).

Review corrections: target .ascx directive names use the target language's case rules.
Entry expressions consume the same in-memory markup snapshot as registration parsing.
Scan Markdown includes entry gaps, encodes repository text and states its 50-row cap.
Containment report TargetSymbolId preserves the resolved target type identity.
New generator hashes use exact assembly bytes, or exact containing application bundle
bytes for a single-file entry assembly; they never substitute the dotnet host.
This does not certify other scanner components for single-file deployment.
