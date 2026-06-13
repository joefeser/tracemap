from __future__ import annotations

import re
from urllib.parse import urlsplit

PARAM_RE = re.compile(r"\{[^}/]+\}|<[^>/]+>|:[A-Za-z_][A-Za-z0-9_]*")


def normalize_path_key(path: str) -> tuple[str, str]:
    value = (path or "").strip()
    if "://" in value:
        split = urlsplit(value)
        value = split.path or "/"
    value = value.split("?", 1)[0].split("#", 1)[0]
    if not value.startswith("/"):
        value = "/" + value
    value = re.sub(r"/+", "/", value)
    if len(value) > 1 and value.endswith("/"):
        value = value[:-1]
    template = PARAM_RE.sub(lambda match: _template_param(match.group(0)), value)
    key = PARAM_RE.sub("{}", value).lower()
    return template, key


def combine_paths(prefix: str, path: str) -> str:
    if not prefix:
        return path
    return prefix.rstrip("/") + "/" + path.lstrip("/")


def _template_param(value: str) -> str:
    value = value.strip("{}<>:")
    if ":" in value:
        value = value.rsplit(":", 1)[-1]
    return "{" + value + "}"
