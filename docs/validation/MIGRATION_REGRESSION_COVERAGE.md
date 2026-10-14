# SQL and migration repair regression coverage

## Combined WebForms pattern matrix

`AppCodeRegistrationMatrixTests` contains 89 generated synthetic cases, not just
one regression per screenshot. The checked-in `samples/webforms-pattern-matrix`
combines namespace registration, a VB App_Code class, With event wiring, Handles,
Page.IsPostBack, and literal/dynamic postback targets. No application code runs.

| Matrix | Dimensions | Cases |
| --- | --- | ---: |
| Registration/type/scope | C#/VB x config/directive x root/child/foreign-root x unique/missing/duplicate/wrong-namespace/outside-App_Code/case-decoy | 72 |
| Refusal boundaries | clear, remove, unrelated location, malformed assembly, malformed Src, missing facts, uninventoried source, interface | 8 |
| Cross-feature sample | local/New/invocation With receiver x Page/Boolean/shadowed lifecycle condition, with registration and static/dynamic postbacks always present | 9 |

Every registration matrix case checks deterministic evidence order. Positive
joins require source type provenance and Tier3 evidence; refusal cases must not
invent assembly joins. The existing source/assembly registration tests, event
identity tests and stacked SQL/HTTP migration corpus remain separate gates.
The Windows gap-family CI runs this matrix on both source and sample changes.

Coverage is explicitly incomplete: arbitrary configuration inheritance,
configSource, external App_Code roots, dynamic assembly loading, partial-type
merging, generic/control ancestry and projectless compiler contexts are not proved.
New supported shapes must add matrix dimensions or interaction cases, not only
isolated happy-path tests. Private acceptance remains one separate scan.

PR target: `dev`. This ledger maps implemented behavior to executable public
synthetic tests. It is not a claim that every possible VB construct is supported,
nor a substitute for passing exact-head PR checks. No private source or artifacts
are required. Existing negative tests must remain in place when refactoring.

| Implemented behavior / invariant | Executable acceptance | Negative or boundary control |
| --- | --- | --- |
| Database API families across C#, VB, source and IL | SqlDatabaseMatrixTests; sql-coverage-smoke.py; samples/sql-database-matrix | Independent named oracle, provider/type shadows, evidence-tier checks, command-state reset |
| Existing C# and compiled traversal fixtures honor trusted API identities | CSharpSemanticExtractorTests; CompiledBaselinePrewalkTests; MessyWorkspaceRegressionTests | Source-declared provider lookalikes are rejected; positive command fixtures inherit signed framework DbCommand and throw if executed; compiled positives carry assembly identity |
| Command state and dynamic values | DatabaseCommandStateTests | Reassignment/uncertain state never retains stale command evidence |
| Projectless SQL visibility and reports | ProjectlessSqlReportingTests | Reduced coverage retained, no clean-absence claim |
| Explicit Parameters.Add and parameter constructors | ProjectlessParameterCandidateTests | Dynamic/named arguments, unsupported types, ordinary object creations, alias cases |
| Nested Try, command constructor arguments, intervening configuration | ProjectlessParameterCandidateTests; MigrationRegressionCorpusTests | Cross-block/reassignment/other receiver rejected; strong execution remains unknown with complex effects |
| Relative SqlClient declaration resolution | Nested_relative_command_uses_same_resolver_for_add_and_declaration | Aliases/shadows and unsupported type spellings refused |
| Value witnesses and later lexical execution | Value_observations_preserve_repeats_and_exclude_other_or_dynamic_indexes; Lexical_execution_does_not_cross_reassignment_branch_or_other_receiver | Dynamic indexes and other receiver assignments excluded |
| String.Empty, replacement, literal concatenation | Empty_seed_and_replacement_have_distinct_trace_semantics; Command_text_trace_is_bounded_constant_syntax_not_raw_sql | Nonconstant state and expression bounds; no raw SQL in normal report |
| Integrated SQL/HTTP/markup inventory | IntegratedMigrationReportTests; samples/migration-vertical-slice | Missing/ambiguous ownership, unconnected groups, parameter grouping, budgets and deterministic order |
| Handles anchors independent of framework ancestry | Handles_helper_http_chain_preserves_syntax_tier_and_refuses_ambiguous_anchors; MigrationRegressionCorpusTests | External/desktop bases never become verified WebForms routes; duplicate owners/types rejected |
| Operation-directed caller search and diagnostics | IntegratedMigrationReportTests | Unrelated fan-out, depth, cycles, rejected-call leads never promoted to edges |
| Protected nested receiver lookup | MigrationRegressionCorpusTests; NestedReceiver.vb | Global/sibling decoys, duplicate declarations/overloads, alias/generic shadows, missing receiver type |
| Inline New receiver and optional argument tail | InlineReceiverMigrationTests | Required/named omissions, ParamArray expansion, ambiguous overloads, old metadata |
| Cross-file explicit property/field chain | PropertyChainMigrationTests | Shared/instance distinction, missing types, duplicate/indexed/array members, local shadows, factories; typed local and Me positives |
| Complete stacked correction, four operations/five parameters | StackedMigrationRegressionTests; samples/stacked-migration-regression | Remove each critical layer: property, inline/nested call, optional metadata, event anchor; required paths disappear |
| Source/compiled wrapper provenance and SQL handoff | SqlWrapperRouteTests; GroupedCompiledPathHandoffTests | No invented source/binary identity bridge; original signatures and context retained |
| Database evidence admission | SqlReviewRegressionTests; GroupedCompiledPathHandoffTests | Canonical allowlisted metadata; 8,192-character/128-item bounds before tokenization; rehashed path and inventory nodes still require rule, tier, commit, source and span provenance |
| Retained SQL inventory and large input admission | test_sql_wrapper_report.py; sql-wrapper-large-index-smoke.py | Hash/identity changes, explicit size bounds, timeout/failure, truncation, deterministic output |
| Opt-in local SQL literal and contract recovery | test_sql_command_text.py | Consent, wrong binary/hash, dynamic state, malformed catalog, ambiguous names, HTML inertness, no overwrite |
| HTTP regressions | OutboundHttpLegacyCoverageTests; http-coverage-smoke.py | Independent HTTP oracle, legacy profiles, syntax fallback and coverage gaps |
| Cached VB type lookup | VisualBasicSyntaxTypeLookupTests | Cached/uncached equivalence and bounded scaling |

## CI enforcement

`scripts/validation/test_retained_gap_inventory.py` covers the offline retained
gap summary: closed-label privacy projection, generator/projected-input hashes,
public-extractor-backed reason labels, gapKind/classification compatibility,
separate unresolved-call totals, deterministic grouping, malformed/duplicate JSON,
byte/record/time limits, linked inputs, unsafe output locations and no-overwrite.
The dedicated retained-gap-inventory workflow runs these synthetic tests on
Windows and Linux without building or scanning an application.

`WebFormsGapFamilyRegressionTests` adds a 15-case synthetic matrix for escaped
VB Handles/AddHandler/RemoveHandler receivers, Boolean-literal IsPostBack
comparisons in both operand orders, negation, parentheses, shadowing, depth
limits, scoped configuration registration/clear, and literal/dynamic postback
targets. Dynamic/chained event receivers and non-Boolean/compound lifecycle
conditions remain gaps. Registration and postback cases protect existing
behavior; they are not new support claims. The Windows gap-family workflow also
runs the stacked migration and integrated report regressions. Use the committed
`prompts/validate-webforms-gap-family-batch.md` for one private scan; synthetic
passes cannot establish that private gap shapes match these bounded additions.

Local batch validation (2026-10-14): 102 tests passed, zero failed/skipped across
the new matrix, VisualBasicWebFormsComposition, LegacyWebFormsExtractor,
LegacyWebFormsEventIdentity, MigrationRegressionCorpus and IntegratedMigrationReport
classes. A public stacked-migration-regression CLI smoke retained 156 facts,
four of four candidate operation paths and zero ungrouped parameter facts.
Full-solution, broader pinned language-adapter matrices, Windows execution and
private acceptance are deferred/separate gates, not implied by this focused pass.

The Windows test-host baseline comparison separately runs `VaultExportTests` and
`WebFormsReviewInputValidationTests` on the pinned pre-hardening base and candidate.
Candidate regression guards cover canonical LF Markdown hashes (with CRLF readback),
rejection of absolute-path redaction markers as navigation paths, and read-only
Git fixture cleanup. The historical base is an intentionally failing control;
its failure does not count as a candidate pass or waive full-suite validation.

`.github/workflows/sql-database-compatibility.yml` runs the matrix, parameter,
report, wrapper, integrated, inline, property and stacked tests on Windows, plus
the SQL/HTTP deterministic CLI matrices and Python diagnostic guards. Both push
and PR path filters include every `samples/*migration*/` corpus, so a sample-only
edit cannot silently skip this lane. The full .NET solution suite is a separate
validation signal; filtered tests alone do not certify the whole repository.

The accepted private baseline is four **observed** operation groups with candidate
paths, not complete application coverage. Runtime routes, framework ancestry,
execution effects, schema contracts and remaining analysis gaps stay outside that
acceptance. Test-only changes do not require repeatedly rescanning private inputs.

Database handoff admission accepts positive ordered source spans or the paired
`-1/-1` unavailable-span sentinel used by compiled evidence. It requires a nonempty
rule, source index, scan and file plus a known evidence tier and full hexadecimal
Git commit identity. This validates evidence shape, not source/binary correspondence
or runtime execution. Optional null metadata remains backward compatible; persisted
direction/type-kind lists must be canonical, while raw producer lists are bounded
before normalization. Invalid producer metadata becomes unavailable, never a claim.
