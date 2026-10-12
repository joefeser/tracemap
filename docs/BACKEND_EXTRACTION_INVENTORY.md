# Backend extraction inventory — first testable slice

Tracking: [#861](https://github.com/joefeser/tracemap/issues/861).
Goal: move human-selected database and outbound service functionality into a
.NET 10 API. This first slice does not generate or deploy API code.

## Try it

Build this branch's CLI and tests:

```sh
dotnet build src/dotnet/TraceMap.sln -warnaserror
dotnet test src/dotnet/TraceMap.sln --no-build --filter 'FullyQualifiedName~BackendExtractionSampleTests|FullyQualifiedName~GroupedCompiledPathHandoffTests|FullyQualifiedName~ConstructedGenericSampleTests'
```

Generate a **new** native Web Forms grouped report or retained-handler requery
using this tool build and your existing admitted inputs. Preserve the original
run/checkpoints and pinned tool. Do not replace a receipted generator in place
or edit old artifacts to add fields. Standard report/updated-reader admission
still applies; report any refusal rather than bypassing identity checks.

Open `compiled-paths.local.html`: the new **Backend extraction inventory** section
lists backend evidence retained by that query. Native Web Forms producers use a
`database-api` filter: their inventory is database-filtered, **not HTTP coverage**;
HTTP-only handlers can be excluded upstream. Incidental HTTP records do not
establish an HTTP inventory. The companion
`compiled-paths.handoff.local.json` contains `backendExtraction` and its rule,
scope, querySurface, coverage, truncation, retained-gap count and operation records.

Each record references the full node evidence, zero-based ordered variant
indexes, query-root node references and candidate-variant indexes. The rule/tier,
source/scan/commit, source or compiled location, command-binding fields and
supporting evidence remain in the existing node/edge/path records. Different
evidence records are not collapsed merely because labels match.

Candidate detection preserves path classification, candidate edges, ambiguous
candidate counts and reduced evidence tiers. Operations with both retained and
candidate variants use `mixed-retained-and-candidate-query-paths`; inspect the
candidate indexes rather than treating every associated variant as retained.

`found-not-connected-in-supplied-query` is not "unused". Query roots are not
automatically verified events. Rows do not count unique procedures or business
operations. Human decisions remain `undecided`; do not edit an admitted handoff
to approve a migration. A separate owner-decision receipt is future work.

Only database-api, sql-query and http-client surface records already retained
by the query are included. This is **not the independent full-index inventory**.
No new readable SQL, procedure definition, destination secret or HTTP payload is
captured. Command hashes are not SQL text. Unknown evidence remains unknown.

New constructed-generic edges include `limitations` in ordinary path JSON,
grouped JSON and method-graph JSON. Grouped HTML, method-graph HTML and displayed
Markdown paths explain that generic definitions are not specialized downstream.
Existing receipts and legacy handoffs remain unchanged/readable.

## Migration planning

Use `prompts/plan-backend-api-extraction.md` with explicitly authorized private
evidence. It is a new planning prompt; existing Claude diagnostic launchers are
not automatically changed to use it. It separates extracted facts, missing
contracts, human retain/retire decisions and proposed API operations. Automatic
launcher integration and implementation authority remain follow-on tasks.

## Synthetic inputs and validation

`samples/backend-extraction` is a build-only .NET 10 library: shared procedure
call, unconnected query, literal/dynamic HTTP targets and a text-only decoy.
Native scanner/report tests consume it without calling any method. Existing
compiler-built constructed-generic fixtures cover positive/negative resolution.

The first-slice discovery spike found no HTTP surface for `HttpClient.GetStringAsync`
in a semantic+IL scan. The positive HTTP fixture uses supported `GetAsync`.
Convenience API coverage and unsupported-family accounting are recorded in #861;
the missing surface is not a statement that the application makes no HTTP call.

Full-index discovery, private opt-in command/contract capture, entry-point
binding, owner-decision receipts and end-to-end API migration acceptance remain
open. No language extractor was changed by this slice; authentic Windows/private
site acceptance and the full repository suite are separate validation gates.


## Outbound HTTP follow-up

The separate `codex/outbound-http-coverage-861` branch depends on inventory commit
`d55eb44b3d4781d2adeffa11a6d42a8ca90e7ea5`, whose base dev commit is
`0d3107cc4e7edb9fca20928f43e6604a4ff2b992`. It closes bounded source HTTP gaps;
see [the mode matrix](../samples/outbound-http-coverage/README.md) and
[every net10.0 overload](../samples/outbound-http-coverage/OVERLOADS.md).
The earlier convenience-method gap is resolved on this follow-up branch.

Migration consumers must distinguish signed semantic calls from name-only C#
candidates and compiled IL observations. VB projectless input and compiled-only
input do not establish HTTP operation classification. Request variables, mutable
WebRequest verbs, wrapper specialization and dynamic destinations require supplied
contracts. WebClient addresses may use other protocols. Content-only JSON reads
are not outbound evidence. Missing or unsupported input and query filtering can
omit operations; absence never means unused, and static evidence never proves
runtime execution. The containing report still supplies generator/bounded-input
hashes, retained references and privacy projection. Full-index discovery, private
contract capture and owner decisions remain separate tasks.


New grouped JSON includes optional `httpCoverageLimitations`; retained HTTP nodes
include optional `httpCallEvidence` with API family/name, destination availability,
protocol and coverage labels. Verbs, callsite locations, rule/tier and retained
fact references remain in the node record. These additions capture no raw URL,
body or new private hashes. Existing nullable fields are omitted when absent;
legacy handoffs may omit the new coverage field. Review original retained facts
for resolved overload signatures. See the spec implementation state for validation
at the exact implementation commit and the independently advanced dependency.
