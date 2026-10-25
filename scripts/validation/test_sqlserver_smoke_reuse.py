"""Public synthetic scan reuse regressions; no CLI, network, or SQL execution."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location(
    "smoke", Path(__file__).resolve().parents[1] / "sqlserver-parameter-evidence/smoke.py")
SMOKE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SMOKE)


class ScanReuseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for language in ("csharp", "vb"):
            for name in SMOKE.SCAN_FILES:
                path = self.root / language / "scan" / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("synthetic " + name)
        self.receipt = {"scanArtifactSha256": {
            language: SMOKE.scan_hashes(self.root, language) for language in ("csharp", "vb")}}

    def test_unchanged_artifacts_match(self):
        self.assertEqual(self.receipt["scanArtifactSha256"], SMOKE.verify_scan_hashes(self.root, self.receipt))

    def test_each_artifact_rejects_mutation_missing_and_symlink(self):
        for language in ("csharp", "vb"):
            for name in SMOKE.SCAN_FILES:
                path = self.root / language / "scan" / name
                original = path.read_bytes()
                for mode in ("mutation", "missing", "symlink"):
                    with self.subTest(language=language, artifact=name, mode=mode):
                        path.unlink()
                        if mode == "mutation":
                            path.write_bytes(original + b"changed")
                        elif mode == "symlink":
                            target = self.root / "same-bytes"
                            target.write_bytes(original)
                            path.symlink_to(target)
                        with self.assertRaises(ValueError):
                            SMOKE.verify_scan_hashes(self.root, self.receipt)
                        if path.exists() or path.is_symlink():
                            path.unlink()
                        path.write_bytes(original)

    def test_directory_links_rejected(self):
        for relative in ("csharp", "csharp/scan", "csharp/scan/logs"):
            with self.subTest(relative=relative):
                path = self.root / relative
                target = self.root / "moved"
                path.rename(target)
                path.symlink_to(target, target_is_directory=True)
                with self.assertRaises(ValueError):
                    SMOKE.verify_scan_hashes(self.root, self.receipt)
                path.unlink()
                target.rename(path)
        link = self.root / "root-link"
        link.symlink_to(self.root, target_is_directory=True)
        with self.assertRaises(ValueError):
            SMOKE.verify_scan_hashes(link, self.receipt)

    def test_old_or_incomplete_receipt_rejected(self):
        for receipt in ({}, {"scanArtifactSha256": {}},
                        {"scanArtifactSha256": {"csharp": self.receipt["scanArtifactSha256"]["csharp"]}}):
            with self.subTest(receipt=receipt), self.assertRaises(ValueError):
                SMOKE.verify_scan_hashes(self.root, receipt)


if __name__ == "__main__":
    unittest.main()
