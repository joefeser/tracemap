# Claude: validate bounded request-method extraction

Validate the new source-semantic WebRequest method evidence against the previously
accepted H3 path. This is a fresh-extraction check, not permission to infer POST
from an old UNKNOWN fact. Read the entire prompt first.

## Bounds and privacy

Budget: 30 minutes, one TraceMap CLI build, one fresh scan using the exact existing
approved source scope and semantic context, and at most one shared H2/H3 HTTP
requery. Preserve all accepted runs, indexes, receipts and notes. Use new private
output locations outside Git and outside retained inputs. No application execution,
outbound HTTP, database connection, SQL execution, dependency installation, source
edits, Git changes, widened scan scope, traversal-budget increases or repeated scans.
Do not run the synthetic request methods. Keep source, symbols, selectors, paths,
URLs, payloads, hashes and business identifiers private. Return only sanitized results.

## 1. Establish current bytes and approved inputs

Use the operator-prepared checkout of this branch (or its merged descendant).
Record live full HEAD and dirty state. Stop on build-affecting modifications; do
not pull or switch branches. Confirm the bounded method helper and semantic
extractor versions csharp-semantic/0.22.6 and vb-semantic/0.10.9 (or documented
successors) are present. Build once:

```text
dotnet build src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj -c Debug --nologo -warnaserror
```

Nonzero exit is BUILD BLOCKED. Never substitute an old DLL. Record the actual CLI
and Reporting DLL hashes before and after validation and ensure HEAD is unchanged.
Locate the accepted local inventory and verify baseline hashes without overwriting
anything. Recover the exact prior successful scan invocation, source commit,
scope, references and semantic context from local records. Do not invent flags or
select the newest folder by name. If that invocation/context is missing, changed,
or would exceed these bounds, stop FRESH_EXTRACTION_REQUIRED with the smallest
operator action. Do not reuse an old index and call its UNKNOWN a regression.

## 2. Fresh extraction and fact-level check

Run the recovered approved scan once with the newly built CLI, changing only the
output destination. Use committed native workflow/tooling only. If producing the
required new run/bundle would require an additional scan or an unrecorded combine
workflow, stop and report the exact missing step instead of improvising it.

Inspect the expected H3 HTTP terminal and its exact source identity in the new
facts/index. Compare privately with the old fact. A qualifying shape is a fresh
same-block local from signed-framework WebRequest.Create/CreateHttp, one prior
unconditional uppercase standard constant Method assignment, and no alias,
escape, reassignment, capture, loop/jump or unsupported use. Ordinary trusted
ContentType/Headers/ContentLength preparation and GetRequestStream are supported.
This is deliberately not general constant propagation. A syntactically present
POST assignment alone does not establish eligibility.

This revision tolerates invalid operations only in isolated expression/declaration
statements that cannot mention the request local. Unrelated unresolved logging or
serialization need not block an otherwise bound request chain. Invalid request
uses, invalid control-flow conditions, missing factory/property/terminal binding,
and unsupported shapes still refuse inference. Do not demand zero diagnostics for
the entire application; preserve every compiler gap and the reduced coverage label.
If UNKNOWN remains, distinguish request-related invalid binding from unsupported
control flow or unrelated isolated errors. A gap category alone does not prove
which operation caused refusal. Inspect locally, return only the sanitized category.

For an eligible shape require httpMethod=POST, httpMethodEvidence=
same-block-local-property-assignment, accurate httpMethodAssignmentStartLine and
httpMethodAssignmentEndLine, the existing http.client.invocation.v1 rule,
Tier1Semantic, terminal location and new extractor version. If UNKNOWN remains,
report the precise unsupported shape or missing semantic input; do not force it
to POST, lower safeguards, or run more scans. No source snippets in the return.

## 3. Native report and provenance

Only if a valid fresh run/bundle and exactly resolved shared event-entry selector
exist, requery once using their new identities and the previously recorded bounds:

```text
dotnet <built-cli.dll> webforms-review requery-handler --run <new-verified-run> --bundle <new-verified-bundle> --source-root <new-private-selector.json> --out <new-private-report> --surface http-client --max-depth <recorded-depth>
```

Never copy an old selector identity blindly into a fresh index. Verify H2 and H3
terminals independently, and that the H3 verb survives index-to-handoff-to-HTML
projection. Event binding stays a separate anchor, not a traversed call edge.
Run the committed provenance verifier with installed Python 3.11+:

```text
<python> scripts/verify-http-report-provenance.py --receipt <new-private-report>/handler-requery.local.json --cli-dll <built-cli.dll>
```

Require exit 0 and CLI_AND_REPORTING_BYTES_MATCH; HANDOFF_LINK_MATCH. Missing
tooling or failed verification means NOT VERIFIED, not permission to install a
helper. Preserve truncation and gap reasons. Do not rerun H1/H4 or SQL baselines;
verify their accepted artifacts remain byte-identical. H4 framework dispatch stays
unproven. Source-level POST evidence is not proof of a sent request or runtime behavior.

## Return

Write a new private local note. Return public tool revision; build result; extractor
versions; fresh extraction completed yes/no; baseline unchanged yes/no; old/new H3
verb; assignment provenance verified yes/no; index/handoff/HTML agreement; binary
verification; truncation/gaps; elapsed time; and the smallest remaining blocker.
Report assignment evidence separately from binary provenance: assignment evidence
requires the new fact's method marker and accurate assignment line fields checked
against source. CLI_AND_REPORTING_BYTES_MATCH and HANDOFF_LINK_MATCH alone do not
verify an assignment. If UNKNOWN persists without those fields, report assignment
evidence NOT ESTABLISHED even when binary verification succeeds.
Verdict: BOUNDED METHOD EVIDENCE VERIFIED / UNSUPPORTED SHAPE REMAINS UNKNOWN /
FRESH_EXTRACTION_REQUIRED / BUILD BLOCKED / PROVENANCE NOT VERIFIED / TIME LIMIT /
REGRESSION SUSPECTED. Distinguish extraction from projection failures.

SQL-wrapper behavior, procedure contracts, DDL, authorization, application build
and migration readiness remain separate work. This check establishes none of them.
