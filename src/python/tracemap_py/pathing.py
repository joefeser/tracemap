from __future__ import annotations

import fnmatch
import os
import sys
import unicodedata
from pathlib import Path


def normalize_path(path: str | Path) -> str:
    return str(path).replace(os.sep, "/")


def relative_to(path: Path, root: Path) -> str:
    return normalize_path(path.resolve().relative_to(root.resolve()))


def matches_any(path: str, patterns: list[str]) -> bool:
    comparable_path = _filesystem_comparison_path(path)
    return any(fnmatch.fnmatch(comparable_path, _filesystem_comparison_path(pattern)) for pattern in patterns)


def _filesystem_comparison_path(value: str) -> str:
    normalized = normalize_path(value)
    return unicodedata.normalize("NFC", normalized) if sys.platform == "darwin" else normalized


def parse_byte_size(value: str | int | None, default: int = 1_000_000) -> int:
    if value is None:
        return default
    if isinstance(value, int):
        return value
    text = value.strip().lower()
    multiplier = 1
    for suffix, factor in (("kb", 1000), ("k", 1000), ("mb", 1000 * 1000), ("m", 1000 * 1000)):
        if text.endswith(suffix):
            text = text[: -len(suffix)]
            multiplier = factor
            break
    return int(float(text) * multiplier)
