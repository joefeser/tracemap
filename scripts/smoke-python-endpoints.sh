#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_ROOT="${1:-"$(mktemp -d)"}"
PYTHON_BIN="${PYTHON_BIN:-python3}"
DOTNET_CLI="$ROOT_DIR/src/dotnet/TraceMap.Cli"

require_file() {
  local path="$1"
  test -f "$path"
}

require_report_classification() {
  local report_path="$1"
  local classification="$2"
  grep -q "\`$classification\`" "$report_path"
}

run_python_scan() {
  local repo_path="$1"
  local out_path="$2"
  "$PYTHON_BIN" -m tracemap_py.cli scan --repo "$repo_path" --out "$out_path"
  require_file "$out_path/scan-manifest.json"
  require_file "$out_path/facts.ndjson"
  require_file "$out_path/index.sqlite"
  require_file "$out_path/report.md"
  require_file "$out_path/logs/analyzer.log"
}

echo "TraceMap Python endpoint smoke output: $OUT_ROOT"
mkdir -p "$OUT_ROOT"

CLIENT_OUT="$OUT_ROOT/python-client"
SERVER_OUT="$OUT_ROOT/python-server"
REPORT_OUT="$OUT_ROOT/python-endpoints"

run_python_scan "$ROOT_DIR/samples/python-client-sample" "$CLIENT_OUT"
run_python_scan "$ROOT_DIR/samples/python-fastapi-sample" "$SERVER_OUT"

dotnet run --project "$DOTNET_CLI" -- endpoints \
  --client-index "$CLIENT_OUT/index.sqlite" \
  --server-index "$SERVER_OUT/index.sqlite" \
  --client-label "python-client" \
  --server-label "python-fastapi" \
  --out "$REPORT_OUT"

require_file "$REPORT_OUT/endpoint-report.md"
require_file "$REPORT_OUT/endpoint-report.json"
require_report_classification "$REPORT_OUT/endpoint-report.md" "MatchedEndpoint"

echo
echo "Smoke complete: $OUT_ROOT"
