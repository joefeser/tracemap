#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_ROOT="${1:-"/tmp/tracemap-swift-route-flow-smoke"}"
SCAN_OUT="$OUT_ROOT/scan"
COMBINED_INDEX="$OUT_ROOT/combined.sqlite"
REPORT_OUT="$OUT_ROOT/route-flow"

rm -rf "$OUT_ROOT"
mkdir -p "$OUT_ROOT"

swift run --package-path "$ROOT_DIR/src/swift" tracemap-swift scan \
  --repo "$ROOT_DIR/samples/swift-http-api-client-surfaces" \
  --out "$SCAN_OUT"

dotnet run --project "$ROOT_DIR/src/dotnet/TraceMap.Cli" -- combine \
  --index "$SCAN_OUT/index.sqlite" \
  --label swift \
  --out "$COMBINED_INDEX"

dotnet run --project "$ROOT_DIR/src/dotnet/TraceMap.Cli" -- route-flow \
  --index "$COMBINED_INDEX" \
  --client-call "GET /v1/orders/{}" \
  --out "$REPORT_OUT" \
  --format json

test -s "$REPORT_OUT/route-flow-report.md"
test -s "$REPORT_OUT/route-flow-report.json"

python3 - "$REPORT_OUT/route-flow-report.json" "$REPORT_OUT/route-flow-report.md" <<'PY'
import json
import sys
from pathlib import Path

json_path = Path(sys.argv[1])
markdown_path = Path(sys.argv[2])
report = json.loads(json_path.read_text(encoding="utf-8"))
markdown = markdown_path.read_text(encoding="utf-8")
serialized = json.dumps(report, sort_keys=True)

if report.get("reportType") != "route-flow":
    raise SystemExit("route-flow reportType missing")

summary = report.get("summary", {})
if summary.get("entryEvidenceCount", 0) < 1:
    raise SystemExit("route-flow entry evidence missing")
if summary.get("flowRowCount", 0) < 3:
    raise SystemExit("route-flow static flow rows missing")
if summary.get("dependencySurfaceCount", 0) < 1:
    raise SystemExit("route-flow dependency surface missing")
if len(report.get("touchedFiles") or []) < 1:
    raise SystemExit("route-flow touched files missing")
if len(report.get("touchedSymbols") or []) < 1:
    raise SystemExit("route-flow touched symbols missing")

entry = report.get("entryEvidence") or []
if not any("swift.http.client-library.v1" in row.get("evidence", {}).get("supportingRuleIds", []) for row in entry):
    raise SystemExit("Swift HTTP supporting rule missing from entry evidence")

flow_rows = report.get("flowRows") or []
if not any(row.get("edgeKind") == "route-bound-to-symbol" for row in flow_rows):
    raise SystemExit("Swift route-flow method-symbol bridge missing")
if not any(row.get("rowKind") == "terminal-surface" and row.get("targetSymbol") == "/v1/orders/{}" for row in flow_rows):
    raise SystemExit("Swift route-flow terminal HTTP surface missing")

limitations = report.get("limitations") or []
if not any("runtime execution" in item for item in limitations):
    raise SystemExit("route-flow runtime limitation missing")

for unsafe in ["https://api.example.invalid", "super-secret", "do-not-render"]:
    if unsafe in serialized or unsafe in markdown:
        raise SystemExit(f"unsafe value leaked into route-flow output: {unsafe}")
PY

printf 'Swift route-flow smoke complete: %s\n' "$REPORT_OUT"
