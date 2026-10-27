import importlib.util
import pathlib
import unittest


PATH = pathlib.Path(__file__).with_name("sql-compiled-wrapper-smoke.py")
SPEC = importlib.util.spec_from_file_location("compiled_wrapper_smoke", PATH)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class CompiledWrapperSmokeGuards(unittest.TestCase):
    def test_finite_independent_denominator(self):
        self.assertEqual(list(MODULE.CASES), ["Direct", "Forwarded", "FieldObject", "PropertyObject",
                                            "Mutated", "Escaped", "Branch", "MergeFresh", "MergeExisting", "MergeMutated"])
        self.assertEqual(MODULE.CASES["MergeExisting"], ("Direct", "existing-destination-schema-unavailable"))
        self.assertEqual(MODULE.CASES["FieldObject"], ("FieldObject", "constructor-field-forwarding-unavailable"))

    def test_missing_native_evidence_fails(self):
        with self.assertRaises(ValueError):
            MODULE.inspect([], "csharp", {"caller": "a", "wrapper": "b"})

    def test_duplicate_native_identity_fails(self):
        with self.assertRaisesRegex(ValueError, "duplicate fact identity"):
            MODULE.inspect([{"factId": "same"}, {"factId": "same"}], "vb", {})

    def test_ambiguous_and_absent_selection_fail(self):
        for values in ([], [1, 2]):
            with self.assertRaises(ValueError):
                MODULE.one(values, "test")
        self.assertEqual(MODULE.one([3], "test"), 3)


if __name__ == "__main__":
    unittest.main()
