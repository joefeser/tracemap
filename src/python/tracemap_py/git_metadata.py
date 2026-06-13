from __future__ import annotations

import subprocess
from pathlib import Path

from .models import GitMetadata


def _git(repo: Path, *args: str) -> str | None:
    try:
        result = subprocess.run(
            ["git", "-C", str(repo), *args],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            timeout=5,
        )
        return result.stdout.strip()
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired, FileNotFoundError):
        return None


def read_git_metadata(repo: Path) -> GitMetadata:
    commit = _git(repo, "rev-parse", "HEAD")
    root = _git(repo, "rev-parse", "--show-toplevel")
    if not commit or not root:
        raise RuntimeError("Git commit SHA unavailable; Python scan requires a concrete git checkout.")
    remote = _git(repo, "config", "--get", "remote.origin.url")
    branch = _git(repo, "rev-parse", "--abbrev-ref", "HEAD")
    root_path = Path(root)
    repo_name = root_path.name
    return GitMetadata(repo_name, remote or None, branch or None, commit, str(root_path))
