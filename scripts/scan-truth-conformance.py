#!/usr/bin/env python3
"""Run the bounded synthetic TraceMap cross-adapter scan-truth matrix."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import shutil
import sqlite3
import subprocess
import sys
import tempfile
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


def git(repo: Path, *arguments: str) -> None:
    run(["git", *arguments], cwd=repo)


def create_fixture(parent: Path, definition: AdapterDefinition) -> Path:
    repo = parent / f"fixture-{definition.name}"
    for relative, content in {definition.relative_source: definition.original_source, **definition.additional_files}.items():
        path = repo / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
    git(repo, "init")
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
        run(["dotnet", "build", "src/dotnet/TraceMap.sln", "--no-restore"], cwd=repo_root)
    elif adapter == "jvm":
        run(["gradle", "-p", "src/jvm", "installDist"], cwd=repo_root, env=java_environment())
    elif adapter == "typescript":
        run(["npm", "ci"], cwd=repo_root / "src/typescript")
        run(["npm", "run", "build"], cwd=repo_root / "src/typescript")
    elif adapter == "swift":
        run(["swift", "build", "--package-path", "src/swift", "--product", "tracemap-swift"], cwd=repo_root)


def java_environment() -> dict[str, str]:
    env = dict(os.environ)
    homebrew = Path("/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home")
    if homebrew.is_dir():
        env["JAVA_HOME"] = str(homebrew)
    return env


def scan_command(adapter: str, repo: Path, output: Path, repo_root: Path) -> tuple[list[str], dict[str, str] | None]:
    if adapter == "dotnet":
        return (["dotnet", "run", "--no-build", "--project", "src/dotnet/TraceMap.Cli", "--", "scan", "--repo", str(repo), "--out", str(output)], None)
    if adapter == "jvm":
        return ([str(repo_root / "src/jvm/build/install/tracemap-jvm/bin/tracemap-jvm"), "scan", "--repo", str(repo), "--out", str(output)], java_environment())
    if adapter == "python":
        env = dict(os.environ)
        env["PYTHONPATH"] = str(repo_root / "src/python")
        return ([sys.executable, "-m", "tracemap_py.cli", "scan", "--repo", str(repo), "--out", str(output)], env)
    if adapter == "typescript":
        return (["node", str(repo_root / "src/typescript/dist/src/cli.js"), "scan", "--repo", str(repo), "--out", str(output)], None)
    if adapter == "swift":
        return (["swift", "run", "--skip-build", "--package-path", str(repo_root / "src/swift"), "tracemap-swift", "scan", "--repo", str(repo), "--out", str(output)], None)
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


def capability(name: str, status: str, evidence: list[str], limitations: list[str] | None = None) -> dict[str, Any]:
    return {
        "capability": name,
        "status": status,
        "ruleId": PROFILE_RULE,
        "evidence": evidence,
        "limitations": limitations or [],
    }


def evaluate_adapter(adapter: str, workspace: Path, repo_root: Path) -> dict[str, Any]:
    definition = DEFINITIONS[adapter]
    repo = create_fixture(workspace, definition)
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
    valid = not any(validation_errors.values())
    capabilities = [
        capability("concrete-git-authority", "supported" if valid else "unsupported", ["synthetic-exact-commit"]),
        capability("actual-analyzed-byte-identity", "supported" if valid else "unsupported", ["manifest-sourceSnapshotDigest"]),
        capability("repeat-determinism", "supported" if deterministic else "unsupported", ["baseline-repeat-comparison"]),
        capability("same-size-dirty-mutation", "supported" if dirty_identity else "unsupported", ["same-commit-different-byte-snapshot"]),
        capability("required-artifact-transaction", "supported" if valid else "unsupported", ["five-artifact-validator"]),
        capability("ndjson-sqlite-roundtrip", "supported" if valid else "unsupported", ["read-only-sqlite-roundtrip"]),
        capability("repository-relative-evidence", "supported" if valid else "unsupported", ["private-path-and-relative-path-validator"]),
    ]
    for pending in (
        "inaccessible-input-truth", "source-mutation-detection", "filesystem-correct-exclusion",
        "reduced-analysis-preservation", "malformed-schema-fail-closed",
    ):
        capabilities.append(capability(pending, "not-run", [], ["This capability requires its dedicated adversarial fixture stage."]))
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
        for adapter in adapters:
            try:
                if not args.skip_build:
                    prepare(adapter, repo_root)
                rows.append(evaluate_adapter(adapter, workspace, repo_root))
            except Exception as exception:
                rows.append({
                    "adapter": adapter,
                    "status": "not-run",
                    "capabilities": [capability(name, "not-run", [], [exception.__class__.__name__]) for name in CAPABILITIES],
                    "limitations": [f"adapter-stage-failed:{exception.__class__.__name__}"],
                })
        overall = "supported" if rows and all(row["status"] == "supported" for row in rows) else "unsupported"
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
        print(f"scan-truth-conformance={overall};adapters={len(rows)};resultSha256={hashlib.sha256(args.out.read_bytes()).hexdigest()}")
        return 0 if overall == "supported" else 1
    finally:
        if temporary is not None:
            temporary.cleanup()


if __name__ == "__main__":
    raise SystemExit(main())
