# Bounded retained HTTP follow-up: H2 / H3 / H4

Work-machine-only investigation using the existing accepted private inventory and
receipt-verified combined index. H1 is already accepted; preserve its report,
receipt, correction note and database baselines byte-for-byte. This task does not
revalidate H1, establish runtime execution, or require database schema evidence.

## Bounds and identity

Budget: 20 minutes, at most one CLI build and three HTTP requeries total. Stop at
the limit and report partial progress. No scan, combine, preflight, dependency
installation, application execution, HTTP request, database access, source edit,
Git mutation or new diagnostic helper. Read-only inspection of retained SQLite
facts with existing approved local tools is allowed. Do not widen recorded query
limits. Keep every private selector, report and note outside Git and outside the
original retained run; never upload or commit private artifacts.

Use the operator-supplied revision on codex/http-evidence-regressions. Read branch,
full HEAD and dirty state live; do not reuse a prior agent message's SHA. Preserve
unpushed commits and unrelated files. Stop on tracked build-affecting changes.
Record HEAD before/after building and querying. Build the CLI once if needed with
`dotnet build src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj --nologo -c Debug`.
Hash the actual invoked tracemap.dll before/after queries and compare each receipt's
GeneratorSha256. Distinguish Git identity from binary identity. Stop if either
changes during the run. Verify the combined-index hash against its accepted receipt.

## Questions to answer

Use the existing private notes to identify the groups; do not guess by names:

- H2: profile-fetch outbound operation. Prior evidence identified constructor and
  control-method callers, but not a complete page-entry chain.
- H3: token-fetch operation called through the H2 method. Verify the retained
  connection separately; do not assume H2 reachability proves H3 reachability.
- H4: bidding-window-details outbound operation. Prior evidence stopped at a
  control/framework lifecycle boundary rather than a verified page-entry chain.

These are historical leads, not current verdicts. Verify all joins in this index.
Keep source call evidence, markup containment, declarative binding and framework
lifecycle candidate evidence distinct. Never invent a CallEdge from containment,
construction, override presence or a method-name match.

## Procedure

1. Inventory retained operation facts and exact caller identities for each group.
   Record rule, tier, location, supporting fact IDs, coverage gaps and identity
   tuple locally. Identify the nearest supported root and separately any claimed
   page/event/lifecycle entry. Prefer a verified entry root when it exists.
2. Find exactly one MethodDeclared for each proposed source-root selector. Copy
   sourceIndexId, scanId, commitSha and symbolId from its retained declaration and
   index_sources record into a new private JSON file. If the root is a constructor
   or otherwise lacks a supported unique MethodDeclared, report SELECTOR UNSUPPORTED
   or ROOT NOT FOUND; do not substitute a helper without labeling the weaker scope.
3. Use at most one query per group with this command shape:

   ```text
   dotnet <built-tracemap.dll> webforms-review requery-handler --run <retained-run> --bundle <verified-bundle> --source-root <private-selector.json> --out <new-private-output> --surface http-client --max-depth <recorded-depth>
   ```

   The bundle supplies verified context, not the root roster. Other limits remain
   those of the accepted configuration. Record the exact selected root scope.
4. Validate receipt root, HTTP scope, index/generator hashes, native bounded-input
   hash and limits. Inspect actual grouped variants: require the expected operation
   identity, ordered hops, rules, tiers, locations and supporting facts. A successful
   exit, receipt, unrelated HTTP terminal or positive count alone is not acceptance.
5. Separately verify entry anchoring. A path from a helper/control method to HTTP
   is useful but does not establish a page-entry path. Event bindings can be entry
   anchors without appearing as traversed call edges. Preserve weaker tiers,
   provenance gaps and truncation; absence in a bounded report is not proof of
   absence in the index or application.
6. For the first missing connection, distinguish: missing retained extraction;
   facts present but missing graph admission/join; unsupported root selector;
   framework/declarative relationship not modeled; report projection issue;
   bounded enumeration; or unknown. Ground the classification in retained facts,
   not a proposed code fix. Do not modify TraceMap on the work machine.

## Return

Write a new private note beside new outputs. Return a manually sanitized table:
group; root scope (entry/constructor/control/helper); expected terminal present;
entry anchor established; retained path present; strongest/weakest tier;
chains/variants; truncation; first missing connection; gap category; next action.
Use verdicts ENTRY-TO-HTTP RETAINED / METHOD-TO-HTTP ONLY / ROOT NOT FOUND /
SELECTOR UNSUPPORTED / PATH NOT ESTABLISHED / CONTEXT REJECTED / TIME LIMIT.
Include public tool revision, build result, binary verification yes/no and elapsed
time. Do not call all HTTP work complete based on one group or method-only paths.

For each unresolved distinct pattern, propose one minimal SYNTHETIC example using
invented types, symbols and example.invalid URLs: language, structural pattern,
expected facts/relations, expected tier and a negative control. Describe structure
only; do not copy or mechanically rename private source. No private names, paths,
hashes, source, tokens, URLs or payloads in returned material. Synthetic proposals
are hypotheses for public tests, not new scanner evidence.
