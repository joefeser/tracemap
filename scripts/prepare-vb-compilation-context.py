"""Local-only candidate preparation; approval is a separate, explicit operation.

Directory membership is a proposal, never evidence of original compiler inputs.
No application code is executed and no references are selected from the host SDK.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read(path, limit):
    path = Path(path).absolute()
    if any(p.is_symlink() or (hasattr(p, "is_junction") and p.is_junction()) for p in (path, *path.parents)):
        raise ValueError("linked-input")
    if not path.is_file():
        raise ValueError("non-file-input")
    with path.open("rb") as stream:
        data = stream.read(limit + 1)
    if len(data) > limit:
        raise ValueError("file-limit")
    return data


def head(repo):
    result = subprocess.run(["git", "-C", str(repo), "rev-parse", "HEAD"],
                            capture_output=True, check=True, timeout=15, text=True)
    value = result.stdout.strip()
    if len(value) not in (40, 64) or any(c not in "0123456789abcdef" for c in value):
        raise ValueError("commit-unavailable")
    return value


def roster(roots, suffix, count_limit, byte_limit, file_limit):
    found = {}
    visited = total = 0
    for root in roots:
        root = Path(root).absolute()
        if not root.is_dir() or any(p.is_symlink() or (hasattr(p, "is_junction") and p.is_junction()) for p in (root, *root.parents)):
            raise ValueError("invalid-directory")
        for directory, dirs, files in os.walk(root, followlinks=False):
            dirs.sort()
            for name in sorted(dirs + files):
                visited += 1
                if visited > 4096:
                    raise ValueError("discovery-limit")
                path = Path(directory) / name
                if path.is_symlink() or (hasattr(path, "is_junction") and path.is_junction()):
                    raise ValueError("linked-input")
            for name in sorted(files):
                path = Path(directory) / name
                if path.suffix.lower() not in suffix or str(path) in found:
                    continue
                data = read(path, file_limit)
                total += len(data)
                if total > byte_limit or len(found) >= count_limit:
                    raise ValueError("roster-limit")
                found[str(path)] = digest(data)
    return [{"path": path, "sha256": sha} for path, sha in sorted(found.items())]


def envelope(schema, payload):
    text = canonical(payload)
    return {"schemaVersion": schema, "generatorSha256": digest(read(__file__, 1_048_576)),
            "boundedInputSha256": digest(text.encode()), "payloadJson": text,
            "ruleId": "workflow.vb-compilation-context-preparation.v1", "visibility": "local-only"}


def unpack(path, schema):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate-key")
            result[key] = value
        return result
    value = json.loads(read(path, 1_048_576), object_pairs_hook=unique)
    if set(value) != {"schemaVersion", "generatorSha256", "boundedInputSha256", "payloadJson", "ruleId", "visibility"} or value["schemaVersion"] != schema or value["visibility"] != "local-only" or value["ruleId"] != "workflow.vb-compilation-context-preparation.v1":
        raise ValueError("invalid-envelope")
    if value["generatorSha256"] != digest(read(__file__, 1_048_576)) or value["boundedInputSha256"] != digest(value["payloadJson"].encode()):
        raise ValueError("stale-or-changed-proposal")
    return json.loads(value["payloadJson"], object_pairs_hook=unique)


def prepare(args):
    repo = Path(args.repo).absolute()
    sources = roster(args.source_dir, {".vb"}, 512, 67_108_864, 8_388_608)
    for item in sources:
        item["path"] = Path(item["path"]).relative_to(repo).as_posix()
    references = roster(args.reference_dir, {".dll", ".exe"}, 256, 536_870_912, 67_108_864)
    if not sources or not references:
        raise ValueError("empty-roster")
    context = {"schemaVersion": "vb-compilation-context.v1", "commitSha": head(repo),
               "rootNamespace": args.root_namespace, "optionStrict": args.option_strict == "on",
               "optionInfer": args.option_infer == "on", "optionExplicit": args.option_explicit == "on",
               "imports": sorted(set(args.imports)), "sources": sources, "references": references}
    return envelope("vb-compilation-proposal.v1", {
        "repo": str(repo), "context": context,
        "observed": "current-file-bytes-and-repository-head-only",
        "proposed": "source-membership-reference-selection-and-compiler-options",
        "originalBuildCorrespondence": "not-established"})


def approve(args):
    proposal = unpack(args.proposal, "vb-compilation-proposal.v1")
    repo = Path(proposal["repo"])
    context = proposal["context"]
    if head(repo) != context["commitSha"]:
        raise ValueError("stale-commit")
    for role, limit in (("sources", 8_388_608), ("references", 67_108_864)):
        for item in context[role]:
            path = repo / item["path"] if role == "sources" else Path(item["path"])
            if digest(read(path, limit)) != item["sha256"]:
                raise ValueError("changed-input")
    return envelope("prepared-vb-compilation-context.v1", context)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    draft = commands.add_parser("draft")
    draft.add_argument("--repo", required=True)
    draft.add_argument("--source-dir", action="append", required=True)
    draft.add_argument("--reference-dir", action="append", required=True)
    draft.add_argument("--root-namespace", required=True)
    for name in ("strict", "infer", "explicit"):
        draft.add_argument("--option-" + name, choices=("on", "off"), required=True)
    draft.add_argument("--import", dest="imports", action="append", default=[])
    draft.add_argument("--out", required=True)
    accept = commands.add_parser("approve")
    accept.add_argument("--proposal", required=True)
    accept.add_argument("--operator-approved", action="store_true", required=True)
    accept.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    try:
        value = prepare(args) if args.command == "draft" else approve(args)
        output = Path(args.out).absolute()
        repo = Path(args.repo).absolute() if args.command == "draft" else Path(unpack(args.proposal, "vb-compilation-proposal.v1")["repo"])
        if output == repo or repo in output.parents:
            raise ValueError("output-inside-source")
        for parent in output.parents:
            if parent.is_symlink() or (hasattr(parent, "is_junction") and parent.is_junction()):
                raise ValueError("linked-output")
        encoded = (canonical(value) + "\n").encode()
        if len(encoded) > 1_048_576:
            raise ValueError("output-limit")
        with output.open("xb") as stream:
            stream.write(encoded)
        print("LOCAL_PROPOSAL_WRITTEN" if args.command == "draft" else "LOCAL_CONTEXT_WRITTEN")
        return 0
    except (ValueError, OSError, KeyError, TypeError, subprocess.SubprocessError):
        print("CONTEXT_PREPARATION_REFUSED: inspect local inputs; no context accepted", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
