from __future__ import annotations

import hashlib


def sha256_hex(value: str, length: int = 64) -> str:
    digest = hashlib.sha256(value.encode("utf-8")).hexdigest()
    return digest[: min(length, len(digest))]


def sha256_bytes(value: bytes, length: int = 64) -> str:
    digest = hashlib.sha256(value).hexdigest()
    return digest[: min(length, len(digest))]
