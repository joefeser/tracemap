#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CACHE_ROOT="${1:-"/tmp/tracemap-oss-cache"}"
OUT_ROOT="${2:-"/tmp/tracemap-oss-smoke"}"
DOTNET_CLI="${ROOT_DIR}/src/dotnet/TraceMap.Cli"
TS_CLI="${ROOT_DIR}/src/typescript/dist/src/cli.js"
JVM_CLI="${ROOT_DIR}/src/jvm/build/install/tracemap-jvm/bin/tracemap-jvm"
JDK21_HOME="${JAVA_HOME:-"/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home"}"
PYTHON_BIN="${TRACEMAP_PYTHON:-python3}"

mkdir -p "$CACHE_ROOT" "$OUT_ROOT"

if [[ "${TRACEMAP_SKIP_BUILD:-0}" != "1" ]]; then
  dotnet build "$ROOT_DIR/src/dotnet/TraceMap.sln"
  npm run check --prefix "$ROOT_DIR/src/typescript"
  JAVA_HOME="$JDK21_HOME" gradle -p "$ROOT_DIR/src/jvm" installDist
  "$PYTHON_BIN" -m py_compile "$ROOT_DIR"/src/python/tracemap_py/*.py
fi

clone_checkout() {
  local label="$1"
  local url="$2"
  local sha="$3"
  local dest="$CACHE_ROOT/$label"

  if [[ ! -d "$dest/.git" ]]; then
    rm -rf "$dest"
    git clone --quiet "$url" "$dest"
  fi
  git -C "$dest" fetch --quiet origin "$sha"
  git -C "$dest" checkout --quiet "$sha"
  git -C "$dest" clean -fdx --quiet
  printf '%s\n' "$dest"
}

require_artifacts() {
  local out="$1"
  test -f "$out/scan-manifest.json"
  test -f "$out/facts.ndjson"
  test -f "$out/index.sqlite"
  test -f "$out/report.md"
  test -f "$out/logs/analyzer.log"
}

sqlite_count() {
  local index="$1"
  local table="$2"
  if command -v sqlite3 >/dev/null 2>&1; then
    sqlite3 "$index" "select count(*) from $table;" 2>/dev/null || printf 'n/a'
  else
    printf 'sqlite3-missing'
  fi
}

fact_count() {
  local index="$1"
  local fact_type="$2"
  if command -v sqlite3 >/dev/null 2>&1; then
    sqlite3 "$index" "select count(*) from facts where fact_type='$fact_type';" 2>/dev/null || printf 'n/a'
  else
    printf 'sqlite3-missing'
  fi
}

manifest_value() {
  local manifest="$1"
  local key="$2"
  python3 - "$manifest" "$key" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    print(json.load(handle).get(sys.argv[2], ""))
PY
}

print_summary() {
  local label="$1"
  local out="$2"
  local index="$out/index.sqlite"
  local manifest="$out/scan-manifest.json"

  printf '\n== %s ==\n' "$label"
  printf 'commit=%s\n' "$(manifest_value "$manifest" commitSha)"
  printf 'analysisLevel=%s\n' "$(manifest_value "$manifest" analysisLevel)"
  printf 'buildStatus=%s\n' "$(manifest_value "$manifest" buildStatus)"
  printf 'facts=%s call_edges=%s object_creations=%s argument_flows=%s http_routes=%s sql=%s analysis_gaps=%s\n' \
    "$(sqlite_count "$index" facts)" \
    "$(sqlite_count "$index" call_edges)" \
    "$(sqlite_count "$index" object_creations)" \
    "$(sqlite_count "$index" argument_flows)" \
    "$(fact_count "$index" HttpRouteBinding)" \
    "$(fact_count "$index" SqlTextUsed)" \
    "$(fact_count "$index" AnalysisGap)"
}

scan_dotnet() {
  local label="$1"
  local url="$2"
  local sha="$3"
  local repo
  repo="$(clone_checkout "$label" "$url" "$sha")"
  local out="$OUT_ROOT/$label"
  rm -rf "$out"
  dotnet run --project "$DOTNET_CLI" -- scan --repo "$repo" --out "$out"
  require_artifacts "$out"
  print_summary "$label" "$out"
}

scan_typescript() {
  local label="$1"
  local url="$2"
  local sha="$3"
  local repo
  repo="$(clone_checkout "$label" "$url" "$sha")"
  local out="$OUT_ROOT/$label"
  rm -rf "$out"
  node "$TS_CLI" scan --repo "$repo" --out "$out"
  require_artifacts "$out"
  print_summary "$label" "$out"
}

scan_jvm() {
  local label="$1"
  local url="$2"
  local sha="$3"
  local repo
  repo="$(clone_checkout "$label" "$url" "$sha")"
  local out="$OUT_ROOT/$label"
  rm -rf "$out"
  JAVA_HOME="$JDK21_HOME" "$JVM_CLI" scan --repo "$repo" --out "$out"
  require_artifacts "$out"
  print_summary "$label" "$out"
}

scan_python() {
  local label="$1"
  local url="$2"
  local sha="$3"
  local scan_subdir="${4:-"."}"
  local repo
  repo="$(clone_checkout "$label" "$url" "$sha")"
  local scan_root="$repo"
  if [[ "$scan_subdir" != "." ]]; then
    scan_root="$repo/$scan_subdir"
  fi
  local out="$OUT_ROOT/$label"
  rm -rf "$out"
  PYTHONPATH="$ROOT_DIR/src/python" "$PYTHON_BIN" -m tracemap_py.cli scan --repo "$scan_root" --out "$out"
  require_artifacts "$out"
  print_summary "$label" "$out"
}

printf 'TraceMap OSS smoke cache: %s\n' "$CACHE_ROOT"
printf 'TraceMap OSS smoke output: %s\n' "$OUT_ROOT"

scan_dotnet "ProjectExtensions.Azure.ServiceBus" "https://github.com/ProjectExtensions/ProjectExtensions.Azure.ServiceBus.git" "2a8e72c8f5680edf2096b05ac08c39d47a95cef8"
scan_dotnet "fluentjdf" "https://github.com/joefeser/fluentjdf.git" "9490e699a89bb21f4aabf198173fc6382f84a53f"
scan_typescript "scip-typescript" "https://github.com/sourcegraph/scip-typescript.git" "891eb4293709a6a587bf4468dfa1b45a85182fd9"
scan_jvm "scip-java" "https://github.com/sourcegraph/scip-java.git" "825463cb15d540d45c680593aad1f634330435cf"
scan_jvm "spring-petclinic" "https://github.com/spring-projects/spring-petclinic.git" "a2c2ef994340d3970eb6db51247456a51bb161f8"
scan_jvm "okio" "https://github.com/square/okio.git" "cad7ff1057307142149b1a28dfcb49117e89b0d3"
scan_python "full-stack-fastapi-template" "https://github.com/fastapi/full-stack-fastapi-template.git" "1c1175eb5045e6e8fca3bcbc4134630f3ae640ba" "backend"
scan_python "microblog" "https://github.com/miguelgrinberg/microblog.git" "a975ef64864354867c88e0ed3a17ba7d17dca752"
scan_python "sqlalchemy" "https://github.com/sqlalchemy/sqlalchemy.git" "bfe559a7e4d69e5699c390ac9cafd2a5a2d38078"

printf '\nOSS smoke complete: %s\n' "$OUT_ROOT"
