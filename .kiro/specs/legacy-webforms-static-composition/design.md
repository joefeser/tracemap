# Design: Legacy Web Forms static composition

## Decision

Extend `LegacyWebFormsExtractor` rather than introduce another repository pass. Markup and code-behind are already read once into a bounded Web Forms context, and the new evidence depends on the same surface and control identities.

## Evidence contracts

Four focused rules own four candidate fact families:

| Rule | Fact | Maximum tier | Meaning |
|---|---|---:|---|
| `legacy.webforms.lifecycle-context.v1` | `WebFormsLifecycleBranchCandidate` | Tier3 | A supported lifecycle method contains a statically recognized postback condition. |
| `legacy.webforms.client-script.v1` | `WebFormsClientScriptRegistrationCandidate` | Tier3 | Checked-in code contains a supported client-script registration shape with a literal payload/target represented by hash. |
| `legacy.webforms.postback-target.v1` | `WebFormsPostBackTargetCandidate` | Tier3 | Checked-in markup or a supported literal script contains a literal `__doPostBack` target represented by hash. |
| `legacy.webforms.data-binding.v1` | `WebFormsDataBindingCandidate` | Tier2/Tier3 | Markup declares a same-surface `DataSourceID` relation or a literal `Eval`/`Bind` field expression. |

All identities are derived from existing normalized surface/control identities plus declaration sites. Candidate payloads use hashes; raw script, postback target, and binding field text are omitted.

## Parsing boundaries

- Lifecycle context supports logical negation of `IsPostBack` (`!IsPostBack` or `!this.IsPostBack`) inside `Page_Init`, `Page_Load`, or `Page_PreRender`. Other conditions containing `IsPostBack` are gaps.
- Client-script methods are a closed representative set: `RegisterStartupScript`, `RegisterClientScriptBlock`, `RegisterClientScriptInclude`, `RegisterOnSubmitStatement`, and `RegisterHiddenField`. At least one literal string argument is required; the final literal string is hashed as the registered payload/target.
- `__doPostBack` parsing accepts only a literal first argument. Exact same-surface control matching is optional supporting evidence, never a dispatch claim.
- `DataSourceID` accepts a safe static control identifier and resolves only within the same markup surface.
- `Eval`/`Bind` accepts a literal first argument. A bounded enclosing server control is chosen only when one parsed opening/closing tag interval contains the expression; otherwise the page remains the source and a scope gap is emitted.

## Failure behavior

Unreadable inputs keep existing behavior. Unsupported or ambiguous shapes emit `AnalysisGap` under their owning rule. The extractor never guesses from a display label or emits a clean-absence conclusion.

## Downstream behavior

The human-readable report includes the new facts. Existing modernization packet, path, handler, `.ashx`, redirect/transfer, and event-flow contracts remain unchanged in this slice.

## Limitations

Static syntax cannot prove page rendering, control-tree construction, lifecycle order, branch feasibility, postback dispatch, browser execution, successful data binding, returned data, or runtime reachability. Dynamic strings, generated markup, reflection, JavaScript construction, indirect helpers, data-source configuration, and runtime control creation remain gaps or outside coverage.

