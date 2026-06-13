from __future__ import annotations

from collections import Counter

from .models import CodeFact, ScanManifest


def render_report(manifest: ScanManifest, facts: list[CodeFact]) -> str:
    by_type = Counter(f.fact_type for f in facts)
    by_tier = Counter(f.evidence_tier for f in facts)
    by_rule = Counter(f.rule_id for f in facts)
    lines = [
        "# TraceMap Python Scan Report",
        "",
        f"- Repo: `{manifest.repo_name}`",
        f"- Commit SHA: `{manifest.commit_sha}`",
        f"- Scanner: `{manifest.scanner_version}`",
        f"- Analysis level: `{manifest.analysis_level}`",
        f"- Build status: `{manifest.build_status}`",
        "",
        "Python MVP scans use AST/package/config evidence and do not perform full type-checker semantic analysis. `FailedOrPartial` is expected for reduced Python MVP coverage and does not by itself indicate a scanner error.",
        "",
        "## Fact Counts",
        "",
        "| Fact type | Count |",
        "| --- | ---: |",
    ]
    for key, count in sorted(by_type.items()):
        lines.append(f"| `{key}` | {count} |")
    lines.extend(["", "## Evidence Tiers", "", "| Tier | Count |", "| --- | ---: |"])
    for key, count in sorted(by_tier.items()):
        lines.append(f"| `{key}` | {count} |")
    lines.extend(["", "## Rules", "", "| Rule | Count |", "| --- | ---: |"])
    for key, count in sorted(by_rule.items()):
        lines.append(f"| `{key}` | {count} |")
    lines.extend(["", "## Known Gaps", ""])
    if manifest.known_gaps:
        for gap in manifest.known_gaps:
            lines.append(f"- {gap}")
    else:
        lines.append("- None recorded.")
    lines.extend(
        [
            "",
            "## Python Limitations",
            "",
            "- Target modules were not imported or executed.",
            "- Decorators, framework startup, runtime dependency injection, branch feasibility, and dynamic dispatch were not evaluated.",
            "- Real Python MVP facts do not emit Tier1 `PropertyAccessed` or `MethodInvoked`.",
            "- No-match reducer outcomes are expected to be `NoEvidenceReducedCoverage` for MVP Python indexes.",
            "",
        ]
    )
    return "\n".join(lines)
