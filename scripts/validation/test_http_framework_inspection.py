#!/usr/bin/env python3
"""Public synthetic guards for the offline work-machine XML/metadata inspector."""
import hashlib
import pathlib
import subprocess
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
TOOL = ROOT / "scripts/diagnostics/InspectHttpFramework/bin/Debug/net10.0/InspectHttpFramework.dll"

class InspectionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        projects = ["scripts/diagnostics/InspectHttpFramework/InspectHttpFramework.csproj",
                    "samples/outbound-http-coverage/legacy/net20/csharp/HttpLegacy.csproj",
                    "samples/outbound-http-coverage/legacy/net35/csharp/HttpLegacy.csproj",
                    "samples/outbound-http-coverage/legacy/net48/vb/HttpLegacy.vbproj",
                    "samples/outbound-http-coverage/csharp/HttpMatrix.CSharp.csproj"]
        for project in projects:
            subprocess.run(["dotnet", "build", str(ROOT / project), "-warnaserror", "-v:q"], check=True, capture_output=True)

    def collect(self, folder, options, success=True, out=None):
        output = out or folder / "local-report.md"
        result = subprocess.run(["dotnet", str(TOOL), "--repo-label", "synthetic-local", "--commit-sha", "unknown", "--out", str(output)] + options,
                                capture_output=True, text=True, cwd=ROOT)
        self.assertEqual(result.returncode, 0 if success else 2, result.stdout + result.stderr)
        return output.read_text(encoding="utf-8") if success else result.stderr

    def binary(self, tfm, language="csharp"):
        if tfm == "net10.0":
            return ROOT / "samples/outbound-http-coverage/csharp/bin/Debug/net10.0/HttpMatrix.CSharp.dll"
        return ROOT / f"samples/outbound-http-coverage/legacy/{tfm}/{language}/bin/Debug/{tfm}/HttpLegacy.{tfm}.{language}.dll"

    def test_sources_stay_separate_conditional_and_conflicting_values_are_retained(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = pathlib.Path(tmp)
            project = folder / "private-name.csproj"
            config = folder / "Web.config"
            project.write_text('<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><PropertyGroup Condition="DO_NOT_SHARE_TEST_SECRET"><TargetFrameworkVersion>v3.5</TargetFrameworkVersion><TargetFramework>$(DO_NOT_SHARE_TEST_SECRET)</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Synthetic"><TargetFramework>net20</TargetFramework></Reference></ItemGroup><Import Project="/private/synthetic-identity" /></Project>')
            config.write_text('<configuration><connectionStrings><add connectionString="DO_NOT_SHARE_TEST_SECRET" /></connectionStrings><system.web><compilation targetFramework="3.5"/><httpRuntime targetFramework="4.8"/></system.web></configuration>')
            binary = self.binary("net48", "vb")
            before = {path: hashlib.sha256(path.read_bytes()).digest() for path in [project, config, binary]}
            report = self.collect(folder, ["--project", str(project), "--config", str(config), "--binary", str(binary)])
            self.assertIn("Project TargetFrameworkVersion", report)
            self.assertIn("conditional=True: v3.5", report)
            self.assertIn("UNRESOLVED PROPERTY EXPRESSION", report)
            self.assertIn("compilation.targetFramework", report)
            self.assertIn("httpRuntime.targetFramework", report)
            self.assertIn("Binary TargetFrameworkAttribute: .NETFramework,Version=v4.8", report)
            self.assertIn("No effective/application TFM is selected", report)
            self.assertIn("Multiple distinct literal framework strings", report)
            self.assertIn("No value wins automatically", report)
            self.assertNotIn("net20", report)
            self.assertIn("UploadString", report)
            self.assertIn("System, Version=4.0.0.0", report)
            for excluded in ["DO_NOT_SHARE_TEST_SECRET", "private-name", "synthetic-identity", str(folder), "connectionString="]:
                self.assertNotIn(excluded, report)
            for path, digest in before.items():
                self.assertEqual(hashlib.sha256(path.read_bytes()).digest(), digest)
            self.assertRegex(report, r"Generator SHA-256: [0-9a-f]{64}")
            self.assertRegex(report, r"Bounded input SHA-256 \(private/local only\): [0-9a-f]{64}")

    def test_net20_and_net35_both_reference_system2_and_keep_binary_tfm_unknown(self):
        for tfm in ["net20", "net35"]:
            with self.subTest(tfm=tfm), tempfile.TemporaryDirectory() as tmp:
                report = self.collect(pathlib.Path(tmp), ["--binary", str(self.binary(tfm))])
                self.assertIn("TargetFrameworkAttribute missing; TFM unknown", report)
                self.assertIn("System, Version=2.0.0.0", report)
                self.assertIn("b77a5c561934e089", report)
                self.assertIn("UploadValues", report)
                self.assertNotIn("Binary TargetFrameworkAttribute:", report)

    def test_modern_http_json_signatures_are_retained_without_private_types(self):
        with tempfile.TemporaryDirectory() as tmp:
            report = self.collect(pathlib.Path(tmp), ["--binary", str(self.binary("net10.0"))])
            self.assertIn(".NETCoreApp,Version=v10.0", report)
            self.assertIn("HttpClientJsonExtensions", report)
            self.assertIn("GetFromJsonAsync", report)
            self.assertIn("HttpRequestMessage..ctor", report)
            self.assertNotIn("Matrix.Case", report)
            self.assertNotIn("example.invalid", report)

    def test_dtd_missing_and_native_binary_fail_explicitly_without_contents_or_paths(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = pathlib.Path(tmp)
            xml = folder / "unsafe.config"
            xml.write_text('<!DOCTYPE configuration [<!ENTITY secret SYSTEM "file:///DO_NOT_SHARE_TEST_SECRET">]><configuration>&secret;</configuration>')
            native = folder / "private-name.dll"
            native.write_bytes(b"DO_NOT_SHARE_TEST_SECRET")
            report = self.collect(folder, ["--config", str(xml), "--binary", str(native), "--project", str(folder / "missing.csproj")])
            self.assertIn("XmlException", report)
            self.assertIn("missing input", report)
            self.assertIn("input inspection failed", report)
            self.assertNotIn("DO_NOT_SHARE_TEST_SECRET", report)
            self.assertNotIn(str(folder), report)

    def test_missing_settings_and_size_refusal_are_explicit(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = pathlib.Path(tmp)
            config = folder / "Web.config"
            config.write_text('<configuration><system.web><compilation/></system.web></configuration>')
            oversized = folder / "oversized.csproj"
            oversized.write_bytes(b"X" * (2 * 1024 * 1024 + 1))
            report = self.collect(folder, ["--config", str(config), "--project", str(oversized)])
            self.assertIn("compilation.targetFramework", report)
            self.assertIn("httpRuntime.targetFramework: MISSING (element absent", report)
            self.assertIn("input byte limit exceeded; unread", report)
            self.assertIn("binary=0", report)
            self.assertEqual(oversized.stat().st_size, 2 * 1024 * 1024 + 1)

    def test_input_and_existing_output_cannot_be_overwritten(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = pathlib.Path(tmp)
            source = folder / "input.csproj"
            source.write_text('<Project><PropertyGroup><TargetFrameworks>net48;net10.0</TargetFrameworks></PropertyGroup></Project>')
            before = source.read_bytes()
            error = self.collect(folder, ["--project", str(source)], success=False, out=source)
            self.assertEqual(before, source.read_bytes())
            self.assertNotIn(str(source), error)
            report = self.collect(folder, ["--project", str(source)])
            self.assertIn("net48;net10.0", report)
            self.collect(folder, ["--project", str(source)], success=False)

if __name__ == "__main__":
    unittest.main()
