#!/usr/bin/env python3

from __future__ import annotations

import hashlib
import importlib.util
import io
import json
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path


def load(name: str):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + ".py"))
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


GENERATOR = load("generate_dotnet_test_class_weights")
SELECTOR = load("select_dotnet_test_lane")

# Element order follows a real xunit trx (results before definitions); a theory case carries
# its arguments in testName only, and a skipped case is a NotExecuted result with a duration.
TRX = """<?xml version="1.0" encoding="utf-8"?>
<TestRun id="run" name="unit" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Times creation="2026-01-01T00:00:00.0000000-05:00" queuing="2026-01-01T00:00:00.0000000-05:00"
         start="2026-01-01T00:00:01.0000000-05:00" finish="2026-01-01T00:10:00.0000000-05:00" />
  <Results>
    <UnitTestResult testId="id-a" testName="One" duration="00:00:01.5000000" outcome="Passed" />
    <UnitTestResult testId="id-b1" testName="One(x: 1)" duration="00:01:02.2500000" outcome="Passed" />
    <UnitTestResult testId="id-b2" testName="Two" duration="00:00:00.0010000" outcome="NotExecuted" />
    <UnitTestResult testId="id-b3" testName="Three" outcome="Passed" />
  </Results>
  <TestDefinitions>
    <UnitTest name="One" id="id-a"><TestMethod className="TraceMap.Tests.Alpha" name="One" /></UnitTest>
    <UnitTest name="One(x: 1)" id="id-b1"><TestMethod className="TraceMap.Tests.Beta" name="One" /></UnitTest>
    <UnitTest name="Two" id="id-b2"><TestMethod className="TraceMap.Tests.Beta" name="Two" /></UnitTest>
    <UnitTest name="Three" id="id-b3"><TestMethod className="TraceMap.Tests.Beta" name="Three" /></UnitTest>
  </TestDefinitions>
  <ResultSummary outcome="Completed">
    <Counters total="4" executed="3" passed="3" failed="0" error="0" timeout="0" aborted="0" notExecuted="1" />
  </ResultSummary>
</TestRun>
"""

CLASSES = {
    "TraceMap.Tests.Alpha": {"seconds": 1.5, "tests": 1},
    "TraceMap.Tests.Beta": {"seconds": 62.251, "tests": 3},
}


class GenerateDotnetTestClassWeightsTests(unittest.TestCase):
    def write_trx(self, directory: Path, text: str = TRX) -> Path:
        path = directory / "run.trx"
        path.write_text(text, encoding="utf-8")
        return path

    def test_projects_class_durations_and_case_counts(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            classes, run = GENERATOR.project_trx(self.write_trx(Path(temp)))
        self.assertEqual(CLASSES, classes)
        self.assertEqual({"started": "2026-01-01T00:00:01.0000000-05:00", "finished": "2026-01-01T00:10:00.0000000-05:00",
                          "total": 4, "executed": 3, "passed": 3, "failed": 0}, run)

    def test_duration_parsing(self) -> None:
        self.assertEqual(62.25, GENERATOR.duration_seconds("00:01:02.25"))
        self.assertEqual(3600.0, GENERATOR.duration_seconds("01:00:00"))

    def test_table_hashes_only_the_projected_class_map(self) -> None:
        table = GENERATOR.build_table(CLASSES, {"total": 4}, "abc1234", "test-runner")
        self.assertEqual(GENERATOR.SCHEMA_VERSION, table["schemaVersion"])
        self.assertEqual(hashlib.sha256(GENERATOR.canonical(CLASSES)).hexdigest(), table["boundedInputSha256"])
        self.assertEqual(hashlib.sha256(Path(GENERATOR.__file__).read_bytes()).hexdigest(), table["generatorSha256"])
        self.assertEqual({"commit": "abc1234", "runner": "test-runner", "total": 4}, table["source"])
        self.assertEqual(CLASSES, table["classes"])
        self.assertNotIn("trx", json.dumps(table).lower())

    def test_main_writes_a_table_the_selector_loads(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            out = directory / "weights.json"
            stdout = io.StringIO()
            with redirect_stdout(stdout):
                exit_code = GENERATOR.main([str(self.write_trx(directory)), "--commit", "abc1234",
                                            "--runner", "test-runner", "--out", str(out)])
            self.assertEqual(0, exit_code)
            self.assertIn("2 classes, 4 test cases, 1.1 test-minutes", stdout.getvalue())
            self.assertIn("TraceMap.Tests.Beta", stdout.getvalue())
            self.assertTrue(out.read_text(encoding="utf-8").endswith("}\n"))
            self.assertEqual(
                (SELECTOR.ClassWeight("TraceMap.Tests.Alpha", 1.5, 1), SELECTOR.ClassWeight("TraceMap.Tests.Beta", 62.251, 3)),
                SELECTOR.load_class_weights(out).classes)

    def test_malformed_trx_is_refused(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            orphan = TRX.replace('testId="id-a"', 'testId="id-missing"')
            with self.assertRaises(ValueError):
                GENERATOR.project_trx(self.write_trx(directory, orphan))
            empty = TRX.split("<Results>")[0] + "<Results /><TestDefinitions /></TestRun>"
            with self.assertRaises(ValueError):
                GENERATOR.project_trx(self.write_trx(directory, empty))

    def test_document_type_declarations_are_refused_before_parsing(self) -> None:
        # No trx has a DTD; refusing one is what closes external entities and entity expansion.
        doctype = TRX.replace("<TestRun ", '<!DOCTYPE TestRun [<!ENTITY x "y">]>\n<TestRun ', 1)
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaises(ValueError) as refused:
                GENERATOR.project_trx(self.write_trx(Path(temp), doctype))
        self.assertIn("document type declaration", str(refused.exception))

    def test_shard_trx_files_merge_into_one_run(self) -> None:
        second = (TRX.replace("TraceMap.Tests.Alpha", "TraceMap.Tests.Gamma")
                  .replace("TraceMap.Tests.Beta", "TraceMap.Tests.Delta")
                  .replace('start="2026-01-01T00:00:01', 'start="2026-01-01T00:00:00')
                  .replace('finish="2026-01-01T00:10:00', 'finish="2026-01-01T00:12:00'))
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            other = directory / "other.trx"
            other.write_text(second, encoding="utf-8")
            classes, run = GENERATOR.project_trx_files([self.write_trx(directory), other])
        self.assertEqual(["TraceMap.Tests.Alpha", "TraceMap.Tests.Beta", "TraceMap.Tests.Delta", "TraceMap.Tests.Gamma"],
                         list(classes))
        self.assertEqual(CLASSES["TraceMap.Tests.Beta"], classes["TraceMap.Tests.Delta"])
        self.assertEqual({"started": "2026-01-01T00:00:00.0000000-05:00", "finished": "2026-01-01T00:12:00.0000000-05:00",
                          "total": 8, "executed": 6, "passed": 6, "failed": 0}, run)

    def test_overlapping_shard_trx_files_are_refused(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            trx = self.write_trx(Path(temp))
            with self.assertRaises(ValueError) as refused:
                GENERATOR.project_trx_files([trx, trx])
        self.assertIn("shards overlap", str(refused.exception))

    def test_checked_in_table_matches_its_own_stamp(self) -> None:
        table = json.loads(GENERATOR.DEFAULT_OUT.read_text(encoding="utf-8"))
        self.assertEqual(GENERATOR.SCHEMA_VERSION, table["schemaVersion"])
        self.assertEqual(hashlib.sha256(GENERATOR.canonical(table["classes"])).hexdigest(), table["boundedInputSha256"])
        self.assertEqual(GENERATOR.BOUNDED_INPUT_KIND, table["boundedInputKind"])
        self.assertTrue(table["source"]["commit"])
        self.assertEqual(sum(entry["tests"] for entry in table["classes"].values()), table["source"]["total"])


if __name__ == "__main__":
    unittest.main()
