"""Public synthetic result-column smoke guards; no CLI, network, or SQL execution."""
import importlib.util
import json
from pathlib import Path
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("result_smoke", ROOT / "scripts/sqlserver-result-column-evidence/smoke.py")
SMOKE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SMOKE)


def fact(name, line, state=None):
    properties = {} if state is None else {"sqlServerReaderState": json.dumps(state)}
    return {"factId": f"id-{name}-{line}", "repo": "synthetic", "commitSha": "1" * 40, "ruleId": "database.operation.call-pattern.v1",
            "evidenceTier": "Tier1Semantic", "sourceSymbol": f"ResultCases.{name}(string, int, bool)",
            "evidence": {"filePath": "Results.cs", "startLine": line, "endLine": line, "extractorId": "CSharpSemanticExtractor",
                         "extractorVersion": "csharp-semantic/0.22.13"}, "properties": properties}


class ResultSmokeGuards(unittest.TestCase):
    def test_captured_cli_uses_fixed_executable_and_literal_arguments_without_shell(self):
        argument = "$(touch never); & | path with spaces"
        with patch.object(SMOKE.subprocess, "run") as run:
            SMOKE.run_captured_cli("dotnet", Path("local.dll"), argument)
            run.assert_called_once_with(["dotnet", "local.dll", argument], cwd=SMOKE.ROOT,
                                        capture_output=True, text=True, shell=False)
        with patch.object(SMOKE.subprocess, "run") as run:
            for args in ((), ("sh", "-c", argument), ("dotnet;evil",)):
                with self.assertRaises(ValueError):
                    SMOKE.run_captured_cli(*args)
            run.assert_not_called()

    def test_documented_denominator_is_derived_from_the_authored_matrix(self):
        self.assertEqual(SMOKE.EXPECTED_TOTALS, SMOKE.expected_totals())
        self.assertEqual(34, len(SMOKE.SOURCE_CASES))
        self.assertEqual(SMOKE.SITES_PER_LANGUAGE, len(SMOKE.SOURCE_CASES))
        self.assertEqual({"json": {"bounded-match": 18, "mismatch": 5, "unknown": 18},
                          "ddl": {"bounded-match": 18, "mismatch": 5, "unknown": 29}}, SMOKE.expected_totals())
        self.assertTrue(set(SMOKE.JSON_VARIANTS) <= set(SMOKE.SOURCE_CASES))
        self.assertTrue(set(SMOKE.DDL_TEMPLATES) <= set(SMOKE.SOURCE_CASES))
        for template in (*SMOKE.DDL_TEMPLATES.values(), *SMOKE.BODY_MATRIX, "base.sql"):
            self.assertTrue((SMOKE.SAMPLE / "ddl" / template).is_file(), template)

    def test_missing_unexpected_and_duplicate_cases_are_explicit_gaps(self):
        keys = list(SMOKE.SOURCE_CASES)
        self.assertEqual([], SMOKE.case_gaps(keys))
        self.assertEqual(["missing:NameMatch"], SMOKE.case_gaps(k for k in keys if k != "NameMatch"))
        self.assertEqual(["unexpected:Renamed2"], SMOKE.case_gaps([*keys, "Renamed2"]))
        self.assertEqual(["duplicate:ReadLoop"], SMOKE.case_gaps([*keys, "ReadLoop"]))
        # A renamed case is never silently counted as the original.
        self.assertEqual(["missing:Renamed", "unexpected:Renamedd"],
                         SMOKE.case_gaps(["Renamedd" if k == "Renamed" else k for k in keys]))

    def test_repeated_methods_get_source_ordered_suffixes(self):
        facts = [fact("TwoReaders", 20), fact("NameMatch", 5), fact("TwoReaders", 19)]
        self.assertEqual([("NameMatch", 5), ("TwoReaders#0", 19), ("TwoReaders#1", 20)],
                         [(k, f["evidence"]["startLine"]) for f, k in SMOKE.case_keys(facts)])

    def test_reader_projection_mirrors_core_order_and_excludes_other_properties(self):
        state = {"version": "sqlserver-reader/1", "coverage": "bounded"}
        item = fact("NameMatch", 5, state)
        item["properties"]["rawSql"] = "PRIVATE"
        projected = SMOKE.reader_projection(item)
        self.assertEqual(["factId", "repo", "commitSha", "ruleId", "evidenceTier", "filePath", "startLine", "endLine",
                          "extractorId", "extractorVersion", "state"], list(projected))
        self.assertEqual(state, projected["state"])
        self.assertNotIn("PRIVATE", json.dumps(projected))
        self.assertIsNone(SMOKE.reader_projection(fact("ScalarChannel", 9))["state"])

    def test_declaration_variants_are_independent_of_extractor_output(self):
        base = json.loads((SMOKE.SAMPLE / "result-declarations-base.json").read_bytes())
        names = lambda d: [c["name"] for c in d["shapes"][0]["columns"]]
        self.assertEqual(["Id", "Name", "Total"], names(SMOKE.declarations(base, None, "r", "1" * 40, "p.json")))
        self.assertEqual(["Id", "DisplayName", "Total"], names(SMOKE.declarations(base, "renamed", "r", "1" * 40, "p.json")))
        reordered = SMOKE.declarations(base, "reordered", "r", "1" * 40, "p.json")
        self.assertEqual(["Total", "Name", "Id"], names(reordered))
        self.assertEqual([0, 1, 2], [c["ordinal"] for c in reordered["shapes"][0]["columns"]])
        self.assertEqual(["Id", "Name", "Name"], names(SMOKE.declarations(base, "duplicate", "r", "1" * 40, "p.json")))
        incomplete = SMOKE.declarations(base, "incomplete", "r", "1" * 40, "p.json")
        self.assertEqual(("unknown", "authored-incomplete"), (incomplete["shapes"][0]["completeness"], incomplete["shapes"][0]["unknownReason"]))
        self.assertEqual(["Id", "Name", "Total"], names(base))  # The committed sample is never mutated.


if __name__ == "__main__":
    unittest.main()
