# Review focused Web Forms coverage gaps locally

```text
Perform one private, read-only coverage audit for the authorized legacy Web
Forms source and its latest completed TraceMap focused-review output. The goal
is to determine which static application shapes TraceMap already represents,
which shapes produced explicit gaps, and which locally observed shapes have no
known TraceMap projection yet.

This task runs only inside the private environment. Do not use network
services, upload files, paste source into another model, commit, push, open a
pull request, or modify either repository. Do not build, restore, execute the
application, launch a browser, or rescan unless separately authorized.

Inputs:

1. Confirm the TraceMap checkout is detached at the exact owner-approved
   TraceMap revision. Do not substitute an experimental branch or a newer
   revision without owner approval.
2. Ask the owner for the exact latest completed focused-review output directory
   if it is not already supplied. Do not search unrelated directories or drives.
3. Ask the owner for the exact authorized source root and the three in-scope
   relative folders if they are not already supplied. Inspect no other source
   folders.
4. Read only:
   - the in-scope source files with extensions `.aspx`, `.ascx`, `.master`,
     `.ashx`, `.asmx`, `.asax`, `.cs`, `.config`, `.cshtml`, and `.chtml`;
   - the completed output's `local-review-result.json`,
     `scan/scan-manifest.json`, and `scan/facts.ndjson`;
   - TraceMap's rule catalog and checked-in Web Forms/Razor extractor source
     solely to identify supported rule and fact shapes.
5. Do not read database contents, runtime logs, secrets, generated reports,
   SQLite indexes, package feeds, connection material, or application data.

Privacy requirements:

- Never output source paths, repository identity, commit SHA, object/type/member
  names, control IDs, handler names, route values, URLs, query-string keys,
  JavaScript function names, SQL, configuration values, source snippets, hashes
  derived from private values, or business terminology.
- Do not reproduce text rendered by a page or stored in markup.
- Keep all inspection results in memory. Do not persist a local report unless
  the owner separately authorizes a specific private output path.
- Before returning, search the proposed response for private paths, identifiers,
  source values, and snippets. If safe output cannot be produced, return only
  `coverage-audit=stopped;failureCode=SANITIZATION_UNAVAILABLE`.

Evidence classes must remain separate:

1. `projected` — a positive TraceMap fact exists under the applicable rule ID;
2. `explicit-gap` — TraceMap emitted a rule-backed `AnalysisGap`;
3. `locally-observed-unprojected-shape` — a bounded syntax shape is present in
   authorized source but neither a positive fact nor an applicable explicit gap
   was found;
4. `workspace-prerequisite` — stronger semantic evidence is unavailable because
   project/workspace loading is reduced;
5. `not-observed` — the bounded source-shape check found zero occurrences. This
   is not proof that the behavior does not exist.

Audit these shape families independently:

- artifact inventory: ASPX, ASCX, master pages, ASHX, ASMX, Global.asax,
  configuration, C# code-behind, designer C#, CSHTML, and literal CHTML;
- directives and code-behind/inheritance linkage;
- declarative `OnX` attributes and syntax-level handler candidates;
- lifecycle methods and `IsPostBack` branch context;
- `__doPostBack`, postback targets, and server/client click registrations;
- literal `RegisterStartupScript`, `ClientScript`, and startup-script calls;
- `DataSourceID`, `Eval`, `Bind`, data-source controls, and `DataBind` calls;
- master-page and user-control composition, including configuration-level
  control registrations;
- redirects, transfers, handler/module registrations, ASHX/ASMX surfaces, and
  navigation candidates;
- Session, ViewState, cookies, request/query/form values, and hidden fields;
- ADO.NET, datasets, table adapters, stored-procedure candidates, and inline or
  dynamic SQL boundaries;
- inline server blocks, dynamically constructed control/event names, reflection,
  custom markup extensions, and other shapes that cannot be resolved statically.

For `.cshtml`, report it as the Razor family and never relabel it Web Forms. For
literal `.chtml`, report only whether the extension exists and whether TraceMap
inventoried or projected it; do not assume what framework owns it.

Use fact type as well as rule ID. Analysis gaps never count as positive evidence.
Do not treat a syntax candidate as compiler-resolved evidence. Do not infer
runtime reachability, branch execution, rendered output, successful binding,
database execution, or application correctness.

Return no more than 80 lines in exactly this form:

coverage-audit=completed
runCoverage=<full|reduced|partial|unknown>
workspaceState=<available|reduced|unavailable|unknown>
artifactFamily01=<category>|inventory=<count>|projected=<count>|gaps=<count>|state=<evidence-class>
...
shapeFamily01=<category>|observed=<count>|projected=<count>|gaps=<count>|state=<evidence-class>|ruleId=<rule-id-or-none>
...
missingProjection01=<generic-shape-category>|observed=<count>|existingGap=<yes|no>|recommendedAction=<extend-rule|add-gap|restore-workspace|research-only>
...
topCoverageConstraint01=<generic-category>|count=<count>|recommendedAction=<bounded-action>
...
nonClaim=runtime-behavior-unproven

Sort all numbered rows by descending count and then category. Limit each section
to ten rows. Use only generic categories and checked-in rule IDs. If no missing
projection is found, emit
`missingProjection01=none-observed|observed=0|existingGap=no|recommendedAction=research-only`.
```
