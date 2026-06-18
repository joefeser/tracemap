#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import sqlite3
import subprocess
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


DEFAULT_TIMEOUT_SECONDS = 20 * 60
DEFAULT_MAX_ARTIFACT_BYTES = 500 * 1024 * 1024
LEGACY_ROOT = Path(".tmp/legacy-codebase-validation")
SUMMARY_RULE_ID = "legacy.validation.summary.v1"
UI_RULE_ID = "legacy.validation.ui-events.v1"
ENV_RULE_ID = "legacy.validation.environment.v1"
BOUNDS_RULE_ID = "legacy.validation.bounds.v1"
WCF_RULE_IDS = [
    "legacy.wcf.config.v1",
    "legacy.wcf.contract.v1",
    "legacy.wcf.host.v1",
    "legacy.wcf.metadata.v1",
    "legacy.wcf.operation-normalization.v1",
    "legacy.wcf.mapping.v1",
]


@dataclass(frozen=True)
class Sample:
    label: str
    path: Path
    kind: str
    timeout_seconds: int = DEFAULT_TIMEOUT_SECONDS
    max_artifact_bytes: int = DEFAULT_MAX_ARTIFACT_BYTES


@dataclass(frozen=True)
class Manifest:
    samples: tuple[Sample, ...]
    private_name_fragments: tuple[str, ...] = ()


@dataclass
class RedactionFailure:
    category: str


@dataclass
class SampleSummary:
    label: str
    kind: str
    status: str
    exit_code: int | None
    duration_seconds: float
    output_size_bytes: int
    truncated: bool
    deferred: bool
    artifacts: dict[str, bool]
    facts_count: int
    analysis_gap_count: int
    coverage_label: str
    build_status: str
    commit_sha_present: bool
    legacy_indicators: dict[str, Any]
    ui_event_probe: dict[str, Any]
    wcf_fact_counts: dict[str, int]
    limitations: list[str] = field(default_factory=list)


class ValidationError(Exception):
    def __init__(self, category: str, message: str):
        super().__init__(message)
        self.category = category


def repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


def as_repo_relative(path: Path, root: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError as exc:
        raise ValidationError("path-boundary", "path must be inside the repository") from exc


def require_under_legacy_root(path: Path, root: Path, *, allow_file: bool) -> Path:
    full = path if path.is_absolute() else root / path
    full = full.resolve()
    legacy = (root / LEGACY_ROOT).resolve()
    try:
        full.relative_to(legacy)
    except ValueError as exc:
        raise ValidationError("path-boundary", "manifest and output paths must be under .tmp/legacy-codebase-validation") from exc
    if allow_file:
        full.parent.mkdir(parents=True, exist_ok=True)
    else:
        full.mkdir(parents=True, exist_ok=True)
    return full


def load_manifest(path: Path, root: Path) -> Manifest:
    manifest_path = require_under_legacy_root(path, root, allow_file=True)
    try:
        data = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValidationError("manifest", f"failed to read or parse manifest: {exc}") from exc
    if not isinstance(data, dict):
        raise ValidationError("manifest", "manifest root must be an object")
    raw_samples = data.get("samples")
    if not isinstance(raw_samples, list) or not raw_samples:
        raise ValidationError("manifest", "manifest must contain a non-empty samples array")

    samples: list[Sample] = []
    labels: set[str] = set()
    for raw in raw_samples:
        if not isinstance(raw, dict):
            raise ValidationError("manifest", "sample entries must be objects")
        label = read_label(raw.get("label"))
        if label in labels:
            raise ValidationError("manifest", "sample labels must be unique")
        labels.add(label)
        raw_path = raw.get("path")
        if not isinstance(raw_path, str) or not raw_path.strip():
            raise ValidationError("manifest", "sample path is required")
        kind = raw.get("kind")
        if not isinstance(kind, str) or not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,63}", kind):
            raise ValidationError("manifest", "sample kind must be a safe lowercase token")
        timeout_seconds = int(raw.get("timeoutSeconds", DEFAULT_TIMEOUT_SECONDS))
        max_artifact_bytes = int(raw.get("maxArtifactBytes", DEFAULT_MAX_ARTIFACT_BYTES))
        if timeout_seconds <= 0 or max_artifact_bytes <= 0:
            raise ValidationError("manifest", "bounds must be positive integers")
        samples.append(Sample(label, Path(raw_path).expanduser(), kind, timeout_seconds, max_artifact_bytes))

    fragments = data.get("privateNameFragments", [])
    if fragments is None:
        fragments = []
    if not isinstance(fragments, list) or any(not isinstance(item, str) for item in fragments):
        raise ValidationError("manifest", "privateNameFragments must be an array of strings")

    return Manifest(tuple(samples), tuple(item for item in fragments if item))


def read_label(value: Any) -> str:
    if not isinstance(value, str) or not re.fullmatch(r"[a-z0-9][a-z0-9-]{2,63}", value):
        raise ValidationError("manifest", "sample label must be a safe neutral token")
    return value


def ensure_no_tracked_legacy_files(root: Path) -> None:
    result = subprocess.run(
        ["git", "ls-files", "--", LEGACY_ROOT.as_posix()],
        cwd=root,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode != 0:
        raise ValidationError("git", "unable to inspect tracked legacy validation files")
    if result.stdout.strip():
        raise ValidationError("tracked-tmp", ".tmp/legacy-codebase-validation contains tracked files")


def run_scan(sample: Sample, output_dir: Path, root: Path, dry_run: bool = False) -> tuple[str, int | None, float, list[str]]:
    sample_out = output_dir / sample.label
    if sample_out.exists():
        shutil.rmtree(sample_out)
    sample_out.mkdir(parents=True, exist_ok=True)

    limitations: list[str] = []
    if dry_run:
        return "dry-run", 0, 0.0, ["dry-run: scan not executed"]

    if not sample.path.exists():
        return "deferred", None, 0.0, ["sample path unavailable"]

    command = [
        "dotnet",
        "run",
        "--project",
        str(root / "src/dotnet/TraceMap.Cli"),
        "--",
        "scan",
        "--repo",
        str(sample.path),
        "--out",
        str(sample_out),
    ]

    start = time.monotonic()
    try:
        completed = subprocess.run(
            command,
            cwd=root,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=sample.timeout_seconds,
            check=False,
        )
        duration = time.monotonic() - start
        write_process_log(sample_out, completed.stdout, completed.stderr)
        if completed.returncode == 0:
            return "completed", completed.returncode, duration, limitations
        limitations.append("scan exited non-zero; summary uses available artifacts")
        return "failed", completed.returncode, duration, limitations
    except subprocess.TimeoutExpired as exc:
        duration = time.monotonic() - start
        write_process_log(sample_out, exc.stdout or "", exc.stderr or "")
        limitations.append("timeout exceeded; result truncated/deferred")
        return "timeout", None, duration, limitations


def write_process_log(sample_out: Path, stdout: str | bytes | None, stderr: str | bytes | None) -> None:
    logs = sample_out / "logs"
    logs.mkdir(parents=True, exist_ok=True)
    text_stdout = stdout.decode("utf-8", errors="replace") if isinstance(stdout, bytes) else (stdout or "")
    text_stderr = stderr.decode("utf-8", errors="replace") if isinstance(stderr, bytes) else (stderr or "")
    (logs / "validation-process.log").write_text(
        "stdout:\n" + text_stdout + "\n\nstderr:\n" + text_stderr,
        encoding="utf-8",
    )


def summarize_sample(sample: Sample, output_dir: Path, status: str, exit_code: int | None, duration: float, limitations: list[str]) -> SampleSummary:
    sample_out = output_dir / sample.label
    artifacts = {
        "scan-manifest.json": (sample_out / "scan-manifest.json").is_file(),
        "facts.ndjson": (sample_out / "facts.ndjson").is_file(),
        "index.sqlite": (sample_out / "index.sqlite").is_file(),
        "report.md": (sample_out / "report.md").is_file(),
        "logs/analyzer.log": (sample_out / "logs/analyzer.log").is_file(),
    }
    output_size = directory_size(sample_out)
    truncated = status == "timeout" or output_size > sample.max_artifact_bytes
    deferred = status in {"deferred", "dry-run"}
    if output_size > sample.max_artifact_bytes:
        limitations.append("artifact size exceeded configured bound")

    if artifacts["index.sqlite"]:
        sql_counts = read_sqlite_counts(sample_out / "index.sqlite")
    else:
        sql_counts = {}
    parse_facts = not truncated or not sql_counts
    if not parse_facts:
        limitations.append("facts.ndjson parsing skipped because artifact size exceeded configured bound and SQLite counts were available")

    manifest = read_json_file(sample_out / "scan-manifest.json")
    facts = read_facts(sample_out / "facts.ndjson") if parse_facts else []

    gap_count = int(sql_counts.get("AnalysisGap", 0)) if sql_counts else sum(1 for fact in facts if fact.get("factType") == "AnalysisGap")
    fact_count = int(sql_counts.get("__facts__", 0)) if sql_counts else len(facts)
    coverage = string_or_unknown(manifest.get("analysisLevel"))
    build_status = string_or_unknown(manifest.get("buildStatus"))
    if build_status != "Succeeded" and build_status != "unknown":
        limitations.append("build/project load did not fully succeed; coverage is reduced")

    return SampleSummary(
        label=sample.label,
        kind=sample.kind,
        status=status,
        exit_code=exit_code,
        duration_seconds=round(duration, 3),
        output_size_bytes=output_size,
        truncated=truncated,
        deferred=deferred,
        artifacts=artifacts,
        facts_count=fact_count,
        analysis_gap_count=gap_count,
        coverage_label=coverage,
        build_status=build_status,
        commit_sha_present=bool(manifest.get("commitSha")),
        legacy_indicators=collect_legacy_indicators(sample.path),
        ui_event_probe=probe_ui_events(facts, enabled=sample.kind in {"legacy-ui", "unknown-legacy"}),
        wcf_fact_counts=collect_wcf_counts(sql_counts, facts),
        limitations=sorted(set(limitations)),
    )


def read_json_file(path: Path) -> dict[str, Any]:
    if not path.is_file():
        return {}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        return data if isinstance(data, dict) else {}
    except json.JSONDecodeError:
        return {}


def read_facts(path: Path) -> list[dict[str, Any]]:
    if not path.is_file():
        return []
    facts: list[dict[str, Any]] = []
    try:
        with path.open("r", encoding="utf-8", errors="replace") as handle:
            for line in handle:
                if not line.strip():
                    continue
                try:
                    value = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if isinstance(value, dict):
                    facts.append(value)
    except OSError:
        return []
    return facts


def read_sqlite_counts(path: Path) -> dict[str, int]:
    counts: dict[str, int] = {}
    try:
        with sqlite3.connect(path) as connection:
            counts["__facts__"] = int(connection.execute("select count(*) from facts").fetchone()[0])
            for fact_type, count in connection.execute("select fact_type, count(*) from facts group by fact_type"):
                counts[str(fact_type)] = int(count)
    except sqlite3.Error:
        return {}
    return counts


def directory_size(path: Path) -> int:
    if not path.exists():
        return 0
    total = 0
    for item in path.rglob("*"):
        if item.is_file():
            try:
                total += item.stat().st_size
            except OSError:
                continue
    return total


def collect_legacy_indicators(repo: Path) -> dict[str, Any]:
    indicators: dict[str, Any] = {
        "targetFrameworks": [],
        "toolsVersions": [],
        "packagesConfigCount": 0,
        "bindingRedirectCount": 0,
        "oldStyleProjectCount": 0,
        "projectFileCount": 0,
    }
    if not repo.exists():
        return indicators

    frameworks: set[str] = set()
    tools_versions: set[str] = set()
    for project in sorted(repo.rglob("*.*proj")):
        indicators["projectFileCount"] += 1
        text = safe_read_text(project)
        if "<Project Sdk=" not in text:
            indicators["oldStyleProjectCount"] += 1
        for match in re.finditer(r"<TargetFrameworks?>([^<]+)</TargetFrameworks?>", text, flags=re.IGNORECASE):
            for framework in re.split(r"[;,]", match.group(1)):
                if framework.strip():
                    frameworks.add(framework.strip())
        for match in re.finditer(r'ToolsVersion\s*=\s*"([^"]+)"', text, flags=re.IGNORECASE):
            tools_versions.add(match.group(1).strip())

    indicators["packagesConfigCount"] = sum(1 for _ in repo.rglob("packages.config"))
    for config in list(repo.rglob("*.config")) + list(repo.rglob("*.xml")):
        text = safe_read_text(config)
        indicators["bindingRedirectCount"] += len(re.findall(r"<bindingRedirect\b", text, flags=re.IGNORECASE))
    indicators["targetFrameworks"] = sorted(frameworks)
    indicators["toolsVersions"] = sorted(tools_versions)
    return indicators


def safe_read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return ""


def probe_ui_events(facts: list[dict[str, Any]], enabled: bool = True) -> dict[str, Any]:
    if not enabled:
        return {
            "classification": "not-applicable",
            "semanticMatches": 0,
            "structuralMatches": 0,
            "syntaxOrTextMatches": 0,
            "downstreamEvidenceMatches": 0,
            "ruleIds": [],
            "evidenceTiers": [],
            "factTypes": [],
            "limitations": [
                "UI event probe is only run for legacy UI or unknown legacy sample kinds.",
            ],
        }

    precise_ui_facts = [
        fact
        for fact in facts
        if str(fact.get("factType", "")).startswith("WebForms")
        or str(fact.get("factType", "")).startswith("WinForms")
    ]
    if precise_ui_facts:
        tiers = sorted({str(fact.get("evidenceTier", "Tier4Unknown")) for fact in precise_ui_facts})
        rule_ids = sorted({str(fact.get("ruleId", "unknown")) for fact in precise_ui_facts})
        fact_types = sorted({str(fact.get("factType", "unknown")) for fact in precise_ui_facts})
        classification = (
            "semantic-static-wiring"
            if any(str(fact.get("factType")) in {"WebFormsHandlerResolved", "WinFormsHandlerResolved"} and str(fact.get("evidenceTier")) == "Tier1Semantic" for fact in precise_ui_facts)
            else "structural-static-wiring"
            if any(str(fact.get("factType")) in {"WebFormsEventBindingDeclared", "WebFormsHandlerResolved", "WinFormsEventBindingDeclared", "WinFormsHandlerResolved"} for fact in precise_ui_facts)
            else "syntax-or-text-static-wiring"
        )
        return {
            "classification": classification,
            "semanticMatches": sum(1 for fact in precise_ui_facts if str(fact.get("evidenceTier")) == "Tier1Semantic"),
            "structuralMatches": sum(1 for fact in precise_ui_facts if str(fact.get("evidenceTier")) == "Tier2Structural"),
            "syntaxOrTextMatches": sum(1 for fact in precise_ui_facts if str(fact.get("evidenceTier")) == "Tier3SyntaxOrTextual"),
            "downstreamEvidenceMatches": sum(1 for fact in precise_ui_facts if str(fact.get("factType")) in {"WebFormsEventFlowProjected", "WinFormsHandlerFlowProjected"}),
            "ruleIds": rule_ids,
            "evidenceTiers": tiers,
            "factTypes": fact_types,
            "limitations": [
                "Precise WebForms and WinForms evidence supersedes the coarse legacy UI token probe for this sample.",
                "Static WebForms and WinForms wiring and flow evidence does not prove runtime execution.",
                "No match is a scanner evidence gap, not proof of absence.",
            ],
        }

    semantic = 0
    structural = 0
    syntax_text = 0
    rule_ids: set[str] = set()
    tiers: set[str] = set()
    matched_fact_types: set[str] = set()
    downstream_edges = 0

    for fact in facts:
        haystack = safe_fact_haystack(fact)
        if not has_ui_event_token(haystack):
            continue
        fact_type = str(fact.get("factType", "unknown"))
        tier = str(fact.get("evidenceTier", "Tier4Unknown"))
        rule_id = str(fact.get("ruleId", "unknown"))
        rule_ids.add(rule_id)
        tiers.add(tier)
        matched_fact_types.add(fact_type)
        if tier == "Tier1Semantic":
            semantic += 1
        elif tier == "Tier2Structural":
            structural += 1
        else:
            syntax_text += 1
        if fact_type in {"CallEdge", "MethodInvoked", "DependencyResolved", "CallbackBoundary"}:
            downstream_edges += 1

    if semantic:
        classification = "semantic-static-wiring"
    elif structural:
        classification = "structural-static-wiring"
    elif syntax_text:
        classification = "syntax-or-text-static-wiring"
    else:
        classification = "gap-no-current-event-evidence"

    return {
        "classification": classification,
        "semanticMatches": semantic,
        "structuralMatches": structural,
        "syntaxOrTextMatches": syntax_text,
        "downstreamEvidenceMatches": downstream_edges,
        "ruleIds": sorted(rule_ids),
        "evidenceTiers": sorted(tiers),
        "factTypes": sorted(matched_fact_types),
        "limitations": [
            "Static wiring evidence does not prove runtime execution.",
            "No match is a scanner evidence gap, not proof of absence.",
        ],
    }


def collect_wcf_counts(sql_counts: dict[str, int], facts: list[dict[str, Any]]) -> dict[str, int]:
    fact_types = [
        "WcfClientEndpointDeclared",
        "WcfServiceEndpointDeclared",
        "WcfServiceContractDeclared",
        "WcfOperationContractDeclared",
        "WcfGeneratedClientDeclared",
        "WcfServiceHostDeclared",
        "WcfServiceReferenceMetadataDeclared",
        "WcfMetadataOperationDeclared",
        "WcfServiceReferenceMapping",
    ]
    gap_classifications = [
        "AmbiguousWcfNormalizedMapping",
        "AmbiguousWcfMetadataContractMapping",
        "MissingLocalWcfMetadata",
        "MalformedWcfMetadata",
        "UnlinkedWcfMetadata",
    ]
    if sql_counts:
        counts = {fact_type: int(sql_counts.get(fact_type, 0)) for fact_type in fact_types}
        counts.update({f"AnalysisGap:{classification}": 0 for classification in gap_classifications})
        return counts

    counts = {fact_type: 0 for fact_type in fact_types}
    counts.update({f"AnalysisGap:{classification}": 0 for classification in gap_classifications})
    for fact in facts:
        fact_type = fact.get("factType")
        if isinstance(fact_type, str) and fact_type in counts:
            counts[fact_type] += 1
        if fact_type == "AnalysisGap":
            properties = fact.get("properties")
            classification = properties.get("classification") if isinstance(properties, dict) else None
            if isinstance(classification, str) and f"AnalysisGap:{classification}" in counts:
                counts[f"AnalysisGap:{classification}"] += 1
    return counts


def safe_fact_haystack(fact: dict[str, Any]) -> str:
    values: list[str] = []
    for key in ("factType", "ruleId", "evidenceTier", "sourceSymbol", "targetSymbol", "contractElement"):
        value = fact.get(key)
        if isinstance(value, str):
            values.append(value)
    evidence = fact.get("evidence")
    if isinstance(evidence, dict):
        file_path = evidence.get("filePath")
        if isinstance(file_path, str):
            values.append(Path(file_path).name)
    properties = fact.get("properties")
    if isinstance(properties, dict):
        for key, value in sorted(properties.items()):
            if isinstance(key, str):
                values.append(key)
            if isinstance(value, str):
                values.append(value)
    return "\n".join(values)


def has_ui_event_token(text: str) -> bool:
    lowered = text.lower()
    return any(token in lowered for token in ("click", "onclick", "initializecomponent", "eventsubscription", "handler"))


def string_or_unknown(value: Any) -> str:
    return value if isinstance(value, str) and value else "unknown"


def build_summary(manifest: Manifest, summaries: list[SampleSummary]) -> dict[str, Any]:
    return {
        "schemaVersion": 1,
        "ruleId": SUMMARY_RULE_ID,
        "publicClaimLevel": "hidden",
        "samples": [sample_to_json(summary) for summary in sorted(summaries, key=lambda item: item.label)],
        "limitations": [
            "Local raw artifacts remain under ignored .tmp/legacy-codebase-validation/.",
            "Sample identity is label-only; repository paths and remotes are intentionally omitted.",
            "Legacy environment indicators are evidence clues, not guaranteed remediation instructions.",
            "UI event evidence is static and does not prove runtime execution.",
        ],
        "prePublishChecklist": {
            "labelsOnlySampleIdentity": True,
            "noAbsolutePaths": True,
            "noRawRemotes": True,
            "noRawSql": True,
            "noConfigValues": True,
            "noSecrets": True,
            "noSnippets": True,
            "countsTiersCoverageRulesLimitationsVisible": True,
        },
        "privateNameFragmentsConfigured": len(manifest.private_name_fragments),
    }


def sample_to_json(summary: SampleSummary) -> dict[str, Any]:
    return {
        "label": summary.label,
        "kind": summary.kind,
        "status": summary.status,
        "exitCode": summary.exit_code,
        "durationSeconds": summary.duration_seconds,
        "outputSizeBytes": summary.output_size_bytes,
        "truncated": summary.truncated,
        "deferred": summary.deferred,
        "artifacts": dict(sorted(summary.artifacts.items())),
        "factsCount": summary.facts_count,
        "analysisGapCount": summary.analysis_gap_count,
        "coverageLabel": summary.coverage_label,
        "buildStatus": summary.build_status,
        "commitShaPresent": summary.commit_sha_present,
        "legacyIndicators": summary.legacy_indicators,
        "uiEventProbe": summary.ui_event_probe,
        "wcfFactCounts": dict(sorted(summary.wcf_fact_counts.items())),
        "ruleIds": sample_rule_ids(summary),
        "limitations": sorted(set(summary.limitations)),
    }


def sample_rule_ids(summary: SampleSummary) -> list[str]:
    rule_ids = [ENV_RULE_ID, UI_RULE_ID, BOUNDS_RULE_ID]
    if any(value > 0 for value in summary.wcf_fact_counts.values()):
        rule_ids.extend(WCF_RULE_IDS)
    return rule_ids


def write_summary(summary: dict[str, Any], summary_dir: Path, manifest: Manifest) -> None:
    summary_dir.mkdir(parents=True, exist_ok=True)
    json_path = summary_dir / "legacy-validation-summary.json"
    md_path = summary_dir / "legacy-validation-summary.md"
    json_text = json.dumps(summary, indent=2, sort_keys=True) + "\n"
    md_text = render_markdown(summary)

    failures = redact_failures(json_text + "\n" + md_text, manifest.private_name_fragments)
    if failures:
        categories = ", ".join(sorted({failure.category for failure in failures}))
        raise ValidationError("redaction", f"redaction failed: {categories}")

    json_path.write_text(json_text, encoding="utf-8")
    md_path.write_text(md_text, encoding="utf-8")


def render_markdown(summary: dict[str, Any]) -> str:
    lines = [
        "# Legacy Codebase Validation Summary",
        "",
        f"Rule ID: `{summary['ruleId']}`",
        "",
        "Public claim level: `hidden`",
        "",
        "Raw artifacts remain local under ignored `.tmp/legacy-codebase-validation/`.",
        "",
        "## Samples",
        "",
        "| Label | Kind | Status | Coverage | Build | Facts | Gaps | UI event evidence | Limitations |",
        "| --- | --- | --- | --- | --- | ---: | ---: | --- | --- |",
    ]
    for sample in summary["samples"]:
        ui = sample["uiEventProbe"]["classification"]
        limitations = "; ".join(sample["limitations"]) if sample["limitations"] else "none"
        lines.append(
            "| "
            + " | ".join(
                [
                    cell(sample["label"]),
                    cell(sample["kind"]),
                    cell(sample["status"]),
                    cell(sample["coverageLabel"]),
                    cell(sample["buildStatus"]),
                    str(sample["factsCount"]),
                    str(sample["analysisGapCount"]),
                    cell(ui),
                    cell(limitations),
                ]
            )
            + " |"
        )
    lines.extend(
        [
            "",
            "## Limitations",
            "",
        ]
    )
    for limitation in summary["limitations"]:
        lines.append(f"- {limitation}")
    lines.extend(
        [
            "",
            "## Pre-Publish Checklist",
            "",
        ]
    )
    for key, value in summary["prePublishChecklist"].items():
        lines.append(f"- `{key}`: `{str(value).lower()}`")
    return "\n".join(lines) + "\n"


def cell(value: Any) -> str:
    text = str(value).replace("|", "\\|").replace("\n", " ")
    return text


def redact_failures(text: str, private_fragments: tuple[str, ...] = ()) -> list[RedactionFailure]:
    failures: list[RedactionFailure] = []
    checks = [
        ("absolute-path", r"(?<![A-Za-z0-9_])/(?:Users|home|var|private|tmp|Volumes)/[^\s`\"')]+"),
        ("absolute-path", r"[A-Za-z]:\\[^\s`\"')]+"),
        ("raw-remote", r"\b(?:https?|ssh)://[^\s`\"')]+"),
        ("raw-remote", r"\bgit@[A-Za-z0-9_.-]+:[^\s`\"')]+"),
        ("raw-sql", r"(?m)\bselect\s+[^\n;]+\bfrom\b|\binsert\s+into\b|\bupdate\s+\w+\s+set\b|\bdelete\s+from\b"),
        ("connection-string", r"\b(?:Server|Data Source|User ID|Password|Initial Catalog)\s*="),
        ("config-value", r"\b(?:ApiKey|ClientSecret|SecretKey|AccessToken)\s*[:=]\s*[^\s`\"')]+"),
        ("secret", r"\b(?:AKIA[0-9A-Z]{16}|sk-[A-Za-z0-9_-]{20,}|ghp_[A-Za-z0-9_]{20,})\b"),
        ("snippet", r"\b(?:public|protected|internal)\s+(?:(?:sealed|static|abstract|partial|async|override)\s+)*(?:class|interface|enum|struct|void|Task|IEnumerable|IList|List|string|int|bool|decimal)\b"),
    ]
    for category, pattern in checks:
        if re.search(pattern, text, flags=re.IGNORECASE | re.DOTALL):
            failures.append(RedactionFailure(category))
    for fragment in private_fragments:
        if fragment and fragment in text:
            failures.append(RedactionFailure("private-name"))
    return failures


def validate_output_path(output_arg: Path, root: Path) -> Path:
    return require_under_legacy_root(output_arg, root, allow_file=False)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Run local-only TraceMap validation against legacy codebases.")
    parser.add_argument("manifest", type=Path)
    parser.add_argument("out", type=Path)
    parser.add_argument("--dry-run", action="store_true", help="Validate manifest and write bounded placeholder summaries without scanning.")
    args = parser.parse_args(argv)

    root = repo_root()
    try:
        ensure_no_tracked_legacy_files(root)
        manifest = load_manifest(args.manifest, root)
        output_dir = validate_output_path(args.out, root)
        summaries: list[SampleSummary] = []
        for sample in manifest.samples:
            status, exit_code, duration, limitations = run_scan(sample, output_dir, root, dry_run=args.dry_run)
            summaries.append(summarize_sample(sample, output_dir, status, exit_code, duration, limitations))
        summary = build_summary(manifest, summaries)
        summary_dir = (root / LEGACY_ROOT / "summary").resolve()
        write_summary(summary, summary_dir, manifest)
        print(f"Legacy validation summary written: {as_repo_relative(summary_dir, root)}")
        return 0
    except ValidationError as exc:
        print(f"legacy validation failed: {exc.category}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
