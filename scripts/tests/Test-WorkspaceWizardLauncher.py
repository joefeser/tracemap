#!/usr/bin/env python3
"""Public synthetic terminal replay: wizard -> SQL workspace -> verified SQLite.
Requires git, dotnet and PowerShell 7. Never reads employer inputs. Fresh output only.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--with-source", action="store_true", help="Also select the synthetic dependency source repository")
    parser.add_argument("--sql-beside-workspace", action="store_true", help="Manifest beside root.config.json lists sql/schema.sql; infer its base without another folder question")
    args = parser.parse_args()
    root = Path(args.output_root).resolve()
    if root.exists():
        raise RuntimeError("Existing replay preserved; choose a fresh output root")
    root.mkdir(parents=True)
    repo = Path(__file__).resolve().parents[2]
    launcher = repo / "scripts/tm.ps1"
    logs = root / "logs"
    logs.mkdir()

    def run(name, command, answers=None, expected=0, timeout=240):
        result = subprocess.run(command, cwd=repo, input=answers, text=True,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=timeout)
        (logs / (name + ".log")).write_text(result.stdout, encoding="utf-8")
        if result.returncode != expected:
            raise RuntimeError(f"{name}: exit {result.returncode}, expected {expected}; inspect {logs / (name + '.log')}")
        print(f"PASS {name}", flush=True)
        return result.stdout

    def tool(name, command, answers=None, expected=0):
        return run(name, ["pwsh", "-NoProfile", "-File", str(launcher), *command], answers, expected)

    def git(path, *command):
        return run("git-" + path.name + "-" + command[0], ["git", "-C", str(path), *command]).strip()

    web = root / "web"
    dependency = root / "dependency"
    build = root / "fixture-build"
    published = root / "published"
    sql = root / "sql"
    workspace = root / "workspace"
    for folder in (web, dependency, build, published / "bin", sql):
        folder.mkdir(parents=True)
    target = run("target-framework", ["dotnet", "msbuild", str(repo / "src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj"), "-nologo", "-getProperty:TargetFramework"]).strip()
    (web / "web.config").write_text("<configuration />", encoding="utf-8")
    (web / "Default.aspx").write_text('<%@ Page Language="C#" CodeFile="Default.aspx.cs" Inherits="Example.Page" %>', encoding="utf-8")
    (web / "Default.aspx.cs").write_text("namespace Example; public class Page { public int Read() => new ExampleDependency.Service().Read(); }", encoding="utf-8")
    (dependency / ".gitignore").write_text("bin/\nobj/\n", encoding="utf-8")
    (dependency / "Service.cs").write_text("namespace ExampleDependency; public class Service { public int Read() => 1; }", encoding="utf-8")
    (dependency / "Dependency.csproj").write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>{target}</TargetFramework></PropertyGroup></Project>', encoding="utf-8")
    from xml.sax.saxutils import escape
    (build / "SyntheticWeb.csproj").write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>{target}</TargetFramework></PropertyGroup><ItemGroup><Compile Include="{escape(str(web / "Default.aspx.cs"))}" /><ProjectReference Include="{escape(str(dependency / "Dependency.csproj"))}" /></ItemGroup></Project>', encoding="utf-8")
    for folder in (web, dependency):
        git(folder, "init", "-q")
        git(folder, "config", "core.autocrlf", "false")
        git(folder, "remote", "add", "origin", f"https://example.invalid/synthetic-{folder.name}.git")
        git(folder, "add", ".")
        git(folder, "-c", "user.name=Public Synthetic Fixture", "-c", "user.email=fixture@example.invalid", "-c", "commit.gpgsign=false", "commit", "-qm", "public synthetic fixture")
    head = git(web, "rev-parse", "HEAD")
    run("build-public-fixture", ["dotnet", "publish", str(build / "SyntheticWeb.csproj"), "-c", "Release", "-o", str(published / "bin"), "--nologo"])
    (published / "web.config").write_text("<configuration />", encoding="utf-8")
    (published / "PrecompiledApp.config").write_text('<precompiledApp version="2" />', encoding="utf-8")
    (published / "bin/Default.aspx.compiled").write_text('<preserve virtualPath="/Default.aspx" assembly="SyntheticWeb" type="Example.Page" />', encoding="utf-8")
    sql_file = sql / "schema.sql"
    sql_file.write_text("CREATE PROC dbo.ReadItems AS SELECT 1 AS Id;", encoding="utf-8")
    manifest = {"version": "sqlserver-local-definitions-input/1", "visibility": "local-only",
                "contextRepository": "web", "contextCommitSha": head, "definitionSet": "synthetic-wizard-replay",
                "server": "ExampleServer", "database": "ExampleDatabase",
                "files": [{"path": "schema.sql", "sha256": hashlib.sha256(sql_file.read_bytes()).hexdigest()}]}
    manifest["provenance"] = {"generatorSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                              "boundedInputSha256": hashlib.sha256(json.dumps(manifest, sort_keys=True).encode()).hexdigest()}
    manifest_path = root / "sql.local.json"
    manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
    tool("prepare-tool", ["-Build", "version"])
    answers = "\n".join(["web", str(web), "all", "ready", str(published), "2", str(published / "bin/Dependency.dll"), "prepare", head, "later", ""])
    tool("wizard-native", ["webforms-review", "wizard", "--root", str(workspace)], answers, expected=2)
    if (workspace / "workspace.local.json").exists():
        raise RuntimeError("Workspace appeared before its setup")
    if args.sql_beside_workspace:
        sql = workspace / "sql"
        sql.mkdir()
        (sql / "schema.sql").write_bytes(sql_file.read_bytes())
        manifest["files"][0]["path"] = "sql/schema.sql"
        del manifest["provenance"]
        manifest["provenance"] = {"generatorSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                                  "boundedInputSha256": hashlib.sha256(json.dumps(manifest, sort_keys=True).encode()).hexdigest()}
        manifest_path = workspace / "sql-definitions.local.json"
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
    source_answers = [str(dependency), "Dependency.csproj"] if args.with_source else ["skip"]
    tool("wizard-pause-before-sql", ["webforms-review", "wizard", "--root", str(workspace), "--continue"],
         "\n".join(["workspace", *source_answers, "later", ""]), expected=2)
    if (workspace / "workspace.local.json").exists():
        raise RuntimeError("Paused SQL setup unexpectedly created a workspace")
    output = tool("wizard-create-workspace", ["webforms-review", "wizard", "--root", str(workspace), "--continue"],
                  "\n".join(["workspace", str(manifest_path), *([] if args.sql_beside_workspace else [str(sql)]), "create", "collect", ""]))
    config_path = workspace / "workspace.local.json"
    config = json.loads(config_path.read_text())
    if len(config["repositories"]) != (2 if args.with_source else 1):
        raise RuntimeError("Additional source selection was not preserved")
    if "Existing SQL manifest FILE" not in output:
        raise RuntimeError("Resumed wizard did not reach the SQL question")
    tool("workspace-verify", ["workspace", "verify", "--root", str(workspace)])
    collections = list((workspace / "runs").glob("workspace-*"))
    if len(collections) != 1:
        raise RuntimeError("Wizard collect did not produce exactly one fresh workspace run")
    collection = collections[0]
    if "Open the workspace results:" not in output:
        raise RuntimeError("Wizard omitted the completed output handoff")
    record = json.loads((collection / "workspace-run.local.json").read_text())
    if record["outcome"] not in ("complete", "partial"):
        raise RuntimeError("Workspace collection did not complete")
    if record["sqlEvidence"]["callBinding"] != "not-established":
        raise RuntimeError("Unexpected SQL binding claim")
    import sqlite3
    indexes = [collection / "combined.sqlite", collection / record["sqlEvidence"]["index"],
               collection / record["compiledReview"]["rawScan"] / "index.sqlite"]
    for index, fact_table in zip(indexes, ("combined_facts", "sql_objects", "facts")):
        if not index.is_file():
            raise RuntimeError(f"Missing SQLite output: {index}")
        with sqlite3.connect(f"file:{index.as_posix()}?mode=ro", uri=True) as db:
            if db.execute("PRAGMA quick_check").fetchone()[0] != "ok":
                raise RuntimeError(f"Invalid SQLite: {index}")
            if db.execute("SELECT count(*) FROM sqlite_master WHERE type='table'").fetchone()[0] == 0:
                raise RuntimeError(f"No tables: {index}")
            if db.execute(f"SELECT count(*) FROM {fact_table}").fetchone()[0] == 0:
                raise RuntimeError(f"No evidence rows in {index}")
    definitions = json.loads((collection / record["sqlEvidence"]["definitions"]).read_text())
    if not any(item["name"] == "ReadItems" for item in definitions["objects"]):
        raise RuntimeError("Synthetic SQL definition missing")
    for path in (collection / "START-HERE.md", collection / "report.md"):
        if not path.is_file():
            raise RuntimeError(f"Missing report: {path}")
    tool("wizard-existing-workspace-verify", ["webforms-review", "wizard", "--root", str(workspace), "--continue"], "workspace\nverify\n")
    print("PASS full public wizard/SQL/SQLite replay", flush=True)
    print(f"Configuration: {config_path}\nReports: {collection / 'START-HERE.md'}")
    for index in indexes:
        print(f"Verified SQLite: {index}")
    print("Synthetic managed fixture only; partial static evidence, not IIS/runtime/private acceptance.")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"FAIL: {error}", file=sys.stderr)
        sys.exit(1)
