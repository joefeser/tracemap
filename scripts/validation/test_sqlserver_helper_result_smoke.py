"""Public synthetic helper-result consumer smoke guards; no CLI, network, or SQL execution."""
import ast
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest import mock
ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("helper_result_smoke", ROOT / "scripts/sqlserver-helper-result-evidence/smoke.py")
SMOKE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SMOKE)
M, X, U = "bounded-match", "mismatch", "unknown"
CALLER = {"line": 30, "endLine": 30, "caller": "HelperCases.TwoCallers(string, int, bool)", "coverage": "bounded", "gap": None,
          "commandTextHash": "a" * 64, "resultLine": 30, "selectors": [], "guards": [], "accesses": [{"name": "Name"}]}


def state(callers=(), **fields):
    value = {"version": "sqlserver-helper-result/1", "coverage": "bounded", "gap": None, "resultKind": "datatable", "commandType": "StoredProcedure",
             "commandTextSource": "constructor-parameter", "adapterAssociation": "constructor-command", "fillOverload": "fill-datatable",
             "tableName": None, "helperLine": 3, "adapterLine": 5, "resultLine": 6, "callers": list(callers)}
    value.update(fields)
    return value


def fact(name, line, helper=None, table_gap="datatable-alias-or-escape", set_gap="fill-overload-unsupported", table_coverage="unknown"):
    properties = {"operationKind": "data-adapter-fill",
                  "sqlServerDataTableState": json.dumps({"version": "sqlserver-datatable/1", "coverage": table_coverage, "gap": table_gap, "accesses": []}),
                  "sqlServerDataSetState": json.dumps({"version": "sqlserver-dataset/1", "coverage": "unknown", "gap": set_gap, "accesses": []}),
                  "sqlServerHelperResultState": json.dumps(helper if helper is not None else state())}
    return {"factId": f"id-{name}-{line}", "repo": "synthetic", "commitSha": "1" * 40, "ruleId": "database.operation.call-pattern.v1",
            "evidenceTier": "Tier1Semantic", "sourceSymbol": f"HelperCases.{name}(string)",
            "evidence": {"filePath": "Helpers.cs", "startLine": line, "endLine": line, "extractorId": "CSharpSemanticExtractor",
                         "extractorVersion": "csharp-semantic/0.22.16"}, "properties": properties}


class HelperResultSmokeGuards(unittest.TestCase):
    def test_native_fixtures_are_in_the_normal_test_build_graph(self):
        project = ROOT / "src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj"
        references = {(project.parent / node.attrib["Include"].replace("\\", "/")).resolve(): node
                      for node in ET.parse(project).iter("ProjectReference")}
        for language, extension in (("csharp", "csproj"), ("vb", "vbproj")):
            fixture = ROOT / "samples/sqlserver-helper-result-evidence" / language / f"Helpers.{extension}"
            with self.subTest(language=language):
                self.assertIn(fixture, references)
                self.assertEqual("false", references[fixture].get("ReferenceOutputAssembly"))
                self.assertIsNone(references[fixture].get("Condition"))

    def test_optimized_execution_refuses_before_output_or_helpers(self):
        for flag, env_value in (("-O", None), ("-OO", None), (None, "1")):
            with self.subTest(flag=flag, env=env_value), tempfile.TemporaryDirectory() as tmp:
                output = Path(tmp) / "must-not-exist"
                env = dict(os.environ)
                env.pop("PYTHONOPTIMIZE", None)
                if env_value:
                    env["PYTHONOPTIMIZE"] = env_value
                result = subprocess.run([sys.executable, *([flag] if flag else []), SMOKE.__file__, "--output", str(output)],
                                        env=env, capture_output=True, text=True, check=False)
                self.assertNotEqual(0, result.returncode)
                self.assertIn("requires Python optimization disabled", result.stderr)
                self.assertFalse(output.exists())
        source = Path(SMOKE.__file__).read_text()
        self.assertLess(source.index("if sys.flags.optimize:"), source.index("SPEC.loader.exec_module"))
        self.assertFalse(any(isinstance(n, ast.Assert) for n in ast.walk(ast.parse(source))))

    def test_denominator_is_derived_from_the_authored_matrix(self):
        self.assertEqual(SMOKE.EXPECTED_TOTALS, SMOKE.expected_totals())
        self.assertEqual({M: 42, X: 15, U: 58}, SMOKE.expected_totals())
        plan = SMOKE.case_plan()
        self.assertEqual(115, len(plan))
        self.assertEqual(23 * 3, sum(1 for kind, key, *_ in plan if key in SMOKE.CORE_CASES))
        self.assertEqual((27, 19, 24), (len(SMOKE.HELPER_CASES), len(SMOKE.CALLER_CASES), SMOKE.REFUSAL_COUNT))
        self.assertTrue(all(origin == "ddl-table-result" and statuses == [U] for kind, key, origin, _, statuses in plan
                            if kind == "helper" or key in SMOKE.CALLER_CASES))
        self.assertFalse(set(SMOKE.CORE_CASES) & set(SMOKE.CALLER_CASES))
        # Multiple callers of one helper with different procedures are separate authored cells.
        self.assertEqual(([M], None), SMOKE.CORE_CASES["TwoCallers#0"])
        self.assertIn("TwoCallers#1", SMOKE.OTHER_PROCEDURE)

    def test_fill_and_caller_keys_are_source_ordered_and_gaps_are_explicit(self):
        two = dict(CALLER, line=31)
        helper = fact("ReadTable", 10, state([CALLER, two]))
        fills = SMOKE.fill_keys([fact("OverloadedHelper", 50), helper, fact("OverloadedHelper", 40), fact("GenericHelper<T>", 60)])
        self.assertEqual(["ReadTable", "OverloadedHelper#0", "OverloadedHelper#1", "GenericHelper"], [k for k, _ in fills])
        sites = SMOKE.caller_keys(fills)
        self.assertEqual([("TwoCallers#0", 0), ("TwoCallers#1", 1)], [(k, v[1]) for k, v in sites])
        self.assertEqual("GenericHelper", SMOKE.member("HelperCases.GenericHelper(Of T)(String)"))
        gaps = SMOKE.case_gaps(["ReadTable", "Surprise"], ["TableName", "TableName"])
        self.assertIn("missing:helper:ReadSet", gaps)
        self.assertIn("unexpected:helper:Surprise", gaps)
        self.assertIn("duplicate:caller:TableName", gaps)
        self.assertIn("missing:caller:TwoCallers#1", gaps)

    def test_helper_digest_is_contract_tagged_per_caller_and_distinct_from_other_routes(self):
        two = dict(CALLER, line=31, commandTextHash="b" * 64)
        item = fact("ReadTable", 10, state([CALLER, two]))
        first, second = SMOKE.helper_projection(item, 0), SMOKE.helper_projection(item, 1)
        self.assertEqual(("sqlserver-helper-result-bindings/1", 0, CALLER, []), (first["contract"], first["callerIndex"], first["caller"], first["helperState"]["callers"]))
        self.assertEqual(list(first)[-3:], ["callerIndex", "helperState", "caller"])
        self.assertNotEqual(SMOKE.HELPERS.digest(first), SMOKE.HELPERS.digest(second))
        self.assertIsNone(SMOKE.helper_projection(item, 2)["caller"])
        for other in (SMOKE.DATASET.dataset_projection(item), SMOKE.DATASET.datatable_projection(item), SMOKE.RESULT.reader_projection(item)):
            self.assertNotEqual(SMOKE.HELPERS.digest(first), SMOKE.HELPERS.digest(other))
        bindings = SMOKE.binding(item, 1, "TwoCallers#1", "1" * 40, second)
        call = bindings["calls"][0]
        self.assertEqual(("sqlserver-helper-result-bindings/1", 1, "dbo.ReadOther"), (bindings["version"], call["callerIndex"], call["commandText"]))
        self.assertEqual(SMOKE.HELPERS.digest(second), call["helperResultEvidenceSha256"])
        self.assertNotIn("dataSetEvidenceSha256", call)
        self.assertEqual([], SMOKE.binding(item, None, "UncalledHelper", "1" * 40, first)["calls"])

    def test_state_drift_and_invalid_aggregate_never_publish_a_receipt(self):
        self.assertIsNone(SMOKE.check_helper_state("ReadTable", fact("ReadTable", 10)))
        for bad in (fact("ReadTable", 10, table_gap="fresh-sqldataadapter-local-required"),
                    fact("ReadTable", 10, state(coverage="unknown", gap="helper-recursion")),
                    fact("ReadSet", 10, state(resultKind="dataset"))):
            with self.assertRaises(ValueError):
                SMOKE.check_helper_state(bad["sourceSymbol"].split(".")[1].split("(")[0], bad)
        unknown = state(coverage="unknown", gap="helper-recursion")
        self.assertEqual("helper-recursion", SMOKE.check_helper_state("RecursiveHelper", fact("RecursiveHelper", 10, unknown)))
        with self.assertRaises(ValueError):
            SMOKE.check_helper_state("RecursiveHelper", fact("RecursiveHelper", 10, state(coverage="unknown", gap="helper-reference-escape")))
        with self.assertRaises(ValueError):
            SMOKE.check_helper_state("InlineFill", fact("InlineFill", 10, state(coverage="unknown", gap="helper-return-required")))
        self.assertEqual("helper-return-required", SMOKE.check_helper_state("InlineFill", fact(
            "InlineFill", 10, state(coverage="unknown", gap="helper-return-required"), table_coverage="bounded", table_gap=None)))
        self.assertIsNone(SMOKE.check_caller_state("TableName", CALLER))
        with self.assertRaises(ValueError):
            SMOKE.check_caller_state("TableName", dict(CALLER, accesses=[]))
        with self.assertRaises(ValueError):
            SMOKE.check_caller_state("CallerChained", CALLER)
        self.assertEqual("helper-result-local-required", SMOKE.check_caller_state("CallerChained", dict(
            CALLER, coverage="unknown", gap="helper-result-local-required", accesses=[])))
        good = {"gaps": [], "observedCounts": {M: 1}, "expectedCounts": {M: 1}, "refusals": [{"refused": True}] * SMOKE.REFUSAL_COUNT}
        for change in ({"gaps": ["missing:caller:TableName"]}, {"observedCounts": {M: 2}}, {"refusals": []},
                       {"refusals": [{"refused": False}] * SMOKE.REFUSAL_COUNT}):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as tmp:
                with mock.patch.object(SMOKE.HELPERS, "verify_scan_hashes") as verify:
                    with self.assertRaises(ValueError):
                        SMOKE.publish_receipt(Path(tmp), dict(good, **change))
                    verify.assert_not_called()
                self.assertFalse((Path(tmp) / "smoke-receipt.json").exists())

    def test_report_status_caller_or_contract_drift_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            schema = root / "schema.json"
            SMOKE.HELPERS.write(schema, {"version": "x"})
            row = {"factId": "f", "callerIndex": 1, "status": X, "reasons": [], "ruleId": SMOKE.RULE, "tier": "Tier2Structural", "sourceTier": "Tier1Semantic"}
            other = dict(row, callerIndex=0, status=U, tier="Tier4Unknown", reasons=["binding-missing"])
            report = {"version": "sqlserver-helper-result-comparison/1", "consumerVersion": "sqlserver-helper-result/1",
                      "bindingsVersion": "sqlserver-helper-result-bindings/1", "schemaRoute": "result-schema",
                      "origin": "independent-synthetic-result-declarations", "coverage": "partial", "verdict": "no-aggregate-clean-verdict",
                      "generatorSha256": "a" * 64, "schemaEvidenceSha256": SMOKE.HELPERS.digest({"version": "x"}), "comparisons": [other, row]}
            for name in ("report", "repeat"):
                (root / name).mkdir()
                SMOKE.HELPERS.write(root / name / "helper-result-column-comparison.json", report)
                (root / name / "report.md").write_text("No aggregate clean verdict binding-missing")
            self.assertEqual([X], SMOKE.check_report(root, {"factId": "f"}, 1, [X], "result-schema", None, "a" * 64, schema)[0])
            for index, expected, origin in ((1, [M], "result-schema"), (1, [X], "ddl-result"), (None, [X], "result-schema")):
                with self.subTest(index=index, expected=expected, origin=origin), self.assertRaises(ValueError):
                    SMOKE.check_report(root, {"factId": "f"}, index, expected, origin, None, "a" * 64, schema)


if __name__ == "__main__":
    unittest.main()
