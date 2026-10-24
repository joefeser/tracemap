# HTTP entry boundaries

Independent public synthetic VB/.NET Framework 4.8 fixture using real System.Web
reference assemblies. Compile and statically scan only; never host it or execute
HTTP requests. Addresses are reserved example.invalid placeholders.
The test project references this compile-only harness, so a clean test build
restores its framework reference assets on Linux as well as Windows. A warmed
developer checkout or installed Windows targeting pack is not a prerequisite.

Expected source routes: button Handles binding → constructor → HTTP;
explicit property read → read-only getter → constructor → HTTP; lifecycle override
body → helper → HTTP. Page Load has no source call to the control event or override.
Markup registration/containment/data binding are separate structural/textual
evidence, never fabricated source CallEdges. The inert override does not reach
HTTP. The same-name ordinary method and unrelated ToString override deliberately
reach HTTP without lifecycle candidates. Framework dispatch timing and runtime
use are unproven.

This fixture is not a copied application, generated designer replacement,
complete WebForms hosting model, or proof that missing private property callers
are markup expressions. See the evidence-admission debugging ledger.

Issue #873 adds `Profile.ascx` and `EntryVariants.aspx`: two source-registered
instances reference the same exact property, while missing receivers/members,
Eval and chained expressions remain gaps. The regression mutation matrix adds
duplicate IDs, conflicting registrations, wrong assemblies and template scope.
See `docs/validation/WEBFORMS_HTTP_ENTRY_RELATIONSHIPS.md` for the supported grammar.

`MatchingHost` retains a reference with an explicit field of the registered type.
`ShadowedHost` inherits an unrelated same-name field; that binding produces a
conflicting-receiver gap instead of a property relationship.

Write-only, private and private-getter properties in EntryVariants remain gaps.
Review regressions also cover mixed target-language casing, explicit receiver
assembly tokens, retained markup snapshots, bounded selected-input labels and
Markdown gap/encoding/truncation behavior.

H4 regression coverage reuses LifecycleWidget rather than adding another app.
The HTTP-bearing ordinary CreateChildControls and unrelated ToString override
are negative lifecycle controls, not negative body-call controls. The projectless
`unresolved-framework` test deliberately omits System.Web references: compiler
diagnostics/reduced coverage remain explicit while independently resolved helper
and HTTP calls survive. It must emit no framework lifecycle candidate.

Faithful semantic lifecycle coverage requires the restored
Microsoft.NETFramework.ReferenceAssemblies.net48 1.0.3 assets (mscorlib.dll,
System.dll, System.Web.dll under build/.NETFramework/v4.8), including System.Web's
b03f5f7f11d50a3a public key token. Modern .NET alone does not supply System.Web.
A source harness or unresolved base is not a substitute for this metadata witness.
No SQL coverage, runtime dispatch, control construction, lifecycle execution,
request execution or private-application parity is established.
