from __future__ import annotations

import re

from .hashes import sha256_hex

SQL_VERBS = {"SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "CREATE", "ALTER", "DROP", "TRUNCATE", "CALL", "EXEC", "EXECUTE"}


def is_sql_like(value: str) -> bool:
    text = value.lstrip()
    if not text:
        return False
    first = re.split(r"\s+", text, maxsplit=1)[0].upper()
    return first in SQL_VERBS or first == "WITH"


def operation_name(value: str) -> str:
    text = value.lstrip()
    if not text:
        return ""
    first = re.split(r"\s+", text, maxsplit=1)[0].upper()
    return first if first in SQL_VERBS else ""


def text_hash(value: str) -> str:
    return sha256_hex(value, 32)
