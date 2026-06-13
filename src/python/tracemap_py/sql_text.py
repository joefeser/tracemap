from __future__ import annotations

import re
from dataclasses import dataclass, field
from typing import Iterable

from .hashes import sha256_hex

SQL_VERBS = {"SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "CREATE", "ALTER", "DROP", "TRUNCATE", "CALL", "EXEC", "EXECUTE"}
SQL_STOP_WORDS = {
    "AND",
    "AS",
    "ASC",
    "BETWEEN",
    "BY",
    "CASE",
    "DESC",
    "DISTINCT",
    "ELSE",
    "END",
    "FALSE",
    "FROM",
    "GROUP",
    "HAVING",
    "IN",
    "IS",
    "JOIN",
    "LEFT",
    "LIKE",
    "LIMIT",
    "NOT",
    "NULL",
    "ON",
    "OR",
    "ORDER",
    "RIGHT",
    "SELECT",
    "SET",
    "THEN",
    "TRUE",
    "VALUES",
    "WHEN",
    "WHERE",
}


@dataclass(frozen=True)
class QueryShape:
    operation_name: str
    table_names: tuple[str, ...] = field(default_factory=tuple)
    column_names: tuple[str, ...] = field(default_factory=tuple)
    query_shape_hash: str = ""

    @property
    def primary_table(self) -> str:
        return self.table_names[0] if self.table_names else ""


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
    if first in SQL_VERBS:
        return first
    if first == "WITH":
        return "WITH"
    return ""


def text_hash(value: str) -> str:
    return sha256_hex(value, 32)


def query_shape(value: str) -> QueryShape:
    normalized = _normalized_sql(value)
    operation = operation_name(normalized)
    tables = _table_names(normalized, operation)
    columns = _column_names(normalized, operation)
    return QueryShape(operation, tuple(tables), tuple(columns), sha256_hex(normalized, 32))


def query_shape_properties(value: str, source_kind: str) -> dict[str, str]:
    shape = query_shape(value)
    props = {
        "textHash": text_hash(value),
        "queryShapeHash": shape.query_shape_hash,
        "operationName": shape.operation_name,
        "sqlSourceKind": source_kind,
    }
    if shape.primary_table:
        props["tableName"] = shape.primary_table
    if shape.table_names:
        props["tableNames"] = ";".join(shape.table_names)
    if shape.column_names:
        props["columnNames"] = ";".join(shape.column_names)
        props["fieldNames"] = ";".join(shape.column_names)
    return props


def _normalized_sql(value: str) -> str:
    value = re.sub(r"--[^\n\r]*", " ", value)
    value = re.sub(r"/\*.*?\*/", " ", value, flags=re.S)
    return re.sub(r"\s+", " ", value).strip().rstrip(";")


def _table_names(sql: str, operation: str) -> list[str]:
    candidates: list[str] = []
    if operation in {"SELECT", "WITH"}:
        candidates.extend(_matches(sql, r"\bFROM\s+([A-Za-z_][A-Za-z0-9_.$\[\]\"`]*)"))
        candidates.extend(_matches(sql, r"\bJOIN\s+([A-Za-z_][A-Za-z0-9_.$\[\]\"`]*)"))
    elif operation == "INSERT":
        candidates.extend(_matches(sql, r"\bINSERT\s+INTO\s+([A-Za-z_][A-Za-z0-9_.$\[\]\"`]*)"))
    elif operation == "UPDATE":
        candidates.extend(_matches(sql, r"\bUPDATE\s+([A-Za-z_][A-Za-z0-9_.$\[\]\"`]*)"))
    elif operation == "DELETE":
        candidates.extend(_matches(sql, r"\bDELETE\s+FROM\s+([A-Za-z_][A-Za-z0-9_.$\[\]\"`]*)"))
    elif operation == "CREATE":
        candidates.extend(_matches(sql, r"\bCREATE\s+(?:TEMP(?:ORARY)?\s+)?TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?([A-Za-z_][A-Za-z0-9_.$\[\]\"`]*)"))
    elif operation in {"DROP", "TRUNCATE", "ALTER"}:
        candidates.extend(_matches(sql, rf"\b{operation}\s+(?:TABLE\s+)?([A-Za-z_][A-Za-z0-9_.$\[\]\"`]*)"))
    return _unique(_clean_identifier(value) for value in candidates)


def _column_names(sql: str, operation: str) -> list[str]:
    if operation in {"SELECT", "WITH"}:
        select_part = _between(sql, "SELECT", "FROM")
        return _select_columns(select_part)
    if operation == "INSERT":
        match = re.search(r"\bINSERT\s+INTO\s+[A-Za-z_][A-Za-z0-9_.$\[\]\"`]*\s*\(([^)]*)\)", sql, flags=re.I)
        return _split_identifier_list(match.group(1) if match else "")
    if operation == "UPDATE":
        set_part = _between(sql, "SET", "WHERE")
        return _unique(_clean_identifier(part.split("=", 1)[0]) for part in _split_csv(set_part))
    if operation == "CREATE":
        match = re.search(r"\bCREATE\s+(?:TEMP(?:ORARY)?\s+)?TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?[A-Za-z_][A-Za-z0-9_.$\[\]\"`]*\s*\((.*)\)", sql, flags=re.I | re.S)
        return _create_table_columns(match.group(1) if match else "")
    return []


def _matches(sql: str, pattern: str) -> list[str]:
    return [match.group(1) for match in re.finditer(pattern, sql, flags=re.I)]


def _between(sql: str, start: str, end: str) -> str:
    match = re.search(rf"\b{start}\b(.*?)(?:\b{end}\b|$)", sql, flags=re.I | re.S)
    return match.group(1) if match else ""


def _select_columns(text: str) -> list[str]:
    columns: list[str] = []
    for part in _split_csv(text):
        cleaned = re.sub(r"\bAS\b\s+[A-Za-z_][A-Za-z0-9_]*$", "", part.strip(), flags=re.I)
        token = cleaned.rsplit(".", 1)[-1].strip()
        token = re.split(r"\s+", token)[-1] if re.search(r"\s", token) else token
        name = _clean_identifier(token)
        if name and name != "*" and name.upper() not in SQL_STOP_WORDS and re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", name):
            columns.append(name)
    return _unique(columns)


def _create_table_columns(text: str) -> list[str]:
    columns: list[str] = []
    for part in _split_csv(text):
        first = part.strip().split(None, 1)[0] if part.strip() else ""
        name = _clean_identifier(first)
        if name and name.upper() not in {"CONSTRAINT", "PRIMARY", "FOREIGN", "UNIQUE", "CHECK", "KEY"}:
            columns.append(name)
    return _unique(columns)


def _split_identifier_list(text: str) -> list[str]:
    return _unique(_clean_identifier(part) for part in _split_csv(text))


def _split_csv(text: str) -> list[str]:
    parts: list[str] = []
    depth = 0
    start = 0
    for index, char in enumerate(text):
        if char == "(":
            depth += 1
        elif char == ")" and depth > 0:
            depth -= 1
        elif char == "," and depth == 0:
            parts.append(text[start:index].strip())
            start = index + 1
    tail = text[start:].strip()
    if tail:
        parts.append(tail)
    return parts


def _clean_identifier(value: str) -> str:
    value = value.strip().strip(",;")
    value = value.strip('"`[]')
    if "." in value:
        value = value.rsplit(".", 1)[-1].strip('"`[]')
    if not value or not re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", value):
        return ""
    return value


def _unique(values: Iterable[str]) -> list[str]:
    seen: set[str] = set()
    result: list[str] = []
    for value in values:
        if value and value not in seen:
            seen.add(value)
            result.append(value)
    return result
