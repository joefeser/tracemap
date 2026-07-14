#!/usr/bin/env python3

from __future__ import annotations

import importlib.util
import json
import sqlite3
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("validate-adapter-artifacts.py")
SPEC = importlib.util.spec_from_file_location("validate_adapter_artifacts", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class AdapterArtifactValidatorTests(unittest.TestCase):
    def test_valid_minimum_output_passes(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp)
            self.write_output(output)
            self.assertEqual([], MODULE.validate_output(output))

    def test_missing_extractor_provenance_column_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp)
            self.write_output(output, include_extractor_columns=False)
            errors = MODULE.validate_output(output)
            self.assertTrue(any("extractor_id" in error for error in errors), errors)

    def test_absolute_evidence_path_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp)
            self.write_output(output, file_path="/opt/build/private.cs")
            errors = MODULE.validate_output(output)
            self.assertTrue(any("repo-relative" in error for error in errors), errors)

    def write_output(self, output: Path, include_extractor_columns: bool = True, file_path: str = "src/Test.cs") -> None:
        (output / "logs").mkdir()
        manifest = {
            "scanId": "scan-1",
            "repoName": "fixture",
            "commitSha": "1" * 40,
            "scannerVersion": "test/1.0",
            "scannedAt": "2026-01-01T00:00:00Z",
            "analysisLevel": "Level3SyntaxAnalysis",
            "buildStatus": "NotRun",
            "knownGaps": [],
        }
        fact = {
            "factId": "fact-1",
            "scanId": "scan-1",
            "repo": "fixture",
            "commitSha": "1" * 40,
            "projectPath": None,
            "factType": "FileInventoried",
            "ruleId": "project.file.v1",
            "evidenceTier": "Tier3SyntaxOrTextual",
            "sourceSymbol": None,
            "targetSymbol": None,
            "contractElement": None,
            "evidence": {
                "filePath": file_path,
                "startLine": 1,
                "endLine": 1,
                "snippetHash": None,
                "extractorId": "fixture",
                "extractorVersion": "1.0",
            },
            "properties": {},
        }
        (output / "scan-manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
        (output / "facts.ndjson").write_text(json.dumps(fact) + "\n", encoding="utf-8")
        (output / "report.md").write_text("# report\n", encoding="utf-8")
        (output / "logs/analyzer.log").write_text("complete\n", encoding="utf-8")
        connection = sqlite3.connect(output / "index.sqlite")
        connection.executescript((MODULE.repo_root() / "contracts/artifacts/index-sqlite.v1.sql").read_text(encoding="utf-8"))
        if not include_extractor_columns:
            connection.execute("alter table facts drop column extractor_id")
        connection.execute(
            "insert into scan_manifest values (?, ?, ?, ?, ?, ?, ?, ?)",
            ("scan-1", "fixture", "1" * 40, "test/1.0", manifest["scannedAt"], manifest["analysisLevel"], manifest["buildStatus"], json.dumps(manifest)),
        )
        columns = (
            "fact_id, scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier, "
            "source_symbol, target_symbol, contract_element, file_path, start_line, end_line, snippet_hash, "
            + ("extractor_id, " if include_extractor_columns else "")
            + "extractor_version, properties_json"
        )
        values = [
            "fact-1", "scan-1", "fixture", "1" * 40, None, "FileInventoried", "project.file.v1", "Tier3SyntaxOrTextual",
            None, None, None, file_path, 1, 1, None,
        ]
        if include_extractor_columns:
            values.append("fixture")
        values.extend(["1.0", "{}"])
        connection.execute(f"insert into facts ({columns}) values ({','.join('?' for _ in values)})", values)
        connection.commit()
        connection.close()


if __name__ == "__main__":
    unittest.main()
