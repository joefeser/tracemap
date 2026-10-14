import importlib.util
import itertools
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "prepare-vb-compilation-context.py"
spec = importlib.util.spec_from_file_location("prepare", SCRIPT)
prepare = importlib.util.module_from_spec(spec)
spec.loader.exec_module(prepare)


class PreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(dir="/private/tmp" if sys.platform == "darwin" else None)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        self.source = self.repo / "Worker.vb"
        self.source.write_text("Public Class Worker\nEnd Class")
        self.refs = self.root / "refs"
        self.refs.mkdir()
        (self.refs / "sample.dll").write_bytes(b"candidate bytes; scanner independently validates PE metadata")
        for args in (("init",), ("add", "."), ("-c", "user.name=Synthetic", "-c", "user.email=test@example.invalid", "commit", "-m", "fixture")):
            subprocess.run(["git", "-C", str(self.repo), *args], check=True, capture_output=True)
        self.proposal = self.root / "proposal.json"
        self.context = self.root / "context.json"

    def draft(self, extra=()):
        return prepare.main(["draft", "--repo", str(self.repo), "--source-dir", str(self.repo),
            "--reference-dir", str(self.refs), "--root-namespace", "", "--option-strict", "off",
            "--option-infer", "on", "--option-explicit", "on", "--out", str(self.proposal), *extra])

    def approve(self):
        return prepare.main(["approve", "--proposal", str(self.proposal), "--operator-approved", "--out", str(self.context)])

    def test_proposal_is_not_context_and_approval_hashes_exact_payload(self):
        self.assertEqual(0, self.draft())
        proposal = prepare.unpack(self.proposal, "vb-compilation-proposal.v1")
        self.assertEqual("not-established", proposal["originalBuildCorrespondence"])
        self.assertEqual("Worker.vb", proposal["context"]["sources"][0]["path"])
        self.assertEqual(0, self.approve())
        context = prepare.unpack(self.context, "prepared-vb-compilation-context.v1")
        self.assertEqual(proposal["context"], context)

    def test_changed_source_refuses_approval(self):
        self.assertEqual(0, self.draft())
        self.source.write_text("changed")
        self.assertEqual(1, self.approve())
        self.assertFalse(self.context.exists())

    def test_changed_reference_refuses_approval(self):
        self.assertEqual(0, self.draft())
        (self.refs / "sample.dll").write_bytes(b"changed")
        self.assertEqual(1, self.approve())

    def test_changed_payload_refuses_approval(self):
        self.assertEqual(0, self.draft())
        data = json.loads(self.proposal.read_text())
        data["payloadJson"] += " "
        self.proposal.write_text(json.dumps(data))
        self.assertEqual(1, self.approve())

    def test_no_overwrite(self):
        self.assertEqual(0, self.draft())
        before = self.proposal.read_bytes()
        self.assertEqual(1, self.draft())
        self.assertEqual(before, self.proposal.read_bytes())

    def test_no_output_in_repo(self):
        self.proposal = self.repo / "proposal.json"
        self.assertEqual(1, self.draft())
        self.assertFalse(self.proposal.exists())

    def test_empty_reference_roster_refused(self):
        (self.refs / "sample.dll").unlink()
        self.assertEqual(1, self.draft())

    def test_discovery_bound(self):
        for i in range(4097):
            (self.refs / str(i)).touch()
        self.assertEqual(1, self.draft())

    def test_sources_outside_repo_refused(self):
        (self.refs / "Other.vb").write_text("Class Other\nEnd Class")
        self.assertEqual(1, self.draft(["--source-dir", str(self.refs)]))

    def test_deterministic_proposal(self):
        self.assertEqual(0, self.draft())
        before = self.proposal.read_bytes()
        self.proposal.unlink()
        self.assertEqual(0, self.draft())
        self.assertEqual(before, self.proposal.read_bytes())

    def test_option_matrix_preserves_all_32_explicit_combinations(self):
        for strict, infer, explicit, root, imports in itertools.product(
                ("on", "off"), ("on", "off"), ("on", "off"), ("", "Synthetic"), (False, True)):
            with self.subTest(strict=strict, infer=infer, explicit=explicit, root=root, imports=imports):
                extra = ["--option-strict", strict, "--option-infer", infer, "--option-explicit", explicit,
                         "--root-namespace", root]
                if imports:
                    extra += ["--import", "System"]
                self.assertEqual(0, self.draft(extra))
                self.assertEqual(0, self.approve())
                value = prepare.unpack(self.context, "prepared-vb-compilation-context.v1")
                self.assertEqual((strict == "on", infer == "on", explicit == "on", root, ["System"] if imports else []),
                    (value["optionStrict"], value["optionInfer"], value["optionExplicit"], value["rootNamespace"], value["imports"]))
                self.context.unlink()
                self.proposal.unlink()

    def test_stale_commit_refuses_approval(self):
        self.assertEqual(0, self.draft())
        subprocess.run(["git", "-C", str(self.repo), "-c", "user.name=Synthetic", "-c", "user.email=test@example.invalid",
                        "commit", "--allow-empty", "-m", "new head"], check=True, capture_output=True)
        self.assertEqual(1, self.approve())


if __name__ == "__main__":
    unittest.main()
