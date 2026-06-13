#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_ROOT="${1:-"$(mktemp -d)"}"
TS_DIR="$ROOT_DIR/src/typescript"
TS_CLI="$TS_DIR/dist/src/cli.js"
DOTNET_CLI="$ROOT_DIR/src/dotnet/TraceMap.Cli"
TARGET_ENDPOINT="GET /api/admin/runner/get-by-id/{}"

require_cmd() {
  local name="$1"
  command -v "$name" >/dev/null 2>&1 || {
    echo "Missing required command: $name" >&2
    exit 1
  }
}

require_file() {
  local path="$1"
  test -f "$path" || {
    echo "Missing required file: $path" >&2
    exit 1
  }
}

run_ts_scan() {
  local repo_path="$1"
  local out_path="$2"
  node "$TS_CLI" scan --repo "$repo_path" --out "$out_path"
  require_file "$out_path/scan-manifest.json"
  require_file "$out_path/facts.ndjson"
  require_file "$out_path/index.sqlite"
  require_file "$out_path/report.md"
  require_file "$out_path/logs/analyzer.log"
}

run_dotnet_scan() {
  local repo_path="$1"
  local out_path="$2"
  dotnet run --project "$DOTNET_CLI" -- scan --repo "$repo_path" --out "$out_path"
  require_file "$out_path/scan-manifest.json"
  require_file "$out_path/facts.ndjson"
  require_file "$out_path/index.sqlite"
  require_file "$out_path/report.md"
  require_file "$out_path/logs/analyzer.log"
}

assert_no_markdown_leaks() {
  local markdown="$1"
  local mac_home_prefix=$'\x2f\x55\x73\x65\x72\x73\x2f'
  local private_tmp_prefix=$'\x2f\x70\x72\x69\x76\x61\x74\x65\x2f\x74\x6d\x70\x2f'
  local tmp_tracemap_prefix=$'\x2f\x74\x6d\x70\x2f\x74\x72\x61\x63\x65\x6d\x61\x70\x2d'
  local leak_pattern="TRACEMAP_SQL_SENTINEL|${mac_home_prefix}|${private_tmp_prefix}|${tmp_tracemap_prefix}"
  require_file "$markdown"
  if grep -Eq "$leak_pattern" "$markdown"; then
    echo "Markdown leak assertion failed: $markdown" >&2
    exit 1
  fi
}

require_cmd dotnet
require_cmd npm
require_cmd node

echo "TraceMap combined paths smoke output: $OUT_ROOT"
mkdir -p "$OUT_ROOT"

npm --prefix "$TS_DIR" run build

CLIENT_OUT="$OUT_ROOT/client"
SERVER_OUT="$OUT_ROOT/server"
COMBINED_INDEX="$OUT_ROOT/combined.sqlite"
REPORT_OUT="$OUT_ROOT/dependency-report"
PATHS_OUT="$OUT_ROOT/paths"
TARGET_PATHS_OUT="$OUT_ROOT/paths-runner-sql"
TARGET_PATHS_OUT_SECOND="$OUT_ROOT/paths-runner-sql-second"
BOGUS_PATHS_OUT="$OUT_ROOT/paths-bogus"

run_ts_scan "$ROOT_DIR/samples/endpoint-client-angular" "$CLIENT_OUT"
run_dotnet_scan "$ROOT_DIR/samples/endpoint-server-aspnet" "$SERVER_OUT"

dotnet run --project "$DOTNET_CLI" -- combine \
  --index "$CLIENT_OUT/index.sqlite" --label sample-client \
  --index "$SERVER_OUT/index.sqlite" --label sample-server \
  --out "$COMBINED_INDEX"
require_file "$COMBINED_INDEX"

dotnet run --project "$DOTNET_CLI" -- report --index "$COMBINED_INDEX" --out "$REPORT_OUT"
require_file "$REPORT_OUT/dependency-report.md"
require_file "$REPORT_OUT/dependency-report.json"

dotnet run --project "$DOTNET_CLI" -- paths --index "$COMBINED_INDEX" --out "$PATHS_OUT"
require_file "$PATHS_OUT/paths-report.md"
require_file "$PATHS_OUT/paths-report.json"

dotnet run --project "$DOTNET_CLI" -- paths \
  --index "$COMBINED_INDEX" \
  --from-endpoint "$TARGET_ENDPOINT" \
  --to-surface sql-query \
  --out "$TARGET_PATHS_OUT"
require_file "$TARGET_PATHS_OUT/paths-report.md"
require_file "$TARGET_PATHS_OUT/paths-report.json"

dotnet run --project "$DOTNET_CLI" -- paths \
  --index "$COMBINED_INDEX" \
  --from-endpoint "$TARGET_ENDPOINT" \
  --to-surface sql-query \
  --out "$TARGET_PATHS_OUT_SECOND"
cmp -s "$TARGET_PATHS_OUT/paths-report.json" "$TARGET_PATHS_OUT_SECOND/paths-report.json"

dotnet run --project "$DOTNET_CLI" -- paths \
  --index "$COMBINED_INDEX" \
  --from-endpoint "GET /api/admin/runner/missing/{}" \
  --to-surface sql-query \
  --out "$BOGUS_PATHS_OUT"
require_file "$BOGUS_PATHS_OUT/paths-report.json"

node - "$REPORT_OUT/dependency-report.json" "$TARGET_PATHS_OUT/paths-report.json" "$BOGUS_PATHS_OUT/paths-report.json" <<'NODE'
const fs = require("node:fs");
const [dependencyPath, pathsPath, bogusPath] = process.argv.slice(2);
const dependency = JSON.parse(fs.readFileSync(dependencyPath, "utf8"));
const paths = JSON.parse(fs.readFileSync(pathsPath, "utf8"));
const bogus = JSON.parse(fs.readFileSync(bogusPath, "utf8"));
const targetKey = "/api/admin/runner/get-by-id/{}";

function fail(message) {
  console.error(message);
  process.exit(1);
}

function assert(condition, message) {
  if (!condition) {
    fail(message);
  }
}

function hasVolatileKey(value) {
  if (Array.isArray(value)) {
    return value.some(hasVolatileKey);
  }
  if (value && typeof value === "object") {
    return Object.entries(value).some(([key, child]) =>
      /generatedAt|timestamp/i.test(key) || hasVolatileKey(child));
  }
  return false;
}

const labels = (dependency.sources ?? []).map(source => source.label).sort();
assert(labels.length === 2 && labels[0] === "sample-client" && labels[1] === "sample-server",
  `Expected exactly sample-client/sample-server sources, got ${labels.join(",")}`);

const targetFinding = (dependency.endpointFindings ?? []).find(finding =>
  finding.clientSourceLabel === "sample-client"
  && finding.serverSourceLabel === "sample-server"
  && finding.normalizedPathKey === targetKey);
assert(targetFinding, `Expected endpoint finding for ${targetKey}`);
assert(["MatchedEndpoint", "AmbiguousMatch"].includes(targetFinding.classification),
  `Unexpected endpoint classification: ${targetFinding.classification}`);

assert(!hasVolatileKey(paths), "Paths JSON contains a volatile generatedAt/timestamp field");
assert((paths.summary?.pathCount ?? 0) > 0, "Expected at least one targeted sql-query path");

for (const path of paths.paths ?? []) {
  assert((path.nodes ?? []).length > 0, `Path ${path.pathId} has no nodes`);
  assert((path.edges ?? []).length > 0, `Path ${path.pathId} has no edges`);
  for (const edge of path.edges) {
    assert(edge.ruleId, `Path ${path.pathId} edge ${edge.edgeId} is missing ruleId`);
    assert(edge.evidenceTier, `Path ${path.pathId} edge ${edge.edgeId} is missing evidenceTier`);
  }
}

for (const gap of paths.gaps ?? []) {
  assert(gap.ruleId, `Gap ${gap.gapId} is missing ruleId`);
  assert(gap.evidenceTier, `Gap ${gap.gapId} is missing evidenceTier`);
}

const usefulClassifications = new Set(["StrongStaticPath", "ProbableStaticPath", "NeedsReviewPath"]);
const connectedSqlPath = (paths.paths ?? []).find(path => {
  const sources = new Set((path.nodes ?? []).map(node => node.sourceLabel));
  const edgeKinds = new Set((path.edges ?? []).map(edge => edge.edgeKind));
  const terminal = path.nodes?.[path.nodes.length - 1];
  return sources.has("sample-client")
    && sources.has("sample-server")
    && terminal?.surfaceKind === "sql-query"
    && edgeKinds.has("endpoint-match")
    && edgeKinds.has("calls")
    && edgeKinds.has("symbol-reconciliation")
    && edgeKinds.has("surface-evidence")
    && usefulClassifications.has(path.classification);
});
assert(connectedSqlPath, "Expected a connected sample-client -> sample-server -> sql-query path");

assert((bogus.summary?.pathCount ?? 0) === 0, "Bogus endpoint query should return zero paths");
assert((bogus.gaps ?? []).some(gap => ["SelectorNoMatch", "NoPathFound", "UnknownAnalysisGap"].includes(gap.gapKind)),
  "Bogus endpoint query should include a selector/no-path/coverage gap");

console.log(`coverage=${paths.reportCoverage}`);
console.log(`endpoint=${targetFinding.classification}:${targetFinding.staticMatchQuality}`);
console.log(`paths=${paths.summary.pathCount}`);
console.log(`gaps=${paths.summary.gapCount}`);
console.log(`connectedPath=${connectedSqlPath.pathId}:${connectedSqlPath.classification}`);
NODE

assert_no_markdown_leaks "$TARGET_PATHS_OUT/paths-report.md"

echo
echo "Combined paths smoke complete: $OUT_ROOT"
