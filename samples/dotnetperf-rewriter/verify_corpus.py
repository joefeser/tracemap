#!/usr/bin/env python3
"""Verify the dotnetperf oracle corpus byte pin.

The pin (CORPUS-SHA256.txt) covers exactly the vendored files — the 405 files
imported from the pinned upstream commit. PROVENANCE.md, CORPUS-SHA256.txt and
this script are repository metadata and are intentionally outside the pin, so
provenance edits never invalidate it.

Two modes, both deterministic:

  python3 verify_corpus.py            # committed objects (autocrlf-proof)
  python3 verify_corpus.py --worktree # working-tree bytes (LF-preserving checkouts)

Exit 0 = every vendored byte matches the pin; nonzero with a reason otherwise.
"""

from __future__ import annotations

import hashlib
import pathlib
import subprocess
import sys

CORPUS_DIR = pathlib.Path(__file__).resolve().parent
REPO_ROOT = CORPUS_DIR.parent.parent
PREFIX = "samples/dotnetperf-rewriter/"
MANIFEST = CORPUS_DIR / "CORPUS-SHA256.txt"
# Repository metadata lives outside the pin by design.
UNPINNED = {"PROVENANCE.md", "CORPUS-SHA256.txt", "verify_corpus.py"}


def pinned_hashes() -> dict[str, str]:
    manifest: dict[str, str] = {}
    for line in MANIFEST.read_text(encoding="utf-8").splitlines():
        digest, _, name = line.partition("  ")
        if digest and name:
            manifest[name] = digest
    return manifest


def committed_hashes() -> dict[str, str]:
    out = subprocess.run(
        ["git", "ls-tree", "-r", "HEAD", "--", PREFIX],
        cwd=REPO_ROOT, capture_output=True, text=True, check=True,
    ).stdout
    hashes: dict[str, str] = {}
    for line in out.splitlines():
        meta, path = line.split("\t", 1)
        name = path[len(PREFIX):]
        if name in UNPINNED:
            continue
        blob = subprocess.run(
            ["git", "cat-file", "blob", meta.split()[2]],
            cwd=REPO_ROOT, capture_output=True, check=True,
        ).stdout
        hashes[name] = hashlib.sha256(blob).hexdigest()
    return hashes


def worktree_hashes() -> dict[str, str]:
    # Tracked files only: untracked junk (Finder droppings, gitignored
    # SourceSafe bindings) must not fail a worktree verification.
    out = subprocess.run(
        ["git", "ls-files", PREFIX],
        cwd=REPO_ROOT, capture_output=True, text=True, check=True,
    ).stdout
    hashes: dict[str, str] = {}
    for path in out.splitlines():
        name = path[len(PREFIX):]
        if name in UNPINNED:
            continue
        hashes[name] = hashlib.sha256((REPO_ROOT / path).read_bytes()).hexdigest()
    return hashes


def main() -> int:
    manifest = pinned_hashes()
    actual = worktree_hashes() if "--worktree" in sys.argv else committed_hashes()
    problems: list[str] = []
    for name in sorted(set(manifest) | set(actual)):
        if name not in manifest:
            problems.append(f"present in corpus but unpinned: {name}")
        elif name not in actual:
            problems.append(f"pinned but missing: {name}")
        elif manifest[name] != actual[name]:
            problems.append(f"hash mismatch: {name}")
    if problems:
        for problem in problems:
            print(problem, file=sys.stderr)
        print(f"FAIL: {len(problems)} problem(s)", file=sys.stderr)
        return 1
    print(f"OK: {len(manifest)} vendored files match the pin "
          f"({'worktree' if '--worktree' in sys.argv else 'committed objects'} mode)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
