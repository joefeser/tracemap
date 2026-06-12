#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SAMPLE_ROOT="${1:-"${ROOT_DIR}/../c-sharp-sample-repos"}"
OUT_ROOT="${2:-"$(mktemp -d)"}"
CLI_PROJECT="${ROOT_DIR}/src/dotnet/TraceMap.Cli"

run_scan() {
  local repo_path="$1"
  local out_path="$2"

  dotnet run --project "$CLI_PROJECT" -- scan --repo "$repo_path" --out "$out_path"
  test -f "$out_path/scan-manifest.json"
  test -f "$out_path/facts.ndjson"
  test -f "$out_path/index.sqlite"
  test -f "$out_path/report.md"
  test -f "$out_path/logs/analyzer.log"
}

run_reduce() {
  local index_path="$1"
  local delta_path="$2"
  local report_path="$3"

  dotnet run --project "$CLI_PROJECT" -- reduce --index "$index_path" --contract-delta "$delta_path" --out "$report_path"
  test -f "$report_path"
  grep -q "Reducer rule: \`contract.delta.reduce.v1\`" "$report_path"
  grep -q "Classification:" "$report_path"
}

print_summary() {
  local label="$1"
  local manifest_path="$2"
  local report_path="$3"

  echo
  echo "== $label =="
  grep -E '"analysisLevel"|"buildStatus"|"commitSha"' "$manifest_path" || true
  grep -E 'Classification:|Warnings:' "$report_path" || true
}

echo "TraceMap smoke output: $OUT_ROOT"

MODERN_OUT="$OUT_ROOT/modern-sample"
MODERN_REPORT="$OUT_ROOT/modern-sample-impact.md"
run_scan "$ROOT_DIR/samples/modern-sample" "$MODERN_OUT"
run_reduce "$MODERN_OUT/index.sqlite" "$ROOT_DIR/samples/contract-deltas/modern-sample.customer-profile.json" "$MODERN_REPORT"
grep -q "Classification: \`DefiniteImpact\`" "$MODERN_REPORT"
print_summary "modern-sample" "$MODERN_OUT/scan-manifest.json" "$MODERN_REPORT"

if [[ ! -d "$SAMPLE_ROOT" ]]; then
  echo
  echo "External sample root not found, skipping: $SAMPLE_ROOT"
  exit 0
fi

SERVICEBUS_REPO="$SAMPLE_ROOT/ProjectExtensions.Azure.ServiceBus"
if [[ -d "$SERVICEBUS_REPO" ]]; then
  SERVICEBUS_OUT="$OUT_ROOT/servicebus"
  SERVICEBUS_REPORT="$OUT_ROOT/servicebus-impact.md"
  run_scan "$SERVICEBUS_REPO" "$SERVICEBUS_OUT"
  run_reduce "$SERVICEBUS_OUT/index.sqlite" "$ROOT_DIR/samples/contract-deltas/servicebus.transient-status.json" "$SERVICEBUS_REPORT"
  print_summary "ProjectExtensions.Azure.ServiceBus" "$SERVICEBUS_OUT/scan-manifest.json" "$SERVICEBUS_REPORT"
else
  echo "Skipping missing external repo: $SERVICEBUS_REPO"
fi

FLUENTJDF_REPO="$SAMPLE_ROOT/fluentjdf"
if [[ -d "$FLUENTJDF_REPO" ]]; then
  FLUENTJDF_OUT="$OUT_ROOT/fluentjdf"
  FLUENTJDF_REPORT="$OUT_ROOT/fluentjdf-impact.md"
  run_scan "$FLUENTJDF_REPO" "$FLUENTJDF_OUT"
  run_reduce "$FLUENTJDF_OUT/index.sqlite" "$ROOT_DIR/samples/contract-deltas/fluentjdf.status-builder.json" "$FLUENTJDF_REPORT"
  print_summary "fluentjdf" "$FLUENTJDF_OUT/scan-manifest.json" "$FLUENTJDF_REPORT"
else
  echo "Skipping missing external repo: $FLUENTJDF_REPO"
fi

echo
echo "Smoke complete: $OUT_ROOT"
