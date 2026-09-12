# Design

The VB semantic extractor owns compiler-resolved event relationships. It emits
language-level event-binding and event-raise facts with canonical VB symbol
identity. Unresolved event sites emit bounded category-only gaps; they are never
upgraded by name alone.

The VB syntax fallback emits the same fact families at Tier3 with local names
and hashes only. It runs only for files without semantic coverage, preserving
the existing no-duplicate boundary and per-file budget.

The shared legacy Web Forms extractor gains a VB parser alongside its C# parser.
It projects VB `Handles` and `AddHandler` subscriptions into the existing
`WebFormsEventBindingDeclared` and `WebFormsHandlerResolved` contracts when a
linked markup surface, one control/lifecycle receiver, and one handler method
are established. `RemoveHandler` is retained as detach evidence but not treated
as a runtime binding. Markup `OnX` attributes continue through the existing
language-neutral path. VB lifecycle/postback syntax is supported conservatively.

Consumers remain unchanged because they already read shared Web Forms facts.
Rule catalog and validation documentation state all static-analysis bounds.
