"""Public synthetic Field<T> read smoke guards; no CLI, network, or SQL execution."""
import ast
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest import mock
ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("field_read_smoke", ROOT / "scripts/sqlserver-field-read-evidence/smoke.py")
SMOKE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SMOKE)
M, X, U = "bounded-match", "mismatch", "unknown"
SAMPLE = ROOT / "samples/sqlserver-field-read-evidence"
FIELD = {"kind": "field-name", "member": "Field", "rowSource": "rows-index", "rowIndex": 0, "name": "Name", "ordinal": None,
         "typeArgument": "System.String", "line": 20, "endLine": 20, "gap": None}
SITE = {"path": "csharp/Callers.cs", "line": 20, "endLine": 20, "caller": "FieldCallers.InternalFieldName(string, System.Data.DataColumn, bool)",
        "coverage": "bounded", "gap": None, "commandTextHash": "a" * 64, "selectors": [], "guards": [], "accesses": [FIELD]}


def state(sites=(), **fields):
    value = {"version": "sqlserver-field-read/1", "coverage": "bounded", "gap": None, "origin": "internal-helper", "originGaps": [],
             "resultKind": "datatable", "commandType": "StoredProcedure", "commandTextSource": "constructor-parameter",
             "adapterAssociation": "constructor-command", "fillOverload": "fill-datatable", "tableName": None, "sites": list(sites)}
    value.update(fields)
    return value


def fact(name, line, field=None, table_gap="datarow-field-extension-unsupported", phase4=None, internal_callers=(), path="csharp/Tables.cs"):
    properties = {"operationKind": "data-adapter-fill",
                  "sqlServerDataTableState": json.dumps({"version": "sqlserver-datatable/1", "coverage": "unknown" if table_gap else "bounded",
                                                         "gap": table_gap, "accesses": []}),
                  "sqlServerDataSetState": json.dumps({"version": "sqlserver-dataset/1", "coverage": "unknown", "gap": "fill-overload-unsupported", "accesses": []}),
                  "sqlServerHelperResultState": json.dumps(phase4 or {"version": "sqlserver-helper-result/2", "coverage": "unknown",
                                                                      "gap": "helper-accessibility-unsupported", "callers": []}),
                  "sqlServerInternalHelperResultState": json.dumps({"version": "sqlserver-internal-helper-result/1", "coverage": "bounded",
                                                                    "gap": None, "callers": list(internal_callers)}),
                  "sqlServerFieldReadState": json.dumps(field if field is not None else state())}
    return {"factId": f"id-{name}-{line}", "repo": "synthetic", "commitSha": "1" * 40, "ruleId": "database.operation.call-pattern.v1",
            "evidenceTier": "Tier1Semantic", "sourceSymbol": f"Tables.{name}(string, System.Data.DataColumn, bool)",
            "evidence": {"filePath": path, "startLine": line, "endLine": line, "extractorId": "CSharpSemanticExtractor",
                         "extractorVersion": "csharp-semantic/0.22.20"}, "properties": properties}


def direct(name, **site):
    one = dict(SITE, path=None, caller=f"Tables.{name}(string, System.Data.DataColumn, bool)", **site)
    return state([one], origin="datatable", commandTextSource=None)


class FieldReadSmokeGuards(unittest.TestCase):
    def test_sql_injection_suppressions_are_rule_specific_static_fixture_controls(self):
        lines = []
        for path in sorted(SAMPLE.glob("csharp/**/*.cs")):
            if "obj" in path.parts or "bin" in path.parts:
                continue  # Build outputs are not fixture sources.
            text = path.read_text()
            self.assertIn("never invoke methods.", text)
            lines += [line for line in text.splitlines() if "nosemgrep" in line]
        self.assertEqual(4, len(lines))
        for line in lines:
            self.assertRegex(line, r"^var command = new SqlCommand\(procedure\); // nosemgrep: csharp\.lang\.security\.sqli\.csharp-sqli$")
        for language, extension in (("csharp", "csproj"), ("vb", "vbproj")):
            project = ET.parse(SAMPLE / language / f"FieldReads.{extension}")
            self.assertFalse(any(n.text in ("Exe", "WinExe") for n in project.iter("OutputType")))
            self.assertEqual([], list(project.iter("Compile")))

    def test_fixture_documents_and_method_names_match_across_languages(self):
        names = {}
        for language, extension, pattern in (("csharp", "cs", r"^(?:public|private|internal) static (?!void)\w+ (\w+)\("),
                                             ("vb", "vb", r"^(?:Public|Private|Friend) Function (\w+)\(")):
            files = sorted(p.relative_to(SAMPLE / language).as_posix() for p in (SAMPLE / language).rglob(f"*.{extension}")
                           if "obj" not in p.parts and "bin" not in p.parts)
            self.assertEqual([f"{n}.{extension}" for n in ("Callers", "Helpers", "Sets", "Tables")], files)
            names[language] = [m[1] for f in files for m in re.finditer(pattern, (SAMPLE / language / f).read_text(), re.M)]
        self.assertEqual(names["csharp"], names["vb"])
        authored = set(SMOKE.CORE_FILL_CASES) | set(SMOKE.STATE_CASES) | set(SMOKE.CORE_SITE_CASES) | set(SMOKE.SITE_CASES) | set(SMOKE.HELPER_FILLS)
        self.assertEqual(authored | {"UseReadBody"}, set(names["csharp"]))

    def test_native_fixtures_are_in_the_normal_test_build_graph(self):
        project = ROOT / "src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj"
        references = {(project.parent / node.attrib["Include"].replace("\\", "/")).resolve(): node
                      for node in ET.parse(project).iter("ProjectReference")}
        for language, extension in (("csharp", "csproj"), ("vb", "vbproj")):
            fixture = SAMPLE / language / f"FieldReads.{extension}"
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
        self.assertEqual({M: 39, X: 18, U: 21}, SMOKE.expected_totals())
        plan = SMOKE.case_plan()
        self.assertEqual(75, len(plan))
        self.assertEqual(21 * 3, sum(1 for _, key, *_ in plan if key in SMOKE.CORE_FILL_CASES or key in SMOKE.CORE_SITE_CASES))
        self.assertEqual((9, 3, 26, 26), (len(SMOKE.STATE_CASES), len(SMOKE.SITE_CASES), SMOKE.REFUSAL_COUNT, SMOKE.SITES_PER_LANGUAGE))
        self.assertTrue(all(origin == "ddl-table-result" and statuses == [U] for _, key, origin, _, statuses in plan
                            if key in SMOKE.STATE_CASES or key in SMOKE.SITE_CASES))
        self.assertFalse((set(SMOKE.CORE_FILL_CASES) | set(SMOKE.STATE_CASES)) & (set(SMOKE.CORE_SITE_CASES) | set(SMOKE.SITE_CASES)))
        self.assertEqual([M, M], SMOKE.CORE_FILL_CASES["TableFieldMixed"][0])
        self.assertEqual(sum(len(s) for _, s in SMOKE.HELPER_FILLS.values()), len(SMOKE.CORE_SITE_CASES) + len(SMOKE.SITE_CASES))
        self.assertEqual({"HelperFieldOtherProcedure"}, SMOKE.OTHER_PROCEDURE)

    def test_site_keys_follow_state_order_and_gaps_are_explicit(self):
        second = dict(SITE, line=24, caller="FieldCallers.InternalFieldOrdinal(string, System.Data.DataColumn, bool)")
        helper = fact("ReadShared", 10, state([SITE, second]), path="csharp/Helpers.cs")
        fills = SMOKE.fill_keys([fact("TableFieldName", 3, direct("TableFieldName")), helper])
        self.assertEqual(["ReadShared", "TableFieldName"], [k for k, _ in fills])
        sites = SMOKE.site_keys(fills)
        self.assertEqual([("InternalFieldName", 0), ("InternalFieldOrdinal", 1)], [(k, v[1]) for k, v in sites])
        gaps = SMOKE.case_gaps(["ReadShared", "Surprise"], ["InternalFieldName", "InternalFieldName"])
        self.assertIn("missing:fill:ReadPrivate", gaps)
        self.assertIn("unexpected:fill:Surprise", gaps)
        self.assertIn("duplicate:site:InternalFieldName", gaps)

    def test_field_digest_is_contract_tagged_per_site_covers_the_type_argument_and_differs_from_other_routes(self):
        second = dict(SITE, line=24, commandTextHash="b" * 64)
        item = fact("ReadShared", 10, state([SITE, second]))
        first, other = SMOKE.field_projection(item, 0), SMOKE.field_projection(item, 1)
        self.assertEqual(("sqlserver-field-read-bindings/1", 0, SITE, []), (first["contract"], first["siteIndex"], first["site"], first["fieldState"]["sites"]))
        self.assertEqual(list(first)[-3:], ["siteIndex", "fieldState", "site"])
        self.assertNotEqual(SMOKE.HELPERS.digest(first), SMOKE.HELPERS.digest(other))
        retyped = fact("ReadShared", 10, state([dict(SITE, accesses=[dict(FIELD, typeArgument="System.Int32")]), second]))
        self.assertNotEqual(SMOKE.HELPERS.digest(first), SMOKE.HELPERS.digest(SMOKE.field_projection(retyped, 0)))
        for projection in (SMOKE.PHASE5.internal_projection(item, 0), SMOKE.PHASE4.helper_projection(item, 0), SMOKE.DATASET.dataset_projection(item),
                           SMOKE.DATASET.datatable_projection(item), SMOKE.RESULT.reader_projection(item)):
            self.assertNotEqual(SMOKE.HELPERS.digest(first), SMOKE.HELPERS.digest(projection))
        bindings = SMOKE.binding(item, 1, "HelperFieldOtherProcedure", "1" * 40, other)
        call = bindings["calls"][0]
        self.assertEqual(("sqlserver-field-read-bindings/1", 1, "dbo.ReadOther"), (bindings["version"], call["siteIndex"], call["commandText"]))
        self.assertEqual(SMOKE.HELPERS.digest(other), call["fieldReadEvidenceSha256"])
        self.assertNotIn("callerIndex", call)
        self.assertEqual([], SMOKE.binding(item, None, "TableFieldColumn", "1" * 40, first)["calls"])

    def test_state_drift_and_invalid_aggregate_never_publish_a_receipt(self):
        self.assertEqual((0, None), SMOKE.check_fill_state("TableFieldName", fact("TableFieldName", 3, direct("TableFieldName"))))
        absent = direct("TableNoField", coverage="unknown", gap="field-read-absent", commandTextHash=None, accesses=[])
        self.assertEqual((0, "field-read-absent"), SMOKE.check_fill_state("TableNoField", fact("TableNoField", 3, absent, table_gap=None)))
        unbound = state(coverage="unknown", gap="field-read-origin-unbound", origin=None, originGaps=[
            "datatable:datacolumn-access-unsupported", "dataset:fill-overload-unsupported", "helper:helper-return-required", "internal-helper:helper-return-required"])
        self.assertEqual((None, "datatable:datacolumn-access-unsupported"), SMOKE.check_fill_state("TableFieldColumn", fact("TableFieldColumn", 3, unbound)))
        for key, bad in (("TableFieldName", fact("TableFieldName", 3, direct("TableFieldName", accesses=[dict(FIELD, member="Item", kind="name-indexer")]))),
                         ("TableFieldName", fact("TableFieldName", 3, direct("TableFieldName"), table_gap=None)),  # Phase 2 must keep refusing Field.
                         ("TableFieldName", fact("TableFieldName", 3, direct("TableFieldName", accesses=[dict(FIELD, typeArgument=None)]))),
                         ("TableNoField", fact("TableNoField", 3, absent)),  # Phase 2 must stay bounded without a Field read.
                         ("TableFieldVersion", fact("TableFieldVersion", 3, unbound)),
                         ("TableFieldColumn", fact("TableFieldColumn", 3, dict(unbound, originGaps=list(reversed(unbound["originGaps"])))))):
            with self.subTest(key=key), self.assertRaises(ValueError):
                SMOKE.check_fill_state(key, bad)
        internal = fact("ReadShared", 10, state([SITE]), internal_callers=[{"line": 20, "caller": SITE["caller"], "coverage": "unknown",
                                                                           "gap": "datarow-field-extension-unsupported"}])
        self.assertIsNone(SMOKE.check_site_state("InternalFieldName", internal, 0, SITE, "csharp"))
        for args in (("InternalFieldName", internal, 0, dict(SITE, path="csharp/Helpers.cs"), "csharp"),
                     ("InternalFieldName", internal, 0, SITE, "vb"),
                     ("InternalNoField", internal, 0, SITE, "csharp")):
            with self.subTest(args=args[0]), self.assertRaises(ValueError):
                SMOKE.check_site_state(*args)
        good = {"gaps": [], "observedCounts": {M: 1}, "expectedCounts": {M: 1},
                "refusals": [{"language": language, "refused": True} for language in ("csharp", "vb") for _ in range(SMOKE.REFUSAL_COUNT)]}
        for change in ({"gaps": ["missing:site:InternalFieldName"]}, {"observedCounts": {M: 2}}, {"refusals": []},
                       {"refusals": [{"refused": False}] * SMOKE.REFUSAL_COUNT},
                       {"refusals": [{"language": "csharp", "refused": True}] * (2 * SMOKE.REFUSAL_COUNT)}):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as tmp:
                with mock.patch.object(SMOKE.HELPERS, "verify_scan_hashes") as verify:
                    with self.assertRaises(ValueError):
                        SMOKE.publish_receipt(Path(tmp), dict(good, **change))
                    verify.assert_not_called()
                self.assertFalse((Path(tmp) / "smoke-receipt.json").exists())

    def test_report_status_path_facet_or_contract_drift_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            schema = root / "schema.json"
            SMOKE.HELPERS.write(schema, {"version": "x"})
            row = {"factId": "f", "siteIndex": 1, "sitePath": "csharp/Callers.cs", "status": X, "reasons": [], "ruleId": SMOKE.RULE,
                   "tier": "Tier2Structural", "sourceTier": "Tier1Semantic", "access": FIELD, "uncomparedFacets": ["field-type-argument-not-compared:System.String"]}
            other = dict(row, siteIndex=0, status=U, tier="Tier4Unknown", reasons=["binding-missing"], access=None, uncomparedFacets=[])
            report = {"version": "sqlserver-field-read-comparison/1", "consumerVersion": "sqlserver-field-read/1",
                      "bindingsVersion": "sqlserver-field-read-bindings/1", "schemaRoute": "result-schema",
                      "origin": "independent-synthetic-result-declarations", "coverage": "partial", "verdict": "no-aggregate-clean-verdict",
                      "generatorSha256": "a" * 64, "schemaEvidenceSha256": SMOKE.HELPERS.digest({"version": "x"}), "comparisons": [other, row]}

            def publish(document):
                for name in ("report", "repeat"):
                    (root / name).mkdir(exist_ok=True)
                    SMOKE.HELPERS.write(root / name / SMOKE.REPORT, document)
                    (root / name / "report.md").write_text("No aggregate clean verdict binding-missing")
            publish(report)
            args = ({"factId": "f"}, 1, [X], "result-schema", None, "a" * 64, schema, "csharp/Callers.cs")
            self.assertEqual([X], SMOKE.check_report(root, *args)[0])
            for index, expected, origin, path in ((1, [M], "result-schema", "csharp/Callers.cs"), (1, [X], "ddl-result", "csharp/Callers.cs"),
                                                  (1, [X], "result-schema", None)):
                with self.subTest(index=index, expected=expected, origin=origin, path=path), self.assertRaises(ValueError):
                    SMOKE.check_report(root, {"factId": "f"}, index, expected, origin, None, "a" * 64, schema, path)
            publish(dict(report, comparisons=[other, dict(row, uncomparedFacets=[])]))
            with self.assertRaises(ValueError):  # A Field row must carry its never-compared type-argument facet.
                SMOKE.check_report(root, *args)


if __name__ == "__main__":
    unittest.main()
