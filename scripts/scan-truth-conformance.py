#!/usr/bin/env python3
"""Run the bounded synthetic TraceMap cross-adapter scan-truth matrix."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import re
import shutil
import sqlite3
import subprocess
import sys
import tempfile
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from typing import Any


PROFILE_RULE = "adapter.scan-truth.conformance.v1"
CAPABILITIES = (
    "concrete-git-authority",
    "actual-analyzed-byte-identity",
    "repeat-determinism",
    "same-size-dirty-mutation",
    "inaccessible-input-truth",
    "source-mutation-detection",
    "filesystem-correct-exclusion",
    "reduced-analysis-preservation",
    "required-artifact-transaction",
    "ndjson-sqlite-roundtrip",
    "malformed-schema-fail-closed",
    "repository-relative-evidence",
)
ALL_ADAPTERS = ("dotnet", "jvm", "python", "typescript", "swift")


def root() -> Path:
    return Path(__file__).resolve().parent.parent


def load_validator():
    path = Path(__file__).with_name("validate-adapter-artifacts.py")
    spec = importlib.util.spec_from_file_location("validate_adapter_artifacts", path)
    if spec is None or spec.loader is None:
        raise RuntimeError("adapter validator unavailable")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


VALIDATOR = load_validator()


@dataclass(frozen=True)
class AdapterDefinition:
    name: str
    relative_source: str
    original_source: str
    changed_source: str
    additional_files: dict[str, str]


DEFINITIONS = {
    "dotnet": AdapterDefinition(
        "dotnet", "Sample.cs", "public sealed class Sample { public int Value => 1; }\n",
        "public sealed class Sample { public int Value => 2; }\n",
        {"Sample.csproj": '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n'},
    ),
    "jvm": AdapterDefinition(
        "jvm", "src/main/java/example/Sample.java", "package example; final class Sample { int value = 1; }\n",
        "package example; final class Sample { int value = 2; }\n",
        {"pom.xml": '<project><modelVersion>4.0.0</modelVersion><groupId>example</groupId><artifactId>fixture</artifactId><version>1</version></project>\n'},
    ),
    "python": AdapterDefinition(
        "python", "app.py", "value = 1\n", "value = 2\n",
        {"pyproject.toml": '[project]\nname = "scan-truth-fixture"\nversion = "0.0.0"\n'},
    ),
    "typescript": AdapterDefinition(
        "typescript", "src/sample.ts", "export const value = 1;\n", "export const value = 2;\n",
        {"tsconfig.json": '{"compilerOptions":{"target":"ES2022","module":"CommonJS","strict":true},"include":["src/**/*.ts"]}\n'},
    ),
    "swift": AdapterDefinition(
        "swift", "Sources/App/main.swift", 'print("hello")\n', 'print("jello")\n',
        {"Package.swift": '// swift-tools-version: 6.0\nimport PackageDescription\nlet package = Package(name: "Fixture", targets: [.executableTarget(name: "App")])\n'},
    ),
}


def run(command: list[str], *, cwd: Path, env: dict[str, str] | None = None, allow_failure: bool = False) -> subprocess.CompletedProcess[str]:
    completed = subprocess.run(command, cwd=cwd, env=env, text=True, capture_output=True, check=False)
    if completed.returncode and not allow_failure:
        label = command[0] if command else "command"
        raise RuntimeError(f"{label} failed with exit {completed.returncode}")
    return completed


def git(repo: Path, *arguments: str) -> str:
    return run(["git", *arguments], cwd=repo).stdout.strip()


def create_fixture(parent: Path, definition: AdapterDefinition) -> Path:
    repo = parent / f"fixture-{definition.name}"
    for relative, content in {definition.relative_source: definition.original_source, **definition.additional_files}.items():
        path = repo / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
    git(repo, "-c", "init.defaultObjectFormat=sha1", "init", "--object-format=sha1")
    git(repo, "config", "user.email", "fixture@example.invalid")
    git(repo, "config", "user.name", "TraceMap Fixture")
    git(repo, "add", ".")
    env = dict(os.environ, GIT_AUTHOR_DATE="2026-01-01T00:00:00Z", GIT_COMMITTER_DATE="2026-01-01T00:00:00Z")
    completed = subprocess.run(["git", "commit", "-m", "baseline"], cwd=repo, env=env, text=True, capture_output=True)
    if completed.returncode:
        raise RuntimeError("synthetic git commit failed")
    return repo


def prepare(adapter: str, repo_root: Path) -> None:
    if adapter == "dotnet":
        run(["dotnet", "build", "src/dotnet/TraceMap.sln"], cwd=repo_root)
        run(["dotnet", "test", "src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj", "--no-build", "--filter", "FullyQualifiedName~ScanEngineTests|FullyQualifiedName~ScanOutputTransactionTests"], cwd=repo_root)
    elif adapter == "jvm":
        run(["gradle", "-p", "src/jvm", "installDist"], cwd=repo_root, env=java_environment())
        run(["gradle", "-p", "src/jvm", "test", "--tests", "*ScanMutationTruthTest"], cwd=repo_root, env=java_environment())
    elif adapter == "python":
        run([sys.executable, "-m", "pytest", "-q", "src/python/tests/test_python_adapter.py", "-k", "source_mutation_before_snapshot_verification or artifact_write_failure_preserves"], cwd=repo_root)
    elif adapter == "typescript":
        run(["npm", "ci"], cwd=repo_root / "src/typescript")
        run(["npm", "run", "build"], cwd=repo_root / "src/typescript")
        run(["npm", "test", "--", "--run", "tests/ScanEngine.test.ts", "-t", "fails before publishing|preserves prior output"], cwd=repo_root / "src/typescript")
    elif adapter == "swift":
        run(["swift", "run", "--package-path", "src/swift", "tracemap-swift-smoke-tests"], cwd=repo_root)
        run(["swift", "build", "--package-path", "src/swift", "--product", "tracemap-swift"], cwd=repo_root)


def java_environment() -> dict[str, str]:
    env = dict(os.environ)
    candidates = [
        Path(env["JAVA_HOME"]) if env.get("JAVA_HOME") else None,
        Path("/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home"),
        Path("/usr/local/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home"),
    ]
    for candidate in candidates:
        if candidate is None or not (candidate / "bin/java").is_file():
            continue
        selected = dict(env, JAVA_HOME=str(candidate))
        version = run([str(candidate / "bin/java"), "-version"], cwd=root(), env=selected, allow_failure=True)
        if version.returncode == 0 and re.search(r'\bversion "21(?:\.|\")', version.stderr + version.stdout):
            return selected
    raise RuntimeError("Java21Unavailable")


def scan_command(adapter: str, repo: Path, output: Path, repo_root: Path, extra: list[str] | None = None) -> tuple[list[str], dict[str, str] | None]:
    extra = extra or []
    if adapter == "dotnet":
        return (["dotnet", "run", "--no-build", "--project", "src/dotnet/TraceMap.Cli", "--", "scan", "--repo", str(repo), "--out", str(output), *extra], None)
    if adapter == "jvm":
        return ([str(repo_root / "src/jvm/build/install/tracemap-jvm/bin/tracemap-jvm"), "scan", "--repo", str(repo), "--out", str(output), *extra], java_environment())
    if adapter == "python":
        env = dict(os.environ)
        env["PYTHONPATH"] = str(repo_root / "src/python")
        return ([sys.executable, "-m", "tracemap_py.cli", "scan", "--repo", str(repo), "--out", str(output), *extra], env)
    if adapter == "typescript":
        return (["node", str(repo_root / "src/typescript/dist/src/cli.js"), "scan", "--repo", str(repo), "--out", str(output), *extra], None)
    if adapter == "swift":
        return (["swift", "run", "--skip-build", "--package-path", str(repo_root / "src/swift"), "tracemap-swift", "scan", "--repo", str(repo), "--out", str(output), *extra], None)
    raise ValueError(adapter)


def normalized_manifest(output: Path) -> dict[str, Any]:
    value = json.loads((output / "scan-manifest.json").read_text(encoding="utf-8"))
    value.pop("scannedAt", None)
    return value


def logical_sqlite_facts(output: Path) -> list[tuple[Any, ...]]:
    connection = sqlite3.connect(f"{(output / 'index.sqlite').resolve().as_uri()}?mode=ro", uri=True)
    try:
        return connection.execute(
            "select fact_id, scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier, "
            "source_symbol, target_symbol, contract_element, file_path, start_line, end_line, snippet_hash, "
            "extractor_id, extractor_version, properties_json from facts order by fact_id"
        ).fetchall()
    finally:
        connection.close()


def facts(output: Path) -> list[dict[str, Any]]:
    return [json.loads(line) for line in (output / "facts.ndjson").read_text(encoding="utf-8").splitlines() if line.strip()]


def artifact_hashes(output: Path) -> dict[str, str]:
    result: dict[str, str] = {}
    for relative in json.loads((root() / "contracts/scan-truth-conformance.v1.json").read_text(encoding="utf-8"))["requiredArtifacts"]:
        path = output / relative
        if path.is_file():
            result[relative] = hashlib.sha256(path.read_bytes()).hexdigest()
    return result


def invalid_source(adapter: str) -> str:
    return {
        "dotnet": "public sealed class Broken { public void Run( { }\n",
        "jvm": "package example; final class Broken { void run( { }\n",
        "python": "def broken(:\n    pass\n",
        "typescript": "export const = ;\n",
        "swift": "func broken( {\n",
    }[adapter]


def unicode_exclusion_fixture(adapter: str) -> tuple[str, str, str]:
    extension = {"dotnet": "cs", "jvm": "java", "python": "py", "typescript": "ts", "swift": "swift"}[adapter]
    relative_nfd = f"Generated/Cafe\u0301.{extension}"
    pattern_nfc = f"Generated/Café.{extension}"
    content = {
        "dotnet": "internal sealed class MustRemainExcluded { }\n",
        "jvm": "final class MustRemainExcluded { }\n",
        "python": "must_remain_excluded = True\n",
        "typescript": "export const mustRemainExcluded = true;\n",
        "swift": "let mustRemainExcluded = true\n",
    }[adapter]
    return relative_nfd, pattern_nfc, content


def host_treats_unicode_names_as_equivalent(parent: Path) -> bool:
    probe = parent / "unicode-probe"
    probe.mkdir(parents=True)
    nfd = probe / "Cafe\u0301.txt"
    nfd.write_text("probe", encoding="utf-8")
    nfc = probe / "Café.txt"
    try:
        return nfc.exists() and os.path.samefile(nfd, nfc)
    finally:
        shutil.rmtree(probe)


def capability(name: str, status: str, evidence: list[str], limitations: list[str] | None = None) -> dict[str, Any]:
    return {
        "capability": name,
        "status": status,
        "ruleId": PROFILE_RULE,
        "evidence": evidence,
        "limitations": limitations or [],
    }


def render_markdown(result: dict[str, Any]) -> str:
    lines = [
        "# TraceMap Cross-Adapter Scan-Truth Readiness",
        "",
        f"- Profile: `{result['profileId']}`",
        f"- Overall status: `{result['overallStatus']}`",
        f"- Rule ID: `{PROFILE_RULE}`",
        "",
        "| Adapter | Status | Unsupported / not-run capabilities |",
        "| --- | --- | --- |",
    ]
    for adapter in result["adapters"]:
        incomplete = [
            row["capability"]
            for row in adapter["capabilities"]
            if row["status"] not in {"supported", "not-applicable"}
        ]
        lines.append(f"| {adapter['adapter']} | {adapter['status']} | {', '.join(incomplete) if incomplete else 'none'} |")
    lines.extend(["", "## Capability evidence", ""])
    for adapter in result["adapters"]:
        lines.extend([f"### {adapter['adapter']}", "", "| Capability | Status | Evidence |", "| --- | --- | --- |"])
        for row in adapter["capabilities"]:
            evidence = ", ".join(row["evidence"]) if row["evidence"] else "none"
            lines.append(f"| {row['capability']} | {row['status']} | {evidence} |")
        lines.append("")
    lines.extend(["## Limitations", ""])
    lines.extend(f"- {item}" for item in result["limitations"])
    lines.append("")
    return "\n".join(lines)


def evaluate_adapter(adapter: str, workspace: Path, repo_root: Path, mutation_test_verified: bool) -> dict[str, Any]:
    definition = DEFINITIONS[adapter]
    repo = create_fixture(workspace, definition)
    fixture_commit = git(repo, "rev-parse", "HEAD")
    first, repeat, dirty = (workspace / f"{adapter}-{name}" for name in ("first", "repeat", "dirty"))
    command, env = scan_command(adapter, repo, first, repo_root)
    run(command, cwd=repo_root, env=env)
    command, env = scan_command(adapter, repo, repeat, repo_root)
    run(command, cwd=repo_root, env=env)
    source = repo / definition.relative_source
    if len(definition.original_source.encode()) != len(definition.changed_source.encode()):
        raise RuntimeError("dirty fixture mutation must preserve byte length")
    source.write_text(definition.changed_source, encoding="utf-8")
    command, env = scan_command(adapter, repo, dirty, repo_root)
    run(command, cwd=repo_root, env=env)

    validation_errors = {name: VALIDATOR.validate_output(path) for name, path in (("first", first), ("repeat", repeat), ("dirty", dirty))}
    first_manifest = normalized_manifest(first)
    repeat_manifest = normalized_manifest(repeat)
    dirty_manifest = normalized_manifest(dirty)
    deterministic = (
        first_manifest == repeat_manifest
        and (first / "facts.ndjson").read_bytes() == (repeat / "facts.ndjson").read_bytes()
        and (first / "report.md").read_bytes() == (repeat / "report.md").read_bytes()
        and (first / "logs/analyzer.log").read_bytes() == (repeat / "logs/analyzer.log").read_bytes()
        and logical_sqlite_facts(first) == logical_sqlite_facts(repeat)
    )
    dirty_identity = (
        first_manifest.get("commitSha") == dirty_manifest.get("commitSha")
        and first_manifest.get("sourceSnapshotDigest") != dirty_manifest.get("sourceSnapshotDigest")
        and first_manifest.get("scanId") != dirty_manifest.get("scanId")
    )
    valid = (
        not any(validation_errors.values())
        and first_manifest.get("commitSha") == fixture_commit
        and repeat_manifest.get("commitSha") == fixture_commit
        and dirty_manifest.get("commitSha") == fixture_commit
    )

    malformed = workspace / f"{adapter}-malformed"
    shutil.copytree(first, malformed)
    malformed_manifest_path = malformed / "scan-manifest.json"
    malformed_manifest = json.loads(malformed_manifest_path.read_text(encoding="utf-8"))
    malformed_manifest["sourceSnapshotDigest"] = "invalid"
    malformed_manifest_path.write_text(json.dumps(malformed_manifest), encoding="utf-8")
    malformed_rejected = any("sourceSnapshotDigest" in error for error in VALIDATOR.validate_output(malformed))

    reduced_repo = create_fixture(workspace / f"{adapter}-reduced-root", definition)
    (reduced_repo / definition.relative_source).write_text(invalid_source(adapter), encoding="utf-8")
    git(reduced_repo, "add", ".")
    git(reduced_repo, "commit", "-m", "invalid-source")
    reduced_output = workspace / f"{adapter}-reduced"
    command, env = scan_command(adapter, reduced_repo, reduced_output, repo_root)
    reduced_run = run(command, cwd=repo_root, env=env, allow_failure=True)
    reduced_valid = reduced_run.returncode == 0 and not VALIDATOR.validate_output(reduced_output)
    reduced_facts = facts(reduced_output) if reduced_valid else []
    reduced_manifest = normalized_manifest(reduced_output) if reduced_valid else {}
    reduced_preserved = (
        reduced_valid
        and any(row.get("factType") == "AnalysisGap" for row in reduced_facts)
        and any(row.get("factType") != "AnalysisGap" for row in reduced_facts)
        and (reduced_manifest.get("buildStatus") != "Succeeded" or reduced_manifest.get("analysisLevel") != "Level1SemanticAnalysis")
    )

    inaccessible_repo = create_fixture(workspace / f"{adapter}-inaccessible-root", definition)
    inaccessible_source = inaccessible_repo / definition.relative_source
    prior_output = workspace / f"{adapter}-inaccessible"
    shutil.copytree(first, prior_output)
    prior_hashes = artifact_hashes(prior_output)
    inaccessible_source.chmod(0)
    try:
        try:
            inaccessible_source.read_bytes()
            inaccessible_precondition = False
        except OSError:
            inaccessible_precondition = True
        if inaccessible_precondition:
            command, env = scan_command(adapter, inaccessible_repo, prior_output, repo_root)
            inaccessible_run = run(command, cwd=repo_root, env=env, allow_failure=True)
        else:
            inaccessible_run = None
    finally:
        inaccessible_source.chmod(0o600)
    if not inaccessible_precondition:
        inaccessible_truth = False
        transaction_truth = False
    elif inaccessible_run is not None and inaccessible_run.returncode != 0:
        inaccessible_truth = artifact_hashes(prior_output) == prior_hashes
        transaction_truth = inaccessible_truth
    else:
        inaccessible_errors = VALIDATOR.validate_output(prior_output)
        inaccessible_rows = facts(prior_output) if not inaccessible_errors else []
        inaccessible_manifest = normalized_manifest(prior_output) if not inaccessible_errors else {}
        inaccessible_truth = (
            not inaccessible_errors
            and any(row.get("factType") == "AnalysisGap" for row in inaccessible_rows)
            and (inaccessible_manifest.get("buildStatus") != "Succeeded" or inaccessible_manifest.get("analysisLevel") != "Level1SemanticAnalysis")
        )
        transaction_truth = not inaccessible_errors

    relative_nfd, pattern_nfc, excluded_content = unicode_exclusion_fixture(adapter)
    exclusion_repo = create_fixture(workspace / f"{adapter}-exclusion-root", definition)
    excluded_path = exclusion_repo / relative_nfd
    excluded_path.parent.mkdir(parents=True, exist_ok=True)
    excluded_path.write_text(excluded_content, encoding="utf-8")
    git(exclusion_repo, "add", ".")
    git(exclusion_repo, "commit", "-m", "unicode-exclusion")
    exclusion_output = workspace / f"{adapter}-exclusion"
    command, env = scan_command(adapter, exclusion_repo, exclusion_output, repo_root, ["--exclude", pattern_nfc])
    exclusion_run = run(command, cwd=repo_root, env=env, allow_failure=True)
    unicode_equivalent = host_treats_unicode_names_as_equivalent(workspace / f"{adapter}-unicode-probe-root")
    if exclusion_run.returncode == 0 and not VALIDATOR.validate_output(exclusion_output):
        excluded_observed = any(
            unicodedata.normalize("NFC", row.get("evidence", {}).get("filePath", ""))
            == unicodedata.normalize("NFC", relative_nfd)
            for row in facts(exclusion_output)
        )
        exclusion_truth = not excluded_observed if unicode_equivalent else True
        exclusion_status = "supported" if unicode_equivalent and exclusion_truth else "not-applicable" if not unicode_equivalent else "unsupported"
    else:
        exclusion_truth = False
        exclusion_status = "unsupported" if unicode_equivalent else "not-applicable"

    capabilities = [
        capability("concrete-git-authority", "supported" if valid else "unsupported", ["synthetic-exact-commit"]),
        capability("actual-analyzed-byte-identity", "supported" if valid else "unsupported", ["manifest-sourceSnapshotDigest"]),
        capability("repeat-determinism", "supported" if deterministic else "unsupported", ["baseline-repeat-comparison"]),
        capability("same-size-dirty-mutation", "supported" if dirty_identity else "unsupported", ["same-commit-different-byte-snapshot"]),
        capability(
            "inaccessible-input-truth",
            "supported" if inaccessible_truth else "not-applicable" if not inaccessible_precondition else "unsupported",
            ["chmod-unreadable-adversarial-scan"] if inaccessible_precondition else ["host-did-not-enforce-unreadable-precondition"],
            [] if inaccessible_precondition else ["The current host did not make the synthetic file unreadable for this process."],
        ),
        capability(
            "source-mutation-detection",
            "supported" if mutation_test_verified else "not-run",
            ["deterministic-before-verification-mutation-test"] if mutation_test_verified else ["mutation-test-skipped-by-option"],
            [] if mutation_test_verified else ["Run without --skip-build so the adapter mutation fixture is verified."],
        ),
        capability("filesystem-correct-exclusion", exclusion_status, ["NFC-pattern-NFD-path-host-semantics"] if unicode_equivalent else ["host-filesystem-distinguishes-normalization-forms"], [] if unicode_equivalent else ["Host filesystem keeps NFC and NFD names distinct."]),
        capability("reduced-analysis-preservation", "supported" if reduced_preserved else "unsupported", ["invalid-source-reduced-scan"]),
        capability(
            "required-artifact-transaction",
            "supported" if valid and transaction_truth and mutation_test_verified else "not-run" if not mutation_test_verified else "unsupported",
            ["five-artifact-validator", "failed-scan-prior-output-check", "deterministic-staged-write-failure-test"] if mutation_test_verified else ["transaction-test-skipped-by-option"],
            [] if mutation_test_verified else ["Run without --skip-build so the staged artifact failure fixture is verified."],
        ),
        capability("ndjson-sqlite-roundtrip", "supported" if valid else "unsupported", ["read-only-sqlite-roundtrip"]),
        capability("malformed-schema-fail-closed", "supported" if malformed_rejected else "unsupported", ["invalid-sourceSnapshotDigest-rejected"]),
        capability("repository-relative-evidence", "supported" if valid else "unsupported", ["private-path-and-relative-path-validator"]),
    ]
    status = "supported" if all(row["status"] in {"supported", "not-applicable"} for row in capabilities) else "unsupported"
    return {
        "adapter": adapter,
        "status": status,
        "capabilities": sorted(capabilities, key=lambda row: CAPABILITIES.index(row["capability"])),
        "limitations": sorted({error for errors in validation_errors.values() for error in errors}),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adapters", default=",".join(ALL_ADAPTERS), help="comma-separated adapter names")
    parser.add_argument("--out", type=Path, required=True, help="sanitized result JSON path")
    parser.add_argument("--markdown-out", type=Path, help="sanitized Markdown result path (defaults beside --out)")
    parser.add_argument("--skip-build", action="store_true", help="use already-built adapter binaries")
    parser.add_argument("--keep-work", type=Path, help="retain synthetic work at this explicit path")
    args = parser.parse_args()
    adapters = tuple(item.strip() for item in args.adapters.split(",") if item.strip())
    unknown = sorted(set(adapters) - set(ALL_ADAPTERS))
    if unknown:
        parser.error(f"unknown adapters: {', '.join(unknown)}")

    repo_root = root()
    temporary: tempfile.TemporaryDirectory[str] | None = None
    if args.keep_work:
        workspace = args.keep_work.resolve()
        if workspace.exists():
            raise SystemExit("--keep-work path must not already exist")
        workspace.mkdir(parents=True)
    else:
        temporary = tempfile.TemporaryDirectory(prefix="tracemap-scan-truth-")
        workspace = Path(temporary.name)
    try:
        rows = []
        selected_adapters = set(adapters)
        for adapter in ALL_ADAPTERS:
            if adapter not in selected_adapters:
                rows.append({
                    "adapter": adapter,
                    "status": "not-run",
                    "capabilities": [capability(name, "not-run", ["adapter-not-selected-by-invocation"], ["adapter-not-selected-by-invocation"]) for name in CAPABILITIES],
                    "limitations": ["adapter-not-selected-by-invocation"],
                })
                continue
            try:
                mutation_test_verified = not args.skip_build
                if not args.skip_build:
                    prepare(adapter, repo_root)
                rows.append(evaluate_adapter(adapter, workspace, repo_root, mutation_test_verified))
            except Exception as exception:
                rows.append({
                    "adapter": adapter,
                    "status": "not-run",
                    "capabilities": [capability(name, "not-run", [f"adapter-stage-failed:{exception.__class__.__name__}"], [exception.__class__.__name__]) for name in CAPABILITIES],
                    "limitations": [f"adapter-stage-failed:{exception.__class__.__name__}"],
                })
        overall = "supported" if all(row["status"] == "supported" for row in rows) else "unsupported"
        profile = json.loads((repo_root / "contracts/scan-truth-conformance.v1.json").read_text(encoding="utf-8"))
        result = {
            "schemaVersion": "scan-truth-conformance-result.v1",
            "profileId": profile["profileId"],
            "overallStatus": overall,
            "adapters": rows,
            "limitations": profile["limitations"],
        }
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        markdown_out = args.markdown_out or args.out.with_suffix(".md")
        markdown_out.parent.mkdir(parents=True, exist_ok=True)
        markdown_out.write_text(render_markdown(result), encoding="utf-8")
        print(f"scan-truth-conformance={overall};adapters={len(rows)};resultSha256={hashlib.sha256(args.out.read_bytes()).hexdigest()}")
        return 0 if overall == "supported" else 1
    finally:
        if temporary is not None:
            temporary.cleanup()


if __name__ == "__main__":
    raise SystemExit(main())
