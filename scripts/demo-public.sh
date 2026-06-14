#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ASSERT_HELPER="$ROOT_DIR/scripts/demo-public-assert.mjs"
DOTNET_CLI="$ROOT_DIR/src/dotnet/TraceMap.Cli"
TS_DIR="$ROOT_DIR/src/typescript"
INCLUDE_PYTHON=0
REQUIRE_JVM=0
OUT_ARG=""
TARGET_ENDPOINT="GET /api/admin/runner/get-by-id/{}"
TARGET_PATH_KEY="/api/admin/runner/get-by-id/{}"
SAMPLE_ROOTS=(
  "$ROOT_DIR/samples/modern-sample"
  "$ROOT_DIR/samples/endpoint-server-aspnet"
  "$ROOT_DIR/samples/typescript-modern-sample"
  "$ROOT_DIR/samples/endpoint-client-angular"
  "$ROOT_DIR/samples/public-demo/before"
  "$ROOT_DIR/samples/public-demo/after"
)

usage() {
  cat <<'EOF'
Usage:
  ./scripts/demo-public.sh [out_dir] [--include-python] [--require-jvm]

Runs the default public TraceMap demo over checked-in sample repositories.
Generated artifacts are kept for inspection under out_dir or a temporary directory.
EOF
}

for arg in "$@"; do
  case "$arg" in
    --help|-h)
      usage
      exit 0
      ;;
    --include-python)
      INCLUDE_PYTHON=1
      ;;
    --require-jvm)
      REQUIRE_JVM=1
      ;;
    --*)
      echo "error: unknown option $arg" >&2
      usage >&2
      exit 1
      ;;
    *)
      if [[ -n "$OUT_ARG" ]]; then
        echo "error: only one output directory may be provided." >&2
        exit 1
      fi
      OUT_ARG="$arg"
      ;;
  esac
done

require_cmd() {
  local name="$1"
  if ! command -v "$name" >/dev/null 2>&1; then
    echo "error: missing required command: $name" >&2
    if command -v brew >/dev/null 2>&1; then
      case "$name" in
        dotnet) echo "hint: try 'brew install --cask dotnet-sdk' or install the .NET SDK from Microsoft." >&2 ;;
        node|npm) echo "hint: try 'brew install node' or use the project's Node version manager." >&2 ;;
        git) echo "hint: try 'brew install git'." >&2 ;;
        python3) echo "hint: try 'brew install python'." >&2 ;;
        java) echo "hint: try 'brew install openjdk@21' and set JAVA_HOME to the Java 21 home." >&2 ;;
      esac
    fi
    exit 1
  fi
}

abs_path() {
  node -e 'const path = require("node:path"); console.log(path.resolve(process.argv[1]));' "$1"
}

is_inside_repo() {
  local candidate="$1"
  [[ "$candidate" == "$ROOT_DIR" || "$candidate" == "$ROOT_DIR/"* ]]
}

is_inside_path() {
  local candidate="$1"
  local parent="$2"
  [[ "$candidate" == "$parent" || "$candidate" == "$parent/"* ]]
}

reject_sample_output_root() {
  local sample_root
  for sample_root in "${SAMPLE_ROOTS[@]}"; do
    if is_inside_path "$OUT_ROOT" "$sample_root"; then
      cat >&2 <<EOF
error: output directory cannot be inside a scanned sample root.

Use an ignored directory outside samples, such as:
  $ROOT_DIR/.tracemap-demo/
EOF
      exit 1
    fi
  done
}

add_section() {
  local name="$1"
  local status="$2"
  local classification="$3"
  local coverage="$4"
  local reason="$5"
  local artifacts="$6"
  local counts="$7"
  node "$ASSERT_HELPER" append-section "$SECTIONS_JSONL" "$name" "$status" "$classification" "$coverage" "$reason" "$artifacts" "$counts"
}

run_dotnet_scan() {
  local repo_path="$1"
  local out_path="$2"
  local label="$3"
  dotnet run --no-build --project "$DOTNET_CLI" -- scan --repo "$repo_path" --out "$out_path"
  node "$ASSERT_HELPER" scan-artifacts "$label" "$out_path"
}

run_ts_scan() {
  local repo_path="$1"
  local out_path="$2"
  local label="$3"
  node "$TS_CLI" scan --repo "$repo_path" --out "$out_path"
  node "$ASSERT_HELPER" scan-artifacts "$label" "$out_path"
}

require_cmd git
require_cmd node
require_cmd npm
require_cmd dotnet

# Node is required before output path normalization because abs_path uses Node's
# cross-platform path resolver.
if [[ -z "$OUT_ARG" ]]; then
  OUT_ROOT="$(mktemp -d)"
else
  OUT_ROOT="$(abs_path "$OUT_ARG")"
  if is_inside_repo "$OUT_ROOT"; then
    if [[ "$OUT_ROOT" == "$ROOT_DIR" ]]; then
      echo "error: output directory cannot be the repository root." >&2
      exit 1
    fi
    OUT_RELATIVE="${OUT_ROOT#$ROOT_DIR/}"
    if ! git -C "$ROOT_DIR" check-ignore -q -- "$OUT_RELATIVE" \
      && ! git -C "$ROOT_DIR" check-ignore -q -- "$OUT_RELATIVE/"; then
      cat >&2 <<EOF
error: output directory is inside the repository but is not ignored by git.

Use an ignored directory such as:
  $ROOT_DIR/.tracemap-demo/
EOF
      exit 1
    fi
  fi
fi

reject_sample_output_root
mkdir -p "$OUT_ROOT"
SCANS_DIR="$OUT_ROOT/scans"
REPORTS_DIR="$OUT_ROOT/reports"
COMBINED_DIR="$OUT_ROOT/combined"
mkdir -p "$SCANS_DIR" "$REPORTS_DIR" "$COMBINED_DIR"
SECTIONS_JSONL="$OUT_ROOT/.demo-sections.jsonl"
: > "$SECTIONS_JSONL"

echo "TraceMap public demo"
echo "Mode: default checked-in samples"
echo "Output root: $OUT_ROOT"
echo "Samples: dotnet-modern, dotnet-endpoint-server, typescript-modern, typescript-endpoint-client, public-demo-before, public-demo-after"
echo

echo "== Toolchain checks =="
dotnet --version >/dev/null
node --version >/dev/null
npm --version >/dev/null
git --version >/dev/null
add_section "toolchains" "available" "NoActionableEvidence" "FullEvidenceAvailable" "" "" '{"requiredTools":4}'

if [[ "$INCLUDE_PYTHON" == "1" ]]; then
  add_section "python" "deferred" "PartialAnalysis" "deferred" "Python scanning was requested, but Python sample scanning is a follow-up slice for this public demo implementation." "" '{"requested":1}'
else
  add_section "python" "not_requested" "NoActionableEvidence" "not_requested" "" "" '{"requested":0}'
fi

if command -v java >/dev/null 2>&1 && java -version 2>&1 | grep -q 'version "21'; then
  add_section "jvm" "deferred" "PartialAnalysis" "deferred" "Java 21 is available, but JVM sample scanning is a follow-up slice for this public demo implementation." "" '{"java21Available":1}'
elif [[ "$REQUIRE_JVM" == "1" ]]; then
  echo "error: --require-jvm was supplied but Java 21 was not found." >&2
  if command -v brew >/dev/null 2>&1; then
    echo "hint: try 'brew install openjdk@21' and set JAVA_HOME to the Java 21 home." >&2
  fi
  exit 1
else
  add_section "jvm" "unavailable" "PartialAnalysis" "unavailable" "Java 21 was not detected; JVM demo scan is optional in this slice." "" '{"java21Available":0}'
fi

echo "== Build TraceMap CLIs =="
dotnet build "$ROOT_DIR/src/dotnet/TraceMap.sln"
if [[ ! -d "$TS_DIR/node_modules" ]]; then
  npm --prefix "$TS_DIR" install
fi
npm --prefix "$TS_DIR" run build

if [[ -f "$TS_DIR/dist/src/cli.js" ]]; then
  TS_CLI="$TS_DIR/dist/src/cli.js"
elif [[ -f "$TS_DIR/dist/cli.js" ]]; then
  TS_CLI="$TS_DIR/dist/cli.js"
else
  echo "error: TypeScript CLI build did not produce dist/src/cli.js or dist/cli.js." >&2
  exit 1
fi
add_section "build" "available" "NoActionableEvidence" "FullEvidenceAvailable" "" "" '{"dotnet":1,"typescript":1}'

echo "== Scan checked-in samples =="
DOTNET_MODERN="$SCANS_DIR/dotnet-modern"
DOTNET_ENDPOINT="$SCANS_DIR/dotnet-endpoint-server"
TS_MODERN="$SCANS_DIR/typescript-modern"
TS_ENDPOINT="$SCANS_DIR/typescript-endpoint-client"
PUBLIC_DEMO_BEFORE="$SCANS_DIR/public-demo-before"
PUBLIC_DEMO_AFTER="$SCANS_DIR/public-demo-after"

run_dotnet_scan "$ROOT_DIR/samples/modern-sample" "$DOTNET_MODERN" "dotnet-modern"
run_dotnet_scan "$ROOT_DIR/samples/endpoint-server-aspnet" "$DOTNET_ENDPOINT" "dotnet-endpoint-server"
run_ts_scan "$ROOT_DIR/samples/typescript-modern-sample" "$TS_MODERN" "typescript-modern"
run_ts_scan "$ROOT_DIR/samples/endpoint-client-angular" "$TS_ENDPOINT" "typescript-endpoint-client"
run_dotnet_scan "$ROOT_DIR/samples/public-demo/before" "$PUBLIC_DEMO_BEFORE" "public-demo-before"
run_dotnet_scan "$ROOT_DIR/samples/public-demo/after" "$PUBLIC_DEMO_AFTER" "public-demo-after"

SCAN_COUNTS="$(
  node "$ASSERT_HELPER" scan-summary \
    "dotnet-modern=$DOTNET_MODERN" \
    "dotnet-endpoint-server=$DOTNET_ENDPOINT" \
    "typescript-modern=$TS_MODERN" \
    "typescript-endpoint-client=$TS_ENDPOINT" \
    "public-demo-before=$PUBLIC_DEMO_BEFORE" \
    "public-demo-after=$PUBLIC_DEMO_AFTER"
)"
SCAN_REDUCED_COUNT="$(node -e 'const counts = JSON.parse(process.argv[1]); console.log(counts.reducedCoverageScans ?? 0);' "$SCAN_COUNTS")"
SCAN_COVERAGE="FullEvidenceAvailable"
SCAN_CLASSIFICATION="ActionableStaticEvidence"
if [[ "$SCAN_REDUCED_COUNT" != "0" ]]; then
  SCAN_COVERAGE="PartialAnalysis"
  SCAN_CLASSIFICATION="PartialAnalysis"
fi

add_section "sample-scans" "available" "$SCAN_CLASSIFICATION" "$SCAN_COVERAGE" "" \
  "scans/dotnet-modern/report.md,scans/dotnet-endpoint-server/report.md,scans/typescript-modern/report.md,scans/typescript-endpoint-client/report.md,scans/public-demo-before/report.md,scans/public-demo-after/report.md" \
  "$SCAN_COUNTS"

echo "== Combine indexes and report dependency evidence =="
ENDPOINT_COMBINED="$COMBINED_DIR/endpoint-stack.sqlite"
MIXED_COMBINED="$COMBINED_DIR/mixed-stack.sqlite"
ENDPOINT_REPORT="$REPORTS_DIR/dependency/endpoint-stack"
MIXED_REPORT="$REPORTS_DIR/dependency/mixed-stack"

dotnet run --no-build --project "$DOTNET_CLI" -- combine \
  --index "$TS_ENDPOINT/index.sqlite" --label public-ts-client \
  --index "$DOTNET_ENDPOINT/index.sqlite" --label public-dotnet-server \
  --out "$ENDPOINT_COMBINED"

dotnet run --no-build --project "$DOTNET_CLI" -- combine \
  --index "$DOTNET_MODERN/index.sqlite" --label public-dotnet-modern \
  --index "$DOTNET_ENDPOINT/index.sqlite" --label public-dotnet-server \
  --index "$TS_MODERN/index.sqlite" --label public-ts-modern \
  --index "$TS_ENDPOINT/index.sqlite" --label public-ts-client \
  --out "$MIXED_COMBINED"

test -f "$ENDPOINT_COMBINED"
test -f "$MIXED_COMBINED"

dotnet run --no-build --project "$DOTNET_CLI" -- report --index "$ENDPOINT_COMBINED" --out "$ENDPOINT_REPORT"
dotnet run --no-build --project "$DOTNET_CLI" -- report --index "$MIXED_COMBINED" --out "$MIXED_REPORT"

ENDPOINT_REPORT_COUNTS="$(
  node "$ASSERT_HELPER" dependency-report "$ENDPOINT_REPORT" \
    "public-ts-client,public-dotnet-server" \
    "$TARGET_PATH_KEY"
)"
MIXED_REPORT_COUNTS="$(
  node "$ASSERT_HELPER" dependency-report "$MIXED_REPORT" \
    "public-dotnet-modern,public-dotnet-server,public-ts-modern,public-ts-client" \
    "$TARGET_PATH_KEY"
)"
DEPENDENCY_REPORT_COUNTS="$(
  node -e '
    const endpoint = JSON.parse(process.argv[1]);
    const mixed = JSON.parse(process.argv[2]);
    console.log(JSON.stringify({
      endpointStackSources: endpoint.sources ?? 0,
      mixedStackSources: mixed.sources ?? 0,
      sources: (endpoint.sources ?? 0) + (mixed.sources ?? 0),
      endpointStackEndpointFindings: endpoint.endpointFindings ?? 0,
      mixedStackEndpointFindings: mixed.endpointFindings ?? 0,
      endpointFindings: (endpoint.endpointFindings ?? 0) + (mixed.endpointFindings ?? 0),
      endpointStackDependencySurfaces: endpoint.dependencySurfaces ?? 0,
      mixedStackDependencySurfaces: mixed.dependencySurfaces ?? 0,
      dependencySurfaces: (endpoint.dependencySurfaces ?? 0) + (mixed.dependencySurfaces ?? 0),
      endpointStackDependencyEdges: endpoint.dependencyEdges ?? 0,
      mixedStackDependencyEdges: mixed.dependencyEdges ?? 0,
      dependencyEdges: (endpoint.dependencyEdges ?? 0) + (mixed.dependencyEdges ?? 0),
      endpointStackGaps: endpoint.gaps ?? 0,
      mixedStackGaps: mixed.gaps ?? 0,
      gaps: (endpoint.gaps ?? 0) + (mixed.gaps ?? 0)
    }));
  ' "$ENDPOINT_REPORT_COUNTS" "$MIXED_REPORT_COUNTS"
)"

add_section "combine-and-dependency-report" "available" "PartialAnalysis" "PartialAnalysis" "" \
  "reports/dependency/endpoint-stack/dependency-report.md,reports/dependency/endpoint-stack/dependency-report.json,reports/dependency/mixed-stack/dependency-report.md,reports/dependency/mixed-stack/dependency-report.json" \
  "$DEPENDENCY_REPORT_COUNTS"

echo "== Run paths and reverse queries =="
PATHS_REPORT="$REPORTS_DIR/paths/endpoint-to-sql"
PATHS_REPORT_SECOND="$REPORTS_DIR/paths/endpoint-to-sql-second"
REVERSE_REPORT="$REPORTS_DIR/reverse/sql-to-endpoints"
REVERSE_REPORT_SECOND="$REPORTS_DIR/reverse/sql-to-endpoints-second"

dotnet run --no-build --project "$DOTNET_CLI" -- paths \
  --index "$ENDPOINT_COMBINED" \
  --from-endpoint "$TARGET_ENDPOINT" \
  --to-surface sql-query \
  --out "$PATHS_REPORT"
dotnet run --no-build --project "$DOTNET_CLI" -- paths \
  --index "$ENDPOINT_COMBINED" \
  --from-endpoint "$TARGET_ENDPOINT" \
  --to-surface sql-query \
  --out "$PATHS_REPORT_SECOND"
cmp -s "$PATHS_REPORT/paths-report.json" "$PATHS_REPORT_SECOND/paths-report.json"

PATHS_COUNTS="$(
  node "$ASSERT_HELPER" paths-report "$PATHS_REPORT" "public-ts-client,public-dotnet-server"
)"

dotnet run --no-build --project "$DOTNET_CLI" -- reverse \
  --index "$ENDPOINT_COMBINED" \
  --surface sql-query \
  --to endpoints \
  --out "$REVERSE_REPORT"
dotnet run --no-build --project "$DOTNET_CLI" -- reverse \
  --index "$ENDPOINT_COMBINED" \
  --surface sql-query \
  --to endpoints \
  --out "$REVERSE_REPORT_SECOND"
cmp -s "$REVERSE_REPORT/reverse-report.json" "$REVERSE_REPORT_SECOND/reverse-report.json"

REVERSE_COUNTS="$(
  node "$ASSERT_HELPER" reverse-report "$REVERSE_REPORT"
)"
PATHS_REVERSE_COUNTS="$(
  node -e '
    const paths = JSON.parse(process.argv[1]);
    const reverse = JSON.parse(process.argv[2]);
    console.log(JSON.stringify({
      paths: paths.paths ?? 0,
      pathGaps: paths.gaps ?? 0,
      reversePaths: reverse.paths ?? 0,
      reverseRoots: reverse.reverseRoots ?? 0,
      reverseGaps: reverse.gaps ?? 0,
      selectedSurfaces: reverse.selectedSurfaces ?? 0
    }));
  ' "$PATHS_COUNTS" "$REVERSE_COUNTS"
)"
add_section "paths-and-reverse" "available" "PartialAnalysis" "PartialAnalysis" "" \
  "reports/paths/endpoint-to-sql/paths-report.md,reports/paths/endpoint-to-sql/paths-report.json,reports/reverse/sql-to-endpoints/reverse-report.md,reports/reverse/sql-to-endpoints/reverse-report.json" \
  "$PATHS_REVERSE_COUNTS"

echo "== Generate portfolio manifest and report =="
PORTFOLIO_MANIFEST="$OUT_ROOT/portfolio-manifest.json"
PORTFOLIO_REPORT="$REPORTS_DIR/portfolio"
node "$ASSERT_HELPER" portfolio-manifest "$OUT_ROOT" "$PORTFOLIO_MANIFEST" \
  "endpoint-stack=combined/endpoint-stack.sqlite" \
  "mixed-stack=combined/mixed-stack.sqlite"

dotnet run --no-build --project "$DOTNET_CLI" -- portfolio \
  --manifest "$PORTFOLIO_MANIFEST" \
  --out "$PORTFOLIO_REPORT"

PORTFOLIO_COUNTS="$(
  node "$ASSERT_HELPER" portfolio-report "$PORTFOLIO_REPORT" "endpoint-stack,mixed-stack"
)"
add_section "portfolio" "available" "PartialAnalysis" "PartialAnalysis" "" \
  "portfolio-manifest.json,reports/portfolio/portfolio-report.md,reports/portfolio/portfolio-report.json" \
  "$PORTFOLIO_COUNTS"

echo "== Run before/after diff, impact, and release review =="
PUBLIC_DEMO_BEFORE_COMBINED="$COMBINED_DIR/public-demo-before.sqlite"
PUBLIC_DEMO_AFTER_COMBINED="$COMBINED_DIR/public-demo-after.sqlite"
DIFF_REPORT="$REPORTS_DIR/diff/public-demo"
IMPACT_REPORT="$REPORTS_DIR/impact/public-demo"
RELEASE_REVIEW_REPORT="$REPORTS_DIR/release-review/public-demo"

dotnet run --no-build --project "$DOTNET_CLI" -- combine \
  --index "$PUBLIC_DEMO_BEFORE/index.sqlite" --label public-demo-api \
  --out "$PUBLIC_DEMO_BEFORE_COMBINED"

dotnet run --no-build --project "$DOTNET_CLI" -- combine \
  --index "$PUBLIC_DEMO_AFTER/index.sqlite" --label public-demo-api \
  --out "$PUBLIC_DEMO_AFTER_COMBINED"

test -f "$PUBLIC_DEMO_BEFORE_COMBINED"
test -f "$PUBLIC_DEMO_AFTER_COMBINED"

dotnet run --no-build --project "$DOTNET_CLI" -- diff \
  --before "$PUBLIC_DEMO_BEFORE_COMBINED" \
  --after "$PUBLIC_DEMO_AFTER_COMBINED" \
  --out "$DIFF_REPORT" \
  --scope surfaces
dotnet run --no-build --project "$DOTNET_CLI" -- diff \
  --before "$PUBLIC_DEMO_BEFORE_COMBINED" \
  --after "$PUBLIC_DEMO_AFTER_COMBINED" \
  --out "$DIFF_REPORT-second" \
  --scope surfaces
cmp -s "$DIFF_REPORT/diff-report.json" "$DIFF_REPORT-second/diff-report.json"

DIFF_COUNTS="$(
  node "$ASSERT_HELPER" diff-report "$DIFF_REPORT" "public-demo-api"
)"
add_section "diff" "available" "PartialAnalysis" "PartialAnalysis" "" \
  "reports/diff/public-demo/diff-report.md,reports/diff/public-demo/diff-report.json" \
  "$DIFF_COUNTS"

dotnet run --no-build --project "$DOTNET_CLI" -- impact \
  --before "$PUBLIC_DEMO_BEFORE_COMBINED" \
  --after "$PUBLIC_DEMO_AFTER_COMBINED" \
  --out "$IMPACT_REPORT" \
  --scope surfaces
dotnet run --no-build --project "$DOTNET_CLI" -- impact \
  --before "$PUBLIC_DEMO_BEFORE_COMBINED" \
  --after "$PUBLIC_DEMO_AFTER_COMBINED" \
  --out "$IMPACT_REPORT-second" \
  --scope surfaces
cmp -s "$IMPACT_REPORT/impact-report.json" "$IMPACT_REPORT-second/impact-report.json"

IMPACT_COUNTS="$(
  node "$ASSERT_HELPER" impact-report "$IMPACT_REPORT" "public-demo-api"
)"
add_section "impact" "available" "PartialAnalysis" "PartialAnalysis" "" \
  "reports/impact/public-demo/impact-report.md,reports/impact/public-demo/impact-report.json" \
  "$IMPACT_COUNTS"

dotnet run --no-build --project "$DOTNET_CLI" -- release-review \
  --before "$PUBLIC_DEMO_BEFORE_COMBINED" \
  --after "$PUBLIC_DEMO_AFTER_COMBINED" \
  --out "$RELEASE_REVIEW_REPORT" \
  --scope surfaces \
  --max-findings 200 \
  --max-surface-rows 200 \
  --max-checklist-items 200 \
  --max-paths 5
dotnet run --no-build --project "$DOTNET_CLI" -- release-review \
  --before "$PUBLIC_DEMO_BEFORE_COMBINED" \
  --after "$PUBLIC_DEMO_AFTER_COMBINED" \
  --out "$RELEASE_REVIEW_REPORT-second" \
  --scope surfaces \
  --max-findings 200 \
  --max-surface-rows 200 \
  --max-checklist-items 200 \
  --max-paths 5
cmp -s "$RELEASE_REVIEW_REPORT/release-review.json" "$RELEASE_REVIEW_REPORT-second/release-review.json"

RELEASE_REVIEW_COUNTS="$(
  node "$ASSERT_HELPER" release-review "$RELEASE_REVIEW_REPORT" "public-demo-api"
)"
add_section "release-review" "available" "PartialAnalysis" "PartialAnalysis" "" \
  "reports/release-review/public-demo/release-review.md,reports/release-review/public-demo/release-review.json" \
  "$RELEASE_REVIEW_COUNTS"

node "$ASSERT_HELPER" write-summary "$OUT_ROOT" "$SECTIONS_JSONL" "$OUT_ROOT/demo-summary.json" "$OUT_ROOT/demo-summary.md"
node "$ASSERT_HELPER" validate-summary "$OUT_ROOT/demo-summary.json"
node "$ASSERT_HELPER" sentinel-scan "$OUT_ROOT"

echo
echo "TraceMap public demo complete"
echo "Output root: $OUT_ROOT"
echo "Scanned sources: 6"
echo "Combined sources: 8"
node -e '
  const fs = require("node:fs");
  const summary = JSON.parse(fs.readFileSync(process.argv[1], "utf8"));
  const section = name => summary.sections.find(item => item.name === name)?.counts ?? {};
  const dependency = section("combine-and-dependency-report");
  const paths = section("paths-and-reverse");
  const portfolio = section("portfolio");
  const diff = section("diff");
  const impact = section("impact");
  const releaseReview = section("release-review");
  console.log(`Endpoint findings: ${dependency.endpointFindings ?? 0}`);
  console.log(`Paths: ${paths.paths ?? 0}`);
  console.log(`Reverse results: ${paths.reversePaths ?? 0}`);
  console.log(`Portfolio sources: ${portfolio.portfolioSources ?? 0}`);
  console.log(`Diff rows: ${diff.diffRows ?? 0}`);
  console.log(`Impact items: ${impact.impactItems ?? 0}`);
  console.log(`Release review findings: ${releaseReview.findings ?? 0}`);
' "$OUT_ROOT/demo-summary.json"
echo "Report coverage: see demo-summary.json"
echo "Gaps: see demo-summary.json"
