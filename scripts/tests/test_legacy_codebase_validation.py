from __future__ import annotations

import importlib.util
import json
import shutil
import sqlite3
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = ROOT / "scripts" / "legacy_codebase_validation.py"
SPEC = importlib.util.spec_from_file_location("legacy_codebase_validation", MODULE_PATH)
assert SPEC is not None
legacy = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = legacy
SPEC.loader.exec_module(legacy)


class LegacyCodebaseValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp(prefix="tracemap-legacy-test-"))
        subprocess.run(["git", "init", "-q"], cwd=self.tmp, check=True)
        (self.tmp / ".tmp/legacy-codebase-validation").mkdir(parents=True)

    def tearDown(self) -> None:
        shutil.rmtree(self.tmp, ignore_errors=True)

    def write_manifest(self, data: dict) -> Path:
        path = self.tmp / ".tmp/legacy-codebase-validation/repos.local.json"
        path.write_text(json.dumps(data, sort_keys=True), encoding="utf-8")
        return path

    def test_manifest_support_accepts_only_legacy_tmp_manifest_paths(self) -> None:
        sample_repo = self.tmp / "sample"
        sample_repo.mkdir()
        manifest = self.write_manifest(
            {
                "samples": [
                    {
                        "label": "legacy-winforms-app",
                        "path": str(sample_repo),
                        "kind": "legacy-ui",
                    }
                ]
            }
        )

        loaded = legacy.load_manifest(manifest, self.tmp)

        self.assertEqual("legacy-winforms-app", loaded.samples[0].label)
        with self.assertRaises(legacy.ValidationError):
            legacy.load_manifest(self.tmp / "repos.local.json", self.tmp)

    def test_invalid_manifest_json_reports_validation_error(self) -> None:
        manifest = self.tmp / ".tmp/legacy-codebase-validation/repos.local.json"
        manifest.write_text("{not-json", encoding="utf-8")

        with self.assertRaises(legacy.ValidationError) as context:
            legacy.load_manifest(manifest, self.tmp)

        self.assertEqual("manifest", context.exception.category)

    def test_output_path_must_remain_under_legacy_tmp(self) -> None:
        output = legacy.validate_output_path(Path(".tmp/legacy-codebase-validation/out"), self.tmp)

        self.assertTrue(output.is_dir())
        with self.assertRaises(legacy.ValidationError):
            legacy.validate_output_path(Path("docs/legacy-output"), self.tmp)

    def test_redaction_rejects_public_unsafe_values_without_echoing_them(self) -> None:
        mac_path = "/" + "Users" + "/operator/private-repo"
        unsafe_text = "\n".join(
            [
                mac_path,
                "https://example.invalid/private/repo.git",
                "select * from CustomerSecrets",
                "Server=db;Password=secret;",
                "ApiKey: private-value",
                "sk-" + ("x" * 24),
                "public interface SecretShape { }",
                "public async Task SendSecret() { }",
                "private-sample-name",
            ]
        )

        failures = legacy.redact_failures(unsafe_text, ("private-sample-name",))

        self.assertEqual(
            {
                "absolute-path",
                "raw-remote",
                "raw-sql",
                "connection-string",
                "config-value",
                "secret",
                "snippet",
                "private-name",
            },
            {failure.category for failure in failures},
        )

    def test_git_tracked_tmp_files_fail_validation(self) -> None:
        tracked = self.tmp / ".tmp/legacy-codebase-validation/leak.txt"
        tracked.write_text("local-only", encoding="utf-8")
        subprocess.run(["git", "add", tracked.relative_to(self.tmp).as_posix()], cwd=self.tmp, check=True)

        with self.assertRaises(legacy.ValidationError) as context:
            legacy.ensure_no_tracked_legacy_files(self.tmp)

        self.assertEqual("tracked-tmp", context.exception.category)

    def test_summary_shape_is_deterministic_and_safe(self) -> None:
        manifest = legacy.Manifest(
            samples=(
                legacy.Sample("large-public-dotnet-client", self.tmp / "large", "large-public"),
                legacy.Sample("legacy-winforms-app", self.tmp / "legacy", "legacy-ui"),
            )
        )
        summaries = [
            legacy.SampleSummary(
                label="legacy-winforms-app",
                kind="legacy-ui",
                status="dry-run",
                exit_code=0,
                duration_seconds=0.0,
                output_size_bytes=0,
                truncated=False,
                deferred=True,
                artifacts={"facts.ndjson": False, "index.sqlite": False},
                facts_count=0,
                analysis_gap_count=0,
                coverage_label="unknown",
                build_status="unknown",
                commit_sha_present=False,
                legacy_indicators={},
                ui_event_probe={"classification": "gap-no-current-event-evidence"},
                wcf_fact_counts={},
                limitations=["dry-run: scan not executed"],
            ),
            legacy.SampleSummary(
                label="large-public-dotnet-client",
                kind="large-public",
                status="completed",
                exit_code=0,
                duration_seconds=1.25,
                output_size_bytes=123,
                truncated=False,
                deferred=False,
                artifacts={"facts.ndjson": True, "index.sqlite": True},
                facts_count=10,
                analysis_gap_count=1,
                coverage_label="Level1SemanticAnalysisReduced",
                build_status="Succeeded",
                commit_sha_present=True,
                legacy_indicators={"targetFrameworks": ["net472"]},
                ui_event_probe={"classification": "gap-no-current-event-evidence"},
                wcf_fact_counts={"WcfServiceReferenceMapping": 2},
                limitations=[],
            ),
        ]

        first = json.dumps(legacy.build_summary(manifest, summaries), sort_keys=True)
        second = json.dumps(legacy.build_summary(manifest, list(reversed(summaries))), sort_keys=True)

        self.assertEqual(first, second)
        self.assertFalse(legacy.redact_failures(first))

    def test_ui_probe_can_be_disabled_for_large_repository_smoke(self) -> None:
        result = legacy.probe_ui_events(
            [
                {
                    "factType": "PackageReferenced",
                    "ruleId": "project.file.v1",
                    "evidenceTier": "Tier2Structural",
                    "properties": {"packageName": "Some.Click.Named.Package"},
                }
            ],
            enabled=False,
        )

        self.assertEqual("not-applicable", result["classification"])
        self.assertEqual(0, result["structuralMatches"])

    def test_ui_probe_does_not_treat_version_comparison_as_event_wiring(self) -> None:
        result = legacy.probe_ui_events(
            [
                {
                    "factType": "PackageReferenced",
                    "ruleId": "project.file.v1",
                    "evidenceTier": "Tier2Structural",
                    "properties": {"version": ">=1.0"},
                }
            ]
        )

        self.assertEqual("gap-no-current-event-evidence", result["classification"])
        self.assertEqual(0, result["structuralMatches"])

    def test_ui_probe_reconciles_precise_winforms_and_webforms_without_divergent_counts(self) -> None:
        result = legacy.probe_ui_events(
            [
                {
                    "factType": "WebFormsEventBindingDeclared",
                    "ruleId": "legacy.webforms.event-binding.v1",
                    "evidenceTier": "Tier2Structural",
                },
                {
                    "factType": "WebFormsEventFlowProjected",
                    "ruleId": "legacy.webforms.event-flow.v1",
                    "evidenceTier": "Tier2Structural",
                },
                {
                    "factType": "WinFormsEventBindingDeclared",
                    "ruleId": "legacy.winforms.event-binding.v1",
                    "evidenceTier": "Tier2Structural",
                },
                {
                    "factType": "WinFormsHandlerFlowProjected",
                    "ruleId": "legacy.winforms.handler-flow.v1",
                    "evidenceTier": "Tier3SyntaxOrTextual",
                },
                {
                    "factType": "MethodDeclared",
                    "ruleId": "csharp.syntax.declarations.v1",
                    "evidenceTier": "Tier3SyntaxOrTextual",
                    "targetSymbol": "Click",
                },
            ]
        )

        self.assertEqual("structural-static-wiring", result["classification"])
        self.assertEqual(3, result["structuralMatches"])
        self.assertEqual(1, result["syntaxOrTextMatches"])
        self.assertEqual(2, result["downstreamEvidenceMatches"])
        self.assertEqual(
            [
                "WebFormsEventBindingDeclared",
                "WebFormsEventFlowProjected",
                "WinFormsEventBindingDeclared",
                "WinFormsHandlerFlowProjected",
            ],
            result["factTypes"],
        )

    def test_collects_wcf_counts_from_fact_rows(self) -> None:
        counts = legacy.collect_wcf_counts(
            {},
            [
                {"factType": "WcfClientEndpointDeclared"},
                {"factType": "WcfServiceReferenceMetadataDeclared"},
                {"factType": "WcfMetadataOperationDeclared"},
                {"factType": "AnalysisGap", "properties": {"classification": "MalformedWcfMetadata"}},
                {"factType": "WcfServiceReferenceMapping"},
                {"factType": "WcfServiceReferenceMapping"},
                {"factType": "Other"},
            ],
        )

        self.assertEqual(1, counts["WcfClientEndpointDeclared"])
        self.assertEqual(1, counts["WcfServiceReferenceMetadataDeclared"])
        self.assertEqual(1, counts["WcfMetadataOperationDeclared"])
        self.assertEqual(1, counts["AnalysisGap:MalformedWcfMetadata"])
        self.assertEqual(2, counts["WcfServiceReferenceMapping"])

    def test_wcf_rule_ids_are_included_when_wcf_counts_exist(self) -> None:
        summary = legacy.SampleSummary(
            label="legacy-winforms-app",
            kind="legacy-ui",
            status="completed",
            exit_code=0,
            duration_seconds=0.0,
            output_size_bytes=0,
            truncated=False,
            deferred=False,
            artifacts={},
            facts_count=1,
            analysis_gap_count=0,
            coverage_label="Level1SemanticAnalysisReduced",
            build_status="FailedOrPartial",
            commit_sha_present=True,
            legacy_indicators={},
            ui_event_probe={"classification": "semantic-static-wiring"},
            wcf_fact_counts={"WcfServiceReferenceMapping": 1},
            limitations=[],
        )

        self.assertIn("legacy.wcf.mapping.v1", legacy.sample_to_json(summary)["ruleIds"])

    def test_summary_uses_sqlite_counts_without_parsing_oversized_facts(self) -> None:
        output = self.tmp / ".tmp/legacy-codebase-validation/out"
        sample_out = output / "large-public-dotnet-client"
        sample_out.mkdir(parents=True)
        (sample_out / "scan-manifest.json").write_text(
            json.dumps({"analysisLevel": "Level1SemanticAnalysisReduced", "buildStatus": "Succeeded", "commitSha": "abc123"}),
            encoding="utf-8",
        )
        (sample_out / "facts.ndjson").write_text("{not-json}\n" * 20, encoding="utf-8")
        with sqlite3.connect(sample_out / "index.sqlite") as connection:
            connection.execute("create table facts (fact_type text not null)")
            connection.executemany(
                "insert into facts (fact_type) values (?)",
                [("AnalysisGap",), ("WcfServiceReferenceMapping",), ("WcfServiceReferenceMapping",)],
            )

        sample = legacy.Sample("large-public-dotnet-client", self.tmp / "large", "large-public", max_artifact_bytes=1)

        summary = legacy.summarize_sample(sample, output, "completed", 0, 0.0, [])

        self.assertEqual(3, summary.facts_count)
        self.assertEqual(1, summary.analysis_gap_count)
        self.assertEqual(2, summary.wcf_fact_counts["WcfServiceReferenceMapping"])
        self.assertIn("AnalysisGap:MalformedWcfMetadata", summary.wcf_fact_counts)
        self.assertIn("facts.ndjson parsing skipped", " ".join(summary.limitations))


if __name__ == "__main__":
    unittest.main()
