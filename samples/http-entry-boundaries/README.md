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
evidence, never fabricated source CallEdges. Inert override and same-name ordinary
method do not reach HTTP. Framework dispatch timing and runtime use are unproven.

This fixture is not a copied application, generated designer replacement,
complete WebForms hosting model, or proof that missing private property callers
are markup expressions. See the evidence-admission debugging ledger.

Issue #873 adds `Profile.ascx` and `EntryVariants.aspx`: two source-registered
instances reference the same exact property, while missing receivers/members,
Eval and chained expressions remain gaps. The regression mutation matrix adds
duplicate IDs, conflicting registrations, wrong assemblies and template scope.
See `docs/validation/WEBFORMS_HTTP_ENTRY_RELATIONSHIPS.md` for the supported grammar.
