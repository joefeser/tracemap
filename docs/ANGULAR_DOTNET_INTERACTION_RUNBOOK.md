# Angular and .NET Interaction Mapping Runbook

This runbook explains how to scan authorized Angular and .NET repositories,
combine their static evidence, and investigate questions such as:

> Which Angular control or event handler can lead to which HTTP call, ASP.NET
> endpoint, backend code, and downstream dependency evidence?

TraceMap produces deterministic static evidence. A reported chain is not a
runtime trace and does not prove that a control is visible, a user clicked it,
an endpoint received traffic, dependency injection selected a particular
implementation, or a database operation executed.

## What you can learn

Depending on the source patterns and scan coverage, the workflow can expose:

- Angular template bindings, form controls, and statically named event
  handlers;
- TypeScript component, service, call, object-shape, and `HttpClient` evidence;
- normalized client HTTP methods and route templates;
- matching, client-only, server-only, method-mismatched, optional, and dynamic
  endpoint evidence;
- ASP.NET routes, controllers, handlers, services, repositories, and static
  call relationships;
- SQL/query, persistence, package/config, HTTP, WCF, ASMX, remoting, message,
  and other supported dependency surfaces;
- rule IDs, evidence tiers, repository labels, commit SHAs, file spans,
  extractor versions, supporting evidence IDs, coverage, and gaps.

A well-supported result can resemble:

```text
Angular event binding
  -> component handler
  -> client service method
  -> Angular HttpClient call
  -> normalized HTTP endpoint match
  -> ASP.NET route/handler
  -> backend static call path
  -> downstream dependency surface
```

Dynamic templates, computed URLs, runtime routing, reflection, runtime-only
dependency injection, generated code that is not checked in, and incomplete
project loading can interrupt that chain. TraceMap should report the missing
link or reduced coverage instead of inventing it.

## Requirements

- A current TraceMap checkout.
- .NET SDK 10.
- Node.js 20 and npm.
- `jq` when using the Bash workflow.
- PowerShell 7.3 or later when using the Windows workflow.
- Local, authorized checkouts of every repository being reviewed.
- A resolvable Git commit for each source repository.
- Enough local disk space for one scan directory per repository plus reports.

Use clean, committed source checkouts. Record each source HEAD before scanning;
a commit SHA does not describe uncommitted edits, and the current scan manifest
does not carry a content-bound dirty-tree identity.

The commands below keep every generated artifact beneath a new timestamped
output directory. Do not reuse an old output path.

## Build TraceMap once

Run the later commands in the same shell session. The first block enables
fail-fast native-command handling; the repository/output setup block repeats
that safety initialization in case the already-built tools are being reused.

### Windows PowerShell

```powershell
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true
$TraceMap = "C:\work\tracemap"

dotnet build (Join-Path $TraceMap "src\dotnet\TraceMap.sln")

Push-Location (Join-Path $TraceMap "src\typescript")
npm ci
npm run build
Pop-Location
```

### Bash

```bash
set -euo pipefail

TRACEMAP=/work/tracemap

dotnet build "$TRACEMAP/src/dotnet/TraceMap.sln"
npm --prefix "$TRACEMAP/src/typescript" ci
npm --prefix "$TRACEMAP/src/typescript" run build
```

## Choose repositories and output paths

This example uses one Angular client and two .NET repositories. Remove the
worker entries if they do not apply. Add additional repositories by repeating
the scan and `--index`/`--label` pairs.

Use stable, non-secret labels. Labels become the source names in combined
reports and selectors.

### Windows PowerShell

```powershell
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true
$TraceMap = "C:\work\tracemap"
$AngularRepo = "C:\work\application-ui"
$AngularProject = Join-Path $AngularRepo "tsconfig.json"
$ApiRepo = "C:\work\application-api"
$ApiSolution = Join-Path $ApiRepo "Application.Api.sln"
$WorkerRepo = "C:\work\application-worker"
$WorkerSolution = Join-Path $WorkerRepo "Application.Worker.sln"

$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$OutputRoot = "C:\work\tracemap-output\full-stack-$Stamp"
$AngularOut = Join-Path $OutputRoot "angular-client"
$ApiOut = Join-Path $OutputRoot "dotnet-api"
$WorkerOut = Join-Path $OutputRoot "dotnet-worker"
$CombinedIndex = Join-Path $OutputRoot "combined.sqlite"
$DotNetCli = Join-Path $TraceMap "src\dotnet\TraceMap.Cli"
$TypeScriptCli = Join-Path $TraceMap "src\typescript\dist\src\cli.js"

if (Test-Path $OutputRoot) {
    throw "Output root already exists: $OutputRoot"
}
New-Item -ItemType Directory -Path $OutputRoot -ErrorAction Stop | Out-Null
```

### Bash

```bash
set -euo pipefail

TRACEMAP=/work/tracemap
ANGULAR_REPO=/work/application-ui
ANGULAR_PROJECT="$ANGULAR_REPO/tsconfig.json"
API_REPO=/work/application-api
API_SOLUTION="$API_REPO/Application.Api.sln"
WORKER_REPO=/work/application-worker
WORKER_SOLUTION="$WORKER_REPO/Application.Worker.sln"

STAMP=$(date +%Y%m%d-%H%M%S)
OUTPUT_ROOT="/work/tracemap-output/full-stack-$STAMP"
ANGULAR_OUT="$OUTPUT_ROOT/angular-client"
API_OUT="$OUTPUT_ROOT/dotnet-api"
WORKER_OUT="$OUTPUT_ROOT/dotnet-worker"
COMBINED_INDEX="$OUTPUT_ROOT/combined.sqlite"
DOTNET_CLI="$TRACEMAP/src/dotnet/TraceMap.Cli"
TYPESCRIPT_CLI="$TRACEMAP/src/typescript/dist/src/cli.js"

mkdir -p "$(dirname "$OUTPUT_ROOT")"
if ! mkdir "$OUTPUT_ROOT"; then
  printf 'Output root already exists or could not be created: %s\n' \
    "$OUTPUT_ROOT" >&2
  exit 1
fi
```

## Record the exact source state

### Windows PowerShell

```powershell
$Repositories = $AngularRepo, $ApiRepo, $WorkerRepo
foreach ($Repository in $Repositories) {
    "REPOSITORY: $Repository"
    git -C $Repository rev-parse --show-toplevel
    git -C $Repository rev-parse HEAD
    $WorkingTreeStatus = @(git -C $Repository status --short)
    if ($WorkingTreeStatus.Count -gt 0) {
        throw "Source checkout must be clean before scanning: $Repository"
    }
}
```

### Bash

```bash
for REPOSITORY in "$ANGULAR_REPO" "$API_REPO" "$WORKER_REPO"; do
  printf 'REPOSITORY: %s\n' "$REPOSITORY"
  git -C "$REPOSITORY" rev-parse --show-toplevel
  git -C "$REPOSITORY" rev-parse HEAD
  WORKING_TREE_STATUS="$(git -C "$REPOSITORY" status --short)"
  if test -n "$WORKING_TREE_STATUS"; then
    printf 'Source checkout must be clean before scanning: %s\n' \
      "$REPOSITORY" >&2
    exit 1
  fi
done
```

Commit or stash working-tree changes before scanning. If the review must cover
uncommitted bytes, create a separate content-addressed source snapshot and scan
that snapshot; do not attribute dirty source content to the checkout's HEAD.

## Scan the Angular repository

The TypeScript scanner uses the configured `tsconfig.json` when supplied and
falls back to lower-tier evidence when semantic analysis is incomplete.

### Windows PowerShell

```powershell
node $TypeScriptCli scan `
  --repo $AngularRepo `
  --project $AngularProject `
  --out $AngularOut
```

### Bash

```bash
node "$TYPESCRIPT_CLI" scan \
  --repo "$ANGULAR_REPO" \
  --project "$ANGULAR_PROJECT" \
  --out "$ANGULAR_OUT"
```

If the repository contains multiple TypeScript projects, repeat `--project`
for each relevant `tsconfig.json`. Do not use `--no-semantic` unless you
intentionally want syntax-only coverage.

## Scan each .NET repository

Repeat this pattern for every API, shared service, worker, integration, or
batch repository that can participate in the application.

### Windows PowerShell

```powershell
dotnet run --project $DotNetCli -- scan `
  --repo $ApiRepo `
  --solution $ApiSolution `
  --out $ApiOut

dotnet run --project $DotNetCli -- scan `
  --repo $WorkerRepo `
  --solution $WorkerSolution `
  --out $WorkerOut
```

### Bash

```bash
dotnet run --project "$DOTNET_CLI" -- scan \
  --repo "$API_REPO" \
  --solution "$API_SOLUTION" \
  --out "$API_OUT"

dotnet run --project "$DOTNET_CLI" -- scan \
  --repo "$WORKER_REPO" \
  --solution "$WORKER_SOLUTION" \
  --out "$WORKER_OUT"
```

Add `--restore` only when an authorized restore is required and network/package
policy permits it. A failed or partial project load should retain provable
syntax/structural evidence and label the reduced coverage.

## Inspect scan health before combining

Every successful scan directory should contain:

```text
scan-manifest.json
facts.ndjson
index.sqlite
report.md
logs/analyzer.log
```

The .NET scanner may also emit `scan-receipt.json` with sanitized stage and
failure diagnostics.

### Windows PowerShell

```powershell
$ScanOutputs = $AngularOut, $ApiOut, $WorkerOut
foreach ($ScanOutput in $ScanOutputs) {
    "SCAN: $ScanOutput"
    $Manifest = Get-Content (Join-Path $ScanOutput "scan-manifest.json") -Raw |
      ConvertFrom-Json
    $Manifest |
      Select-Object repoName, commitSha, analysisLevel, buildStatus, scannerVersion |
      Format-List
    Get-Item (Join-Path $ScanOutput "index.sqlite") |
      Select-Object Name, Length
}
```

### Bash

```bash
for SCAN_OUTPUT in "$ANGULAR_OUT" "$API_OUT" "$WORKER_OUT"; do
  printf 'SCAN: %s\n' "$SCAN_OUTPUT"
  jq '{repoName, commitSha, analysisLevel, buildStatus, scannerVersion}' \
    "$SCAN_OUTPUT/scan-manifest.json"
  test -s "$SCAN_OUTPUT/index.sqlite"
done
```

Read each `report.md` and review every analysis gap. Do not interpret an empty
or reduced graph as proof that the application has no dependency.

## Generate a direct Angular-to-API endpoint report

This report is the fastest first check of client/server connectivity evidence.

### Windows PowerShell

```powershell
$EndpointOut = Join-Path $OutputRoot "endpoint-alignment"
dotnet run --project $DotNetCli -- endpoints `
  --client-index (Join-Path $AngularOut "index.sqlite") `
  --server-index (Join-Path $ApiOut "index.sqlite") `
  --client-label angular-client `
  --server-label dotnet-api `
  --out $EndpointOut
```

### Bash

```bash
ENDPOINT_OUT="$OUTPUT_ROOT/endpoint-alignment"
dotnet run --project "$DOTNET_CLI" -- endpoints \
  --client-index "$ANGULAR_OUT/index.sqlite" \
  --server-index "$API_OUT/index.sqlite" \
  --client-label angular-client \
  --server-label dotnet-api \
  --out "$ENDPOINT_OUT"
```

Inspect `endpoint-report.md` and `endpoint-report.json` for:

- matched method/path evidence;
- optional-segment matches;
- method mismatches;
- dynamic client URLs needing review;
- client calls without a static server match;
- server routes without a static client match;
- coverage warnings and limitations.

A client-only row is not proof of a broken request, and a server-only row is
not proof of dead code.

## Combine every repository index

Labels must remain stable across reruns if reports will be compared later.

### Windows PowerShell

```powershell
dotnet run --project $DotNetCli -- combine `
  --index (Join-Path $AngularOut "index.sqlite") --label angular-client `
  --index (Join-Path $ApiOut "index.sqlite") --label dotnet-api `
  --index (Join-Path $WorkerOut "index.sqlite") --label dotnet-worker `
  --out $CombinedIndex
```

### Bash

```bash
dotnet run --project "$DOTNET_CLI" -- combine \
  --index "$ANGULAR_OUT/index.sqlite" --label angular-client \
  --index "$API_OUT/index.sqlite" --label dotnet-api \
  --index "$WORKER_OUT/index.sqlite" --label dotnet-worker \
  --out "$COMBINED_INDEX"
```

## Generate the broad inventory reports

### Combined dependency report

```powershell
$DependencyOut = Join-Path $OutputRoot "dependency-report"
dotnet run --project $DotNetCli -- report `
  --index $CombinedIndex `
  --out $DependencyOut
```

```bash
DEPENDENCY_OUT="$OUTPUT_ROOT/dependency-report"
dotnet run --project "$DOTNET_CLI" -- report \
  --index "$COMBINED_INDEX" \
  --out "$DEPENDENCY_OUT"
```

This summarizes source coverage, endpoint alignment, dependency surfaces,
dependency edges, review rows, gaps, and static-analysis limitations.

### Multi-repository portfolio report

```powershell
$PortfolioOut = Join-Path $OutputRoot "portfolio"
dotnet run --project $DotNetCli -- portfolio `
  --index (Join-Path $AngularOut "index.sqlite") --label angular-client `
  --index (Join-Path $ApiOut "index.sqlite") --label dotnet-api `
  --index (Join-Path $WorkerOut "index.sqlite") --label dotnet-worker `
  --out $PortfolioOut
```

```bash
PORTFOLIO_OUT="$OUTPUT_ROOT/portfolio"
dotnet run --project "$DOTNET_CLI" -- portfolio \
  --index "$ANGULAR_OUT/index.sqlite" --label angular-client \
  --index "$API_OUT/index.sqlite" --label dotnet-api \
  --index "$WORKER_OUT/index.sqlite" --label dotnet-worker \
  --out "$PORTFOLIO_OUT"
```

This provides a bounded overview of sources, shared static surfaces, endpoints,
dependencies, gaps, and limitations without claiming a runtime topology.

## Investigate a button, control, field, or binding

Start with a statically visible Angular control name, form control, property
binding, or event handler. For a template such as `(click)="save()"`, a useful
first selector is commonly `binding:save`. For `formControlName="email"`, use
`control:email`.

Use `--source angular-client` to prevent same-name evidence in another source
from being selected silently.

### Windows PowerShell

```powershell
$PropertyFlowOut = Join-Path $OutputRoot "property-flow-save"
dotnet run --project $DotNetCli -- property-flow `
  --index $CombinedIndex `
  --property "binding:save" `
  --source angular-client `
  --framework angular `
  --out $PropertyFlowOut
```

### Bash

```bash
PROPERTY_FLOW_OUT="$OUTPUT_ROOT/property-flow-save"
dotnet run --project "$DOTNET_CLI" -- property-flow \
  --index "$COMBINED_INDEX" \
  --property 'binding:save' \
  --source angular-client \
  --framework angular \
  --out "$PROPERTY_FLOW_OUT"
```

The result may connect UI evidence to a payload property, client HTTP call,
matched server endpoint, and server model/DTO evidence. Generic names such as
`save`, `status`, or `name` can legitimately produce multiple candidates. Use
the selected root's exact `fact:` or `symbol:` identity for a narrower rerun:

```powershell
dotnet run --project $DotNetCli -- property-flow `
  --index $CombinedIndex `
  --property "fact:<combined-fact-id>" `
  --out (Join-Path $OutputRoot "property-flow-exact")
```

Do not choose an arbitrary candidate when the report emits `AmbiguousSelector`.

## Follow a matched endpoint into backend dependencies

Copy the normalized method/path key from the endpoint report. Preserve the
quotes because the selector contains a space.

### Route-centered report

```powershell
$RouteFlowOut = Join-Path $OutputRoot "route-flow-profile-save"
dotnet run --project $DotNetCli -- route-flow `
  --index $CombinedIndex `
  --route "POST /api/profile" `
  --out $RouteFlowOut
```

```bash
ROUTE_FLOW_OUT="$OUTPUT_ROOT/route-flow-profile-save"
dotnet run --project "$DOTNET_CLI" -- route-flow \
  --index "$COMBINED_INDEX" \
  --route 'POST /api/profile' \
  --out "$ROUTE_FLOW_OUT"
```

The route-flow report groups already-supported route, method, service,
repository, query, data, dependency, value-origin, and gap evidence.

### Path to one dependency family

```powershell
$SqlPathOut = Join-Path $OutputRoot "paths-profile-to-sql"
dotnet run --project $DotNetCli -- paths `
  --index $CombinedIndex `
  --from-endpoint "POST /api/profile" `
  --to-surface sql-query `
  --source-pair "angular-client:dotnet-api" `
  --out $SqlPathOut
```

```bash
SQL_PATH_OUT="$OUTPUT_ROOT/paths-profile-to-sql"
dotnet run --project "$DOTNET_CLI" -- paths \
  --index "$COMBINED_INDEX" \
  --from-endpoint 'POST /api/profile' \
  --to-surface sql-query \
  --source-pair 'angular-client:dotnet-api' \
  --out "$SQL_PATH_OUT"
```

Other useful `--to-surface` values include `sql-persistence`, `http-client`,
`message-queue`, `message-topic`, `wcf-operation`, `asmx-operation`, and
`dependency-surface`. Availability depends on the evidence producers present
in the scanned repositories.

## A practical discovery sequence

For each important screen or workflow:

1. Find a distinctive Angular event handler, control name, or bound property.
2. Run `property-flow` with the Angular source label.
3. Record the exact selected fact/symbol identity and every ambiguity or gap.
4. Find the normalized client/server endpoint in `endpoint-report.md`.
5. Run `route-flow` for that server route.
6. Run `paths` for the downstream surface families that matter.
7. Cite repository label, commit SHA, rule ID, evidence tier, and file span for
   every accepted link.
8. Mark unsupported links as `Unknown` or `Needs review`; do not fill them from
   naming similarity alone.

A useful working matrix is:

| Screen/control | Angular handler | Client call | Server endpoint | Backend evidence | Downstream surface | Coverage/gaps |
| --- | --- | --- | --- | --- | --- | --- |
| `<observed name>` | `<static identity>` | `<method/path>` | `<matched route>` | `<handler/path>` | `<surface>` | `<classification>` |

TraceMap currently emits the supporting reports rather than one dedicated
full-stack interaction-matrix artifact. Keep this matrix as a human-reviewed
projection; do not upgrade uncertain links beyond their source evidence.

## Troubleshooting and truthful interpretation

### No Angular event or control appears

- Confirm the correct `tsconfig.json` was supplied.
- Review TypeScript project diagnostics and `report.md`.
- Check whether the template is external, inline, generated, dynamic, or
  excluded.
- Treat missing evidence under reduced coverage as unknown, not absent.

### Client call does not match a server endpoint

- Review method mismatch, optional segment, and dynamic URL findings.
- Confirm the correct API repository and commit were scanned.
- Check for configuration-provided base paths, gateways, generated clients,
  proxies, or runtime route rewriting.
- Do not conclude that the call is broken from static mismatch evidence alone.

### The route exists but no backend path appears

- Review the .NET scan's build status and analysis gaps.
- Check for runtime DI, reflection, dynamic dispatch, generated code, or an
  unsupported integration framework.
- Look for `NoBackendEvidence`, `ReducedCoverage`, or `AnalysisGap` rows.
- Preserve partial route/handler evidence even when the downstream chain ends.

### A selector matches too many things

- Add `--source`.
- Use a more specific field/control/model/DTO selector.
- Rerun with the exact `fact:` or `symbol:` identity from the first report.
- Never select a same-name candidate merely because it looks plausible.

## Outputs to retain for a private review

Retain the following together so evidence does not lose its provenance:

- every source `scan-manifest.json` and `scan-receipt.json` when present;
- every source `index.sqlite` and `report.md`;
- the combined index;
- endpoint, dependency, portfolio, property-flow, route-flow, and path JSON;
- the corresponding Markdown projections;
- a small note recording the exact commands and non-default bounds used.

Generated reports can contain repository-relative paths, symbol/control names,
route templates, dependency identities, and architectural metadata. Treat the
bundle according to the source organization's handling rules.

## What the reports do not prove

The workflow does not prove:

- runtime rendering, clicks, submitted values, traffic, or reachability;
- branch feasibility or business intent;
- runtime dependency-injection target selection;
- authentication or authorization outcomes;
- SQL execution, database state, or effective permissions;
- deployment topology, ownership, health, or production use;
- that client-only or server-only evidence represents a defect;
- that missing evidence means a feature or dependency does not exist.

The useful claim is narrower: TraceMap found a bounded static evidence chain—or
an explicit gap—across exact repository snapshots.
