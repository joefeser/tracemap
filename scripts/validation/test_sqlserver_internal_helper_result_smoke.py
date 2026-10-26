"""Public synthetic internal-helper-result consumer smoke guards; no CLI, network, or SQL execution."""
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
SPEC = importlib.util.spec_from_file_location("internal_helper_result_smoke", ROOT / "scripts/sqlserver-internal-helper-result-evidence/smoke.py")
SMOKE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SMOKE)
M, X, U = "bounded-match", "mismatch", "unknown"
SAMPLE = ROOT / "samples/sqlserver-internal-helper-result-evidence"
CALLER = {"path": "csharp/Callers.cs", "line": 30, "endLine": 30, "caller": "Callers.CrossTwoCallers(string, int, bool)", "coverage": "bounded",
          "gap": None, "commandTextHash": "a" * 64, "resultLine": 30, "selectors": [], "guards": [], "accesses": [{"name": "Name"}]}


def state(callers=(), **fields):
    value = {"version": "sqlserver-internal-helper-result/1", "coverage": "bounded", "gap": None, "resultKind": "datatable",
             "commandType": "StoredProcedure", "commandTextSource": "constructor-parameter", "adapterAssociation": "constructor-command",
             "fillOverload": "fill-datatable", "tableName": None, "helperLine": 3, "adapterLine": 5, "resultLine": 6, "callers": list(callers)}
    value.update(fields)
    return value


def fact(name, line, internal=None, phase4_gap="helper-accessibility-unsupported", path="csharp/Helpers.cs"):
    phase4 = {"version": "sqlserver-helper-result/2", "coverage": "unknown", "gap": phase4_gap, "callers": []}
    properties = {"operationKind": "data-adapter-fill",
                  "sqlServerDataTableState": json.dumps({"version": "sqlserver-datatable/1", "coverage": "unknown", "gap": "datatable-alias-or-escape", "accesses": []}),
                  "sqlServerDataSetState": json.dumps({"version": "sqlserver-dataset/1", "coverage": "unknown", "gap": "fill-overload-unsupported", "accesses": []}),
                  "sqlServerHelperResultState": json.dumps(phase4),
                  "sqlServerInternalHelperResultState": json.dumps(internal if internal is not None else state())}
    return {"factId": f"id-{name}-{line}", "repo": "synthetic", "commitSha": "1" * 40, "ruleId": "database.operation.call-pattern.v1",
            "evidenceTier": "Tier1Semantic", "sourceSymbol": f"InternalData.{name}(string)",
            "evidence": {"filePath": path, "startLine": line, "endLine": line, "extractorId": "CSharpSemanticExtractor",
                         "extractorVersion": "csharp-semantic/0.22.18"}, "properties": properties}


class InternalHelperResultSmokeGuards(unittest.TestCase):
    def test_sql_injection_suppressions_are_rule_specific_static_fixture_controls(self):
        lines = []
        for path in sorted([*SAMPLE.glob("csharp/**/*.cs"), *SAMPLE.glob("shared/*.cs")]):
            if "obj" in path.parts or "bin" in path.parts:
                continue  # Build outputs are not fixture sources.
            text = path.read_text()
            self.assertIn("never invoke methods.", text)
            lines += [line for line in text.splitlines() if "nosemgrep" in line]
        self.assertEqual(14, len(lines))
        for line in lines:
            self.assertRegex(line, r"(?:new SqlCommand\(procedure(?:, connection)?\)|command\.CommandText = procedure); // nosemgrep: csharp\.lang\.security\.sqli\.csharp-sqli$")
        for language, extension in (("csharp", "csproj"), ("vb", "vbproj")):
            project = ET.parse(SAMPLE / language / f"InternalHelpers.{extension}")
            self.assertFalse(any(n.text in ("Exe", "WinExe") for n in project.iter("OutputType")))

    def test_fixture_documents_and_links_match_the_authored_document_classes(self):
        for language, extension in (("csharp", "cs"), ("vb", "vb")):
            with self.subTest(language=language):
                project = ET.parse(SAMPLE / language / f"InternalHelpers.{extension}proj")
                links = sorted((n.attrib["Include"], n.attrib["Link"]) for n in project.iter("Compile"))
                self.assertEqual([(f"../shared/LinkedCallers.{extension}", f"LinkedCallers.{extension}"),
                                  (f"../shared/LinkedHelpers.{extension}", f"LinkedHelpers.{extension}")], links)
                names = sorted(p.relative_to(SAMPLE / language).as_posix() for p in (SAMPLE / language).rglob(f"*.{extension}")
                               if "obj" not in p.parts and "bin" not in p.parts)
                self.assertEqual(sorted(f"{n}.{extension}" for n in ("Bases", "Callers", "Escapes", "Generated/GeneratedCallers.g", "Helpers",
                                                                     "MoreCallers", "SplitA", "SplitB")), names)
                generated = (SAMPLE / language / f"Generated/GeneratedCallers.g.{extension}").read_text()
                self.assertTrue(generated.splitlines()[0].endswith("<auto-generated/>"))

    def test_native_fixtures_are_in_the_normal_test_build_graph(self):
        project = ROOT / "src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj"
        references = {(project.parent / node.attrib["Include"].replace("\\", "/")).resolve(): node
                      for node in ET.parse(project).iter("ProjectReference")}
        for language, extension in (("csharp", "csproj"), ("vb", "vbproj")):
            fixture = SAMPLE / language / f"InternalHelpers.{extension}"
            with self.subTest(language=language):
                self.assertIn(fixture, references)
                self.assertEqual("false", references[fixture].get("ReferenceOutputAssembly"))
                self.assertIsNone(references[fixture].get("Condition"))

    def test_optimized_execution_refuses_before_output_or_helpers(self):
        for flag, env_value in (("-O", None), (None, "1")):
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
        self.assertLess(source.index("if sys.flags.optimize:"), source.index("spec.loader.exec_module"))
        self.assertFalse(any(isinstance(n, ast.Assert) for n in ast.walk(ast.parse(source))))

    def test_denominator_is_derived_from_the_authored_matrix(self):
        self.assertEqual(SMOKE.EXPECTED_TOTALS, SMOKE.expected_totals())
        self.assertEqual({M: 36, X: 12, U: 30}, SMOKE.expected_totals())
        plan = SMOKE.case_plan()
        self.assertEqual(78, len(plan))
        self.assertEqual(20 * 3, sum(1 for kind, key, *_ in plan if key in SMOKE.CORE_CASES))
        self.assertEqual((10, 8, 24, 14), (len(SMOKE.HELPER_CASES), len(SMOKE.CALLER_CASES), SMOKE.REFUSAL_COUNT, SMOKE.SITES_PER_LANGUAGE))
        self.assertTrue(all(origin == "ddl-table-result" and statuses == [U] for kind, key, origin, _, statuses in plan
                            if kind == "helper" or key in SMOKE.CALLER_CASES))
        self.assertFalse(set(SMOKE.CORE_CASES) & set(SMOKE.CALLER_CASES))
        self.assertEqual("helper-private-phase4-scope", SMOKE.HELPER_CASES["PrivateHelper"])
        self.assertIn("CrossTwoCallers#1", SMOKE.OTHER_PROCEDURE)

    def test_caller_keys_are_path_then_line_ordered_and_gaps_are_explicit(self):
        later = dict(CALLER, path="csharp/MoreCallers.cs", line=5, caller="MoreCallers.OtherTypeCaller(string, int, bool)")
        same = dict(CALLER, path="csharp/Helpers.cs", line=90, caller="InternalData.SameDocumentName(string, int, bool)")
        two = dict(CALLER, line=31)
        helper = fact("ReadTable", 10, state([CALLER, two, same, later]))
        fills = SMOKE.fill_keys([fact("LinkedFileHelper", 3, path="shared/LinkedHelpers.cs"), helper])
        self.assertEqual(["ReadTable", "LinkedFileHelper"], [k for k, _ in fills])
        sites = SMOKE.caller_keys(fills)
        self.assertEqual([("CrossTwoCallers#0", 0), ("CrossTwoCallers#1", 1), ("SameDocumentName", 2), ("OtherTypeCaller", 3)], [(k, v[1]) for k, v in sites])
        gaps = SMOKE.case_gaps(["ReadTable", "Surprise"], ["CrossTableName", "CrossTableName"])
        self.assertIn("missing:helper:ReadSet", gaps)
        self.assertIn("unexpected:helper:Surprise", gaps)
        self.assertIn("duplicate:caller:CrossTableName", gaps)

    def test_internal_digest_is_contract_tagged_per_caller_covers_the_path_and_differs_from_phase4(self):
        two = dict(CALLER, line=31, commandTextHash="b" * 64)
        item = fact("ReadTable", 10, state([CALLER, two]))
        first, second = SMOKE.internal_projection(item, 0), SMOKE.internal_projection(item, 1)
        self.assertEqual(("sqlserver-internal-helper-result-bindings/1", 0, CALLER, []), (first["contract"], first["callerIndex"], first["caller"], first["helperState"]["callers"]))
        self.assertEqual(list(first)[-3:], ["callerIndex", "helperState", "caller"])
        self.assertNotEqual(SMOKE.HELPERS.digest(first), SMOKE.HELPERS.digest(second))
        moved = fact("ReadTable", 10, state([dict(CALLER, path="csharp/MoreCallers.cs"), two]))
        self.assertNotEqual(SMOKE.HELPERS.digest(first), SMOKE.HELPERS.digest(SMOKE.internal_projection(moved, 0)))
        for other in (SMOKE.PHASE4.helper_projection(item, 0), SMOKE.DATASET.dataset_projection(item), SMOKE.DATASET.datatable_projection(item),
                      SMOKE.RESULT.reader_projection(item)):
            self.assertNotEqual(SMOKE.HELPERS.digest(first), SMOKE.HELPERS.digest(other))
        bindings = SMOKE.binding(item, 1, "CrossTwoCallers#1", "1" * 40, second)
        call = bindings["calls"][0]
        self.assertEqual(("sqlserver-internal-helper-result-bindings/1", 1, "dbo.ReadOther"), (bindings["version"], call["callerIndex"], call["commandText"]))
        self.assertEqual(SMOKE.HELPERS.digest(second), call["internalHelperResultEvidenceSha256"])
        self.assertNotIn("helperResultEvidenceSha256", call)

    def test_state_and_path_drift_and_invalid_aggregate_never_publish_a_receipt(self):
        self.assertIsNone(SMOKE.check_helper_state("ReadTable", fact("ReadTable", 10)))
        for bad in (fact("ReadTable", 10, phase4_gap="helper-recursion"), fact("ReadTable", 10, state(coverage="unknown", gap="helper-compilation-errors"))):
            with self.assertRaises(ValueError):
                SMOKE.check_helper_state("ReadTable", bad)
        unknown = state(coverage="unknown", gap="helper-reference-escape")
        self.assertEqual("helper-reference-escape", SMOKE.check_helper_state("CrossDelegateHelper", fact("CrossDelegateHelper", 10, unknown)))
        with self.assertRaises(ValueError):
            SMOKE.check_helper_state("GeneratedCallerHelper", fact("GeneratedCallerHelper", 10, unknown))
        with self.assertRaises(ValueError):  # Phase 4 must keep the private helper bounded.
            SMOKE.check_helper_state("PrivateHelper", fact("PrivateHelper", 10, state(coverage="unknown", gap="helper-private-phase4-scope")))
        self.assertIsNone(SMOKE.check_caller_state("CrossTableName", CALLER, "csharp"))
        with self.assertRaises(ValueError):
            SMOKE.check_caller_state("CrossTableName", dict(CALLER, path="csharp/MoreCallers.cs"), "csharp")
        with self.assertRaises(ValueError):
            SMOKE.check_caller_state("SameDocumentName", CALLER, "csharp")
        self.assertIsNone(SMOKE.check_caller_state("CrossTableName", dict(CALLER, path="vb/Callers.vb"), "vb"))
        good = {"gaps": [], "observedCounts": {M: 1}, "expectedCounts": {M: 1}, "refusals": [{"refused": True}] * SMOKE.REFUSAL_COUNT}
        for change in ({"gaps": ["missing:caller:CrossTableName"]}, {"observedCounts": {M: 2}}, {"refusals": []},
                       {"refusals": [{"refused": False}] * SMOKE.REFUSAL_COUNT}):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as tmp:
                with mock.patch.object(SMOKE.HELPERS, "verify_scan_hashes") as verify:
                    with self.assertRaises(ValueError):
                        SMOKE.publish_receipt(Path(tmp), dict(good, **change))
                    verify.assert_not_called()
                self.assertFalse((Path(tmp) / "smoke-receipt.json").exists())

    def test_report_status_path_or_contract_drift_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            schema = root / "schema.json"
            SMOKE.HELPERS.write(schema, {"version": "x"})
            row = {"factId": "f", "callerIndex": 1, "callerPath": "csharp/Callers.cs", "status": X, "reasons": [], "ruleId": SMOKE.RULE,
                   "tier": "Tier2Structural", "sourceTier": "Tier1Semantic"}
            other = dict(row, callerIndex=0, status=U, tier="Tier4Unknown", reasons=["binding-missing"])
            report = {"version": "sqlserver-internal-helper-result-comparison/1", "consumerVersion": "sqlserver-internal-helper-result/1",
                      "bindingsVersion": "sqlserver-internal-helper-result-bindings/1", "schemaRoute": "result-schema",
                      "origin": "independent-synthetic-result-declarations", "coverage": "partial", "verdict": "no-aggregate-clean-verdict",
                      "generatorSha256": "a" * 64, "schemaEvidenceSha256": SMOKE.HELPERS.digest({"version": "x"}), "comparisons": [other, row]}
            for name in ("report", "repeat"):
                (root / name).mkdir()
                SMOKE.HELPERS.write(root / name / SMOKE.REPORT, report)
                (root / name / "report.md").write_text("No aggregate clean verdict binding-missing csharp/Callers.cs")
            args = ({"factId": "f"}, 1, [X], "result-schema", None, "a" * 64, schema, "csharp/Callers.cs")
            self.assertEqual([X], SMOKE.check_report(root, *args)[0])
            for index, expected, origin, path in ((1, [M], "result-schema", "csharp/Callers.cs"), (1, [X], "ddl-result", "csharp/Callers.cs"),
                                                  (1, [X], "result-schema", "csharp/MoreCallers.cs")):
                with self.subTest(index=index, expected=expected, origin=origin, path=path), self.assertRaises(ValueError):
                    SMOKE.check_report(root, {"factId": "f"}, index, expected, origin, None, "a" * 64, schema, path)


if __name__ == "__main__":
    unittest.main()
