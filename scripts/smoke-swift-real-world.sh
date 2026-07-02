#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CACHE_ROOT="${1:-"/tmp/tracemap-swift-real-world-cache"}"
OUT_ROOT="${2:-"/tmp/tracemap-swift-real-world-smoke"}"
SWIFT_CLI_ARGS=(--package-path "$ROOT_DIR/src/swift")
DOTNET_CLI="${ROOT_DIR}/src/dotnet/TraceMap.Cli"

mkdir -p "$CACHE_ROOT" "$OUT_ROOT"

if [[ "${TRACEMAP_SKIP_BUILD:-0}" != "1" ]]; then
  swift build "${SWIFT_CLI_ARGS[@]}"
fi

# label|owner/repo|sha|why
SAMPLES=(
  "icecubesapp|Dimillian/IceCubesApp|9c05a720597b3ff13de2e241bf58d3fba0863c09|SwiftUI Mastodon client with real federated API client and UI surface evidence"
  "mastodon-ios|mastodon/mastodon-ios|95ac4a6d726ebf9fa867036dbf9d72f0a4b5f534|Official Mastodon iOS app with real backend/API client and mobile app structure evidence"
  "kickstarter-ios|kickstarter/ios-oss|203971bdf40f3a3a5071ce0c1fbc4eb3cad5b094|Product iOS app with real backend/API client, view model, dependency, and persistence-adjacent evidence"
)

selected_labels="${TRACEMAP_SWIFT_REAL_WORLD_REPOS:-}"

is_selected() {
  local label="$1"
  if [[ -z "$selected_labels" ]]; then
    return 0
  fi
  IFS=',' read -r -a requested <<<"$selected_labels"
  for requested_label in "${requested[@]}"; do
    if [[ "$requested_label" == "$label" ]]; then
      return 0
    fi
  done
  return 1
}

clone_checkout() {
  local label="$1"
  local slug="$2"
  local sha="$3"
  local dest="$CACHE_ROOT/$label"

  if [[ ! -d "$dest/.git" ]]; then
    rm -rf "$dest"
    mkdir -p "$dest"
    git -C "$dest" init --quiet
    git -C "$dest" remote add origin "https://github.com/${slug}.git"
  fi

  git -C "$dest" fetch --quiet --depth 1 origin "$sha"
  git -C "$dest" checkout --quiet --detach "$sha"
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

summarize_sample() {
  local label="$1"
  local slug="$2"
  local sha="$3"
  local why="$4"
  local out="$5"
  local summary_json="$6"

  python3 - "$label" "$slug" "$sha" "$why" "$out" "$summary_json" <<'PY'
import json
import sqlite3
import sys
from pathlib import Path

label, slug, sha, why, out, summary_json = sys.argv[1:]
out_path = Path(out)
manifest_path = out_path / "scan-manifest.json"
index_path = out_path / "index.sqlite"

with manifest_path.open(encoding="utf-8") as handle:
    manifest = json.load(handle)

def query_scalar(sql, params=()):
    try:
        with sqlite3.connect(index_path) as connection:
            row = connection.execute(sql, params).fetchone()
            return row[0] if row else 0
    except sqlite3.Error:
        return "n/a"

def query_pairs(sql, params=()):
    try:
        with sqlite3.connect(index_path) as connection:
            return [
                {"key": str(row[0]), "count": int(row[1])}
                for row in connection.execute(sql, params).fetchall()
            ]
    except sqlite3.Error:
        return []

fact_total = query_scalar("select count(*) from facts")
analysis_gaps = query_scalar("select count(*) from facts where fact_type = 'AnalysisGap'")
swift_rule_counts = query_pairs(
    "select rule_id, count(*) from facts where rule_id like 'swift.%' group by rule_id order by rule_id"
)
top_fact_counts = query_pairs(
    "select fact_type, count(*) from facts group by fact_type order by count(*) desc, fact_type limit 20"
)

def count_like(column, patterns):
    clauses = " or ".join([f"{column} like ?" for _ in patterns])
    return query_scalar(f"select count(*) from facts where {clauses}", tuple(patterns))

summary = {
    "label": label,
    "repo": slug,
    "pinnedSha": sha,
    "why": why,
    "artifactDirectory": label,
    "manifestCommitSha": manifest.get("commitSha", ""),
    "analysisLevel": manifest.get("analysisLevel", ""),
    "buildStatus": manifest.get("buildStatus", ""),
    "coverageLabel": manifest.get("coverageLabel", manifest.get("analysisLevel", "")),
    "factCount": fact_total,
    "analysisGapCount": analysis_gaps,
    "evidenceFamilies": {
        "swiftRuleFacts": sum(item["count"] for item in swift_rule_counts),
        "declarationFacts": count_like("fact_type", ("%Declaration%", "TypeDeclared", "MethodDeclared", "PropertyDeclared", "FieldDeclared", "ParameterDeclared")),
        "callCandidateFacts": count_like("fact_type", ("%Call%", "%Construction%")),
        "httpApiClientFacts": count_like("rule_id", ("swift.http.%", "%http%", "%api%")),
        "uiSurfaceFacts": count_like("rule_id", ("swift.ui.%",)),
        "storageDataFacts": count_like("rule_id", ("swift.storage.%",)),
        "packageDependencyFacts": count_like("rule_id", ("swift.package.%", "swift.dependency.%")),
    },
    "topFactTypes": top_fact_counts,
    "swiftRuleCounts": swift_rule_counts,
    "artifacts": {
        "scan-manifest.json": True,
        "facts.ndjson": True,
        "index.sqlite": True,
        "report.md": True,
        "logs/analyzer.log": True,
    },
    "limitations": [
        "Static evidence only; no Xcode build, SwiftPM resolution, simulator, device, app execution, runtime network call, auth, or production telemetry was used.",
        "Rows are evidence-backed candidates and coverage-relative findings, not runtime endpoint reachability or complete Swift semantic proof.",
    ],
}

Path(summary_json).write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")
PY
}

write_markdown_summary() {
  local output="$OUT_ROOT/swift-real-world-smoke-summary.md"
  python3 - "$OUT_ROOT" "$output" <<'PY'
import json
import sys
from pathlib import Path

out_root = Path(sys.argv[1])
output = Path(sys.argv[2])
summaries = []
for path in sorted(out_root.glob("*/summary.json")):
    with path.open(encoding="utf-8") as handle:
        summaries.append(json.load(handle))

lines = [
    "# Swift Real-World Smoke Summary",
    "",
    "This generated summary is public-safe by construction: it uses pinned public repository slugs, commit SHAs, artifact directory labels, counts, rule IDs, coverage labels, and limitations. It does not include local absolute paths, clone URLs, source snippets, raw SQL, credentials, config values, or runtime observations.",
    "",
    "| Sample | Pinned SHA | Analysis | Facts | Gaps | HTTP/API | UI | Storage/Data | Packages |",
    "| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |",
]

for item in summaries:
    families = item["evidenceFamilies"]
    lines.append(
        "| {label} | `{sha}` | `{analysis}` / `{build}` | {facts} | {gaps} | {http} | {ui} | {storage} | {packages} |".format(
            label=item["label"],
            sha=item["pinnedSha"][:12],
            analysis=item["analysisLevel"],
            build=item["buildStatus"],
            facts=item["factCount"],
            gaps=item["analysisGapCount"],
            http=families["httpApiClientFacts"],
            ui=families["uiSurfaceFacts"],
            storage=families["storageDataFacts"],
            packages=families["packageDependencyFacts"],
        )
    )

lines.extend(["", "## Samples", ""])
for item in summaries:
    lines.extend(
        [
            f"### {item['label']}",
            "",
            f"- Repository: `{item['repo']}`",
            f"- Pinned SHA: `{item['pinnedSha']}`",
            f"- Why included: {item['why']}",
            f"- Artifact directory: `{item['artifactDirectory']}`",
            f"- Manifest commit SHA: `{item['manifestCommitSha']}`",
            f"- Coverage: `{item['coverageLabel']}`",
            f"- Top fact types: "
            + ", ".join(f"`{entry['key']}`={entry['count']}" for entry in item["topFactTypes"][:8]),
            "",
        ]
    )

lines.extend(
    [
        "## Limitations",
        "",
        "- This smoke does not run Xcode builds, SwiftPM dependency resolution, simulators, devices, app code, network calls, auth flows, or production telemetry.",
        "- Counts are static evidence counts. They are not runtime reachability, endpoint compatibility, complete app navigation, or impact conclusions.",
    ]
)

output.write_text("\n".join(lines) + "\n", encoding="utf-8")
PY
}

printf 'TraceMap Swift real-world smoke cache: %s\n' "$CACHE_ROOT"
printf 'TraceMap Swift real-world smoke output: %s\n' "$OUT_ROOT"

for sample in "${SAMPLES[@]}"; do
  IFS='|' read -r label slug sha why <<<"$sample"
  if ! is_selected "$label"; then
    continue
  fi

  printf '\n== %s ==\n' "$label"
  repo="$(clone_checkout "$label" "$slug" "$sha")"
  out="$OUT_ROOT/$label"
  rm -rf "$out"
  swift run "${SWIFT_CLI_ARGS[@]}" tracemap-swift scan --repo "$repo" --out "$out"
  require_artifacts "$out"
  summarize_sample "$label" "$slug" "$sha" "$why" "$out" "$out/summary.json"
done

write_markdown_summary
printf '\nSwift real-world smoke complete: %s\n' "$OUT_ROOT"
printf 'Summary: %s\n' "$OUT_ROOT/swift-real-world-smoke-summary.md"
