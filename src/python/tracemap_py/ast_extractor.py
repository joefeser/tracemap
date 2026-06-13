from __future__ import annotations

import ast
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable

from .constants import EvidenceTiers, FactTypes, RuleIds, ScannerVersions
from .fact_factory import create_fact, evidence
from .hashes import sha256_hex
from .inventory import module_name
from .models import CodeFact, ScanManifest
from .route import combine_paths, normalize_path_key
from .sql_text import is_sql_like, operation_name, text_hash


HTTP_METHODS = {"get": "GET", "post": "POST", "put": "PUT", "patch": "PATCH", "delete": "DELETE", "head": "HEAD", "options": "OPTIONS"}


@dataclass
class PythonContext:
    repo: Path
    package_roots: list[Path]
    dependencies: dict[str, str]
    import_aliases: dict[str, str] = field(default_factory=dict)
    resolved_router_prefixes: dict[str, str] = field(default_factory=dict)
    imports: set[str] = field(default_factory=set)
    app_vars: set[str] = field(default_factory=set)
    router_prefixes: dict[str, str] = field(default_factory=dict)
    flask_vars: set[str] = field(default_factory=set)
    sqlalchemy_imported: bool = False


@dataclass(frozen=True)
class ClassContext:
    symbol: str
    is_pydantic: bool
    is_dataclass: bool
    is_sqlalchemy: bool
    table_name: str | None = None


@dataclass(frozen=True)
class ParsedPythonFile:
    path: Path
    rel: str
    module: str
    root: ast.Module


@dataclass(frozen=True)
class PythonPrepass:
    aliases_by_module: dict[str, dict[str, str]]
    router_prefixes_by_module: dict[str, dict[str, str]]


def extract_python_files(repo: Path, manifest: ScanManifest, files: list[Path], package_roots: list[Path], dependencies: dict[str, str], gaps: list[str]) -> list[CodeFact]:
    facts: list[CodeFact] = []
    parsed: list[ParsedPythonFile] = []
    for path in sorted(files):
        rel = str(path.resolve().relative_to(repo.resolve())).replace("\\", "/")
        if path.suffix == ".pyi":
            gaps.append(f"PythonStubSkipped: {rel}")
            facts.append(_gap(manifest, rel, "python-stub", "pyi stubs are out of MVP scope"))
            continue
        try:
            text = path.read_text(encoding="utf-8")
            root = ast.parse(text, filename=rel)
        except SyntaxError as exc:
            gaps.append(f"PythonParseFailed: {rel}:{exc.lineno or 1}")
            facts.append(_gap(manifest, rel, "parse-error", exc.msg, exc.lineno or 1))
            continue
        except (OSError, UnicodeDecodeError) as exc:
            gaps.append(f"PythonReadFailed: {rel}: {type(exc).__name__}")
            facts.append(_gap(manifest, rel, "read-error", type(exc).__name__))
            continue
        parsed.append(ParsedPythonFile(path, rel, module_name(path, repo, package_roots), root))
    prepass = _prepass_python_files(parsed)
    for item in parsed:
        aliases = prepass.aliases_by_module.get(item.module, {})
        ctx = PythonContext(repo, package_roots, dependencies, import_aliases=aliases, resolved_router_prefixes=prepass.router_prefixes_by_module.get(item.module, {}))
        _seed_context_imports(ctx, aliases)
        visitor = AstVisitor(manifest, item.rel, item.module, ctx)
        visitor.visit(item.root)
        facts.extend(visitor.facts)
    return facts


class AstVisitor(ast.NodeVisitor):
    def __init__(self, manifest: ScanManifest, rel: str, module: str, ctx: PythonContext):
        self.manifest = manifest
        self.rel = rel
        self.module = module
        self.ctx = ctx
        self.facts: list[CodeFact] = []
        self.class_stack: list[str] = []
        self.class_context_stack: list[ClassContext] = []
        self.function_stack: list[str] = []
        self.parameters_stack: list[set[str]] = []

    @property
    def containing_symbol(self) -> str | None:
        if self.function_stack:
            return ".".join([self.module, *self.class_stack, self.function_stack[-1]])
        if self.class_stack:
            return ".".join([self.module, *self.class_stack])
        return self.module

    def visit_Import(self, node: ast.Import) -> None:
        for alias in node.names:
            root = alias.name.split(".")[0]
            self.ctx.imports.add(root)
            self.ctx.import_aliases[alias.asname or root] = alias.name
            if root == "sqlalchemy":
                self.ctx.sqlalchemy_imported = True
        self.generic_visit(node)

    def visit_ImportFrom(self, node: ast.ImportFrom) -> None:
        if node.module:
            root = node.module.split(".")[0]
            self.ctx.imports.add(root)
            for alias in node.names:
                if alias.name != "*":
                    self.ctx.import_aliases[alias.asname or alias.name] = f"{node.module}.{alias.name}"
            if root == "sqlalchemy":
                self.ctx.sqlalchemy_imported = True
        self.generic_visit(node)

    def visit_Assign(self, node: ast.Assign) -> None:
        self._record_framework_assignment(node)
        self._record_config_assignment(node)
        self._record_class_column_assignment(node)
        self._record_field_alias(node)
        self._record_local_alias(node)
        self.generic_visit(node)

    def visit_AnnAssign(self, node: ast.AnnAssign) -> None:
        if self.class_stack and isinstance(node.target, ast.Name):
            containing = ".".join([self.module, *self.class_stack])
            self._field_fact(node, node.target.id, containing, _safe_name(node.annotation), node.value)
        self.generic_visit(node)

    def visit_ClassDef(self, node: ast.ClassDef) -> None:
        symbol = ".".join([self.module, *self.class_stack, node.name])
        bases = [_safe_name(base) for base in node.bases]
        is_pydantic = any(base.endswith("BaseModel") for base in bases)
        is_dataclass = any(_decorator_name(dec).endswith("dataclass") for dec in node.decorator_list)
        table_name = _tablename(node.body)
        is_sqlalchemy = self.ctx.sqlalchemy_imported and (any(base.endswith(("Base", "DeclarativeBase")) for base in bases) or table_name is not None)
        tier = EvidenceTiers.TIER2 if is_pydantic or is_dataclass else EvidenceTiers.TIER3
        rule = RuleIds.PYDANTIC if is_pydantic else RuleIds.DATACLASS if is_dataclass else RuleIds.PY_DECLARATIONS
        self.facts.append(
            create_fact(
                self.manifest,
                FactTypes.TYPE_DECLARED,
                rule,
                tier,
                self._span(node, "PythonAstExtractor", ScannerVersions.AST),
                target_symbol=symbol,
                contract_element=node.name,
                properties={**_symbol_props("target", _symbol_id(symbol, "class"), "class", symbol), "name": node.name, "typeName": node.name, "namespace": self.module, "targetSymbol": symbol},
            )
        )
        for base in bases:
            if base:
                self.facts.append(
                    create_fact(
                        self.manifest,
                        FactTypes.SYMBOL_RELATIONSHIP,
                        RuleIds.PY_RELATIONSHIP,
                        EvidenceTiers.TIER3,
                        self._span(node, "PythonAstExtractor", ScannerVersions.AST),
                        source_symbol=symbol,
                        target_symbol=base,
                        properties={**_symbol_props("source", _symbol_id(symbol, "class"), "class", symbol), "sourceSymbolId": _symbol_id(symbol, "class"), "targetSymbolId": _symbol_id(base, "class"), "relationshipKind": "ExtendsClass"},
                    )
                )
        self.class_stack.append(node.name)
        self.class_context_stack.append(ClassContext(symbol, is_pydantic, is_dataclass, is_sqlalchemy, table_name))
        for stmt in node.body:
            self.visit(stmt)
        self.class_context_stack.pop()
        self.class_stack.pop()

    def visit_FunctionDef(self, node: ast.FunctionDef) -> None:
        self._visit_function(node)

    def visit_AsyncFunctionDef(self, node: ast.AsyncFunctionDef) -> None:
        self._visit_function(node)

    def visit_Call(self, node: ast.Call) -> None:
        self._record_route(node)
        self._record_http_client(node)
        self._record_config_read(node)
        self._record_sql_call(node)
        self._record_invocation(node)
        self._record_object_creation(node)
        self._record_argument_flow(node)
        self.generic_visit(node)

    def visit_Subscript(self, node: ast.Subscript) -> None:
        self._record_config_subscript(node)
        self.generic_visit(node)

    def visit_Attribute(self, node: ast.Attribute) -> None:
        self.facts.append(
            create_fact(
                self.manifest,
                FactTypes.MEMBER_ACCESS_NAME,
                RuleIds.PY_INVOCATION,
                EvidenceTiers.TIER3,
                self._span(node, "PythonAstExtractor", ScannerVersions.AST),
                source_symbol=self.containing_symbol,
                target_symbol=node.attr,
                contract_element=node.attr,
                properties={"memberName": node.attr, "name": node.attr, "expressionHash": _node_hash(node.value)},
            )
        )
        self.generic_visit(node)

    def _visit_function(self, node: ast.FunctionDef | ast.AsyncFunctionDef) -> None:
        symbol = ".".join([self.module, *self.class_stack, node.name])
        self.facts.append(
            create_fact(
                self.manifest,
                FactTypes.METHOD_DECLARED,
                RuleIds.PY_DECLARATIONS,
                EvidenceTiers.TIER3,
                self._span(node, "PythonAstExtractor", ScannerVersions.AST),
                source_symbol=".".join([self.module, *self.class_stack]) if self.class_stack else self.module,
                target_symbol=symbol,
                contract_element=node.name,
                properties={**_symbol_props("target", _symbol_id(symbol, "function"), "function", symbol), "methodName": node.name, "name": node.name, "containingType": ".".join([self.module, *self.class_stack])},
            )
        )
        params = {arg.arg for arg in node.args.args}
        self.function_stack.append(node.name)
        self.parameters_stack.append(params)
        for decorator in node.decorator_list:
            self.visit(decorator)
        for index, arg in enumerate(node.args.args):
            param_symbol = f"{symbol}({arg.arg})"
            self.facts.append(
                create_fact(
                    self.manifest,
                    FactTypes.PARAMETER_DECLARED,
                    RuleIds.PY_DECLARATIONS,
                    EvidenceTiers.TIER3,
                    evidence(self.rel, getattr(arg, "lineno", node.lineno), getattr(arg, "end_lineno", node.lineno), "PythonAstExtractor", ScannerVersions.AST),
                    source_symbol=symbol,
                    target_symbol=param_symbol,
                    contract_element=arg.arg,
                    properties={**_symbol_props("target", _symbol_id(param_symbol, "parameter"), "parameter", param_symbol), "parameterName": arg.arg, "parameterType": _safe_name(arg.annotation), "parameterOrdinal": index},
                )
            )
        for stmt in node.body:
            self.visit(stmt)
        self.parameters_stack.pop()
        self.function_stack.pop()

    def _field_fact(self, node: ast.AST, name: str, containing: str, annotation: str, value: ast.AST | None) -> None:
        target = f"{containing}.{name}"
        context = self.class_context_stack[-1] if self.class_context_stack else None
        tier = EvidenceTiers.TIER2 if context and (context.is_pydantic or context.is_dataclass or context.is_sqlalchemy) else EvidenceTiers.TIER3
        self.facts.append(
            create_fact(
                self.manifest,
                FactTypes.FIELD_DECLARED,
                RuleIds.PY_DECLARATIONS,
                tier,
                self._span(node, "PythonAstExtractor", ScannerVersions.AST),
                source_symbol=containing,
                target_symbol=target,
                contract_element=target,
                properties={"fieldName": name, "memberName": name, "fieldType": annotation, "containingType": containing, "targetSymbol": target},
            )
        )
        if context and (context.is_pydantic or context.is_dataclass):
            rule = RuleIds.PYDANTIC if context.is_pydantic else RuleIds.DATACLASS
            self.facts.append(
                create_fact(
                    self.manifest,
                    FactTypes.SERIALIZER_CONTRACT_MEMBER,
                    rule,
                    EvidenceTiers.TIER2,
                    self._span(node, "PythonAstExtractor", ScannerVersions.AST),
                    source_symbol=containing,
                    target_symbol=target,
                    contract_element=target,
                    properties={"contractName": containing.split(".")[-1], "memberName": name, "fieldName": name, "containingType": containing, "targetSymbol": target},
                )
            )
        if context and context.is_sqlalchemy and _is_sqlalchemy_column_value(self._call_name(value) if isinstance(value, ast.Call) else ""):
            self._column_fact(node, name, containing, value)

    def _column_fact(self, node: ast.AST, name: str, containing: str, value: ast.AST | None) -> None:
        target = f"{containing}.{name}"
        column_name = _first_call_string(value) or name
        self.facts.append(
            create_fact(
                self.manifest,
                FactTypes.DATABASE_COLUMN_MAPPING,
                RuleIds.SQLALCHEMY,
                EvidenceTiers.TIER2,
                self._span(node, "PythonAstExtractor", ScannerVersions.INTEGRATION),
                source_symbol=containing,
                target_symbol=target,
                contract_element=target,
                properties={"tableName": self.class_context_stack[-1].table_name if self.class_context_stack else None, "columnName": column_name, "fieldName": name, "memberName": name, "containingType": containing, "targetSymbol": target},
            )
        )

    def _record_framework_assignment(self, node: ast.Assign) -> None:
        if len(node.targets) != 1 or not isinstance(node.targets[0], ast.Name) or not isinstance(node.value, ast.Call):
            return
        target = node.targets[0].id
        call = self._call_name(node.value)
        if call.endswith("FastAPI"):
            self.ctx.app_vars.add(target)
        if call.endswith("APIRouter"):
            prefix = self.ctx.resolved_router_prefixes.get(target) or _kw_literal(node.value, "prefix") or ""
            self.ctx.app_vars.add(target)
            self.ctx.router_prefixes[target] = prefix
        if call.endswith("Flask") or call.endswith("Blueprint"):
            self.ctx.flask_vars.add(target)

    def _record_config_assignment(self, node: ast.Assign) -> None:
        if not self.rel.endswith(".py") or "config" not in self.rel.lower():
            return
        if len(node.targets) != 1 or not isinstance(node.targets[0], ast.Name):
            return
        if not isinstance(node.value, (ast.Constant, ast.List, ast.Tuple, ast.Dict)):
            return
        key = node.targets[0].id
        self.facts.append(create_fact(self.manifest, FactTypes.CONFIG_KEY_DECLARED, RuleIds.CONFIG, EvidenceTiers.TIER2, self._span(node, "PythonAstExtractor", ScannerVersions.CONFIG), source_symbol=self.containing_symbol, target_symbol=key, contract_element=key, properties={"keyPath": key, "name": key, "valueKind": node.value.__class__.__name__}))

    def _record_class_column_assignment(self, node: ast.Assign) -> None:
        if not self.class_stack or not self.class_context_stack or not self.class_context_stack[-1].is_sqlalchemy:
            return
        if len(node.targets) != 1 or not isinstance(node.targets[0], ast.Name):
            return
        if not _is_sqlalchemy_column_value(self._call_name(node.value)):
            return
        containing = ".".join([self.module, *self.class_stack])
        self._column_fact(node, node.targets[0].id, containing, node.value)

    def _record_field_alias(self, node: ast.Assign) -> None:
        if len(node.targets) != 1 or not isinstance(node.targets[0], ast.Attribute):
            return
        target = node.targets[0]
        if _safe_name(target.value) != "self" or not isinstance(node.value, (ast.Name, ast.Attribute)):
            return
        containing = self.containing_symbol or self.module
        field_symbol = f"{'.'.join([self.module, *self.class_stack])}.{target.attr}" if self.class_stack else f"{self.module}.{target.attr}"
        origin = _safe_name(node.value)
        origin_kind = "parameter" if self.parameters_stack and isinstance(node.value, ast.Name) and node.value.id in self.parameters_stack[-1] else "name"
        self.facts.append(
            create_fact(
                self.manifest,
                FactTypes.FIELD_ALIAS,
                RuleIds.PY_ARGUMENT,
                EvidenceTiers.TIER3,
                self._span(node, "PythonAstExtractor", ScannerVersions.AST),
                source_symbol=containing,
                target_symbol=field_symbol,
                contract_element=target.attr,
                properties={"fieldSymbol": field_symbol, "fieldSymbolKind": "field", "originSymbol": origin, "originSymbolKind": origin_kind, "expressionHash": _node_hash(node.value)},
            )
        )

    def _record_route(self, node: ast.Call) -> None:
        if not isinstance(node.func, ast.Attribute):
            return
        receiver = self._resolve_name(_name_of(node.func.value))
        method = HTTP_METHODS.get(node.func.attr)
        route_literal = _first_string(node)
        if not receiver or not route_literal:
            return
        is_fastapi = receiver in self.ctx.app_vars and ("fastapi" in self.ctx.imports or "fastapi" in self.ctx.dependencies)
        is_flask = receiver in self.ctx.flask_vars and ("flask" in self.ctx.imports or "flask" in self.ctx.dependencies)
        if not (is_fastapi or is_flask or node.func.attr == "route"):
            return
        methods = [method] if method else _methods_kw(node) or ["ANY"]
        prefix = self.ctx.router_prefixes.get(receiver, "")
        full_path = combine_paths(prefix, route_literal)
        template, key = normalize_path_key(full_path)
        for http_method in methods:
            rule = RuleIds.FASTAPI if is_fastapi else RuleIds.FLASK if is_flask else RuleIds.PY_BOUNDARY
            tier = EvidenceTiers.TIER2 if is_fastapi or is_flask else EvidenceTiers.TIER3
            handler = self.containing_symbol or self.module
            self.facts.append(
                create_fact(
                    self.manifest,
                    FactTypes.HTTP_ROUTE_BINDING,
                    rule,
                    tier,
                    self._span(node, "PythonAstExtractor", ScannerVersions.INTEGRATION),
                    source_symbol=handler,
                    target_symbol=handler,
                    contract_element=handler,
                    properties={"httpMethod": http_method, "methodName": handler.split(".")[-1], "normalizedPathTemplate": template, "normalizedPathKey": key, "routeTemplate": route_literal, "targetSymbol": handler},
                )
            )

    def _record_http_client(self, node: ast.Call) -> None:
        name = self._call_name(node)
        parts = name.split(".")
        if len(parts) < 2 or parts[0] not in {"requests", "httpx"}:
            return
        method = parts[-1].upper()
        if method == "REQUEST":
            method = (_literal_arg(node, 0) or "ANY").upper()
            url = _literal_arg(node, 1)
        else:
            url = _literal_arg(node, 0)
        props = {"httpMethod": method, "methodName": parts[-1], "targetSymbol": name}
        if url:
            template, key = normalize_path_key(url)
            props.update({"urlHash": sha256_hex(url, 32), "normalizedPathTemplate": template, "normalizedPathKey": key, "urlPath": template})
            tier = EvidenceTiers.TIER2 if parts[0] in self.ctx.imports or parts[0] in self.ctx.dependencies else EvidenceTiers.TIER3
        else:
            props.update({"urlKind": "dynamic", "dynamicReason": "non-literal-url"})
            tier = EvidenceTiers.TIER3
        self.facts.append(create_fact(self.manifest, FactTypes.HTTP_CALL_DETECTED, RuleIds.HTTP_CLIENT, tier, self._span(node, "PythonAstExtractor", ScannerVersions.INTEGRATION), source_symbol=self.containing_symbol, target_symbol=name, contract_element=name, properties=props))

    def _record_config_read(self, node: ast.Call) -> None:
        name = self._call_name(node)
        if name not in {"os.getenv", "environ.get", "os.environ.get"}:
            return
        key = _literal_arg(node, 0)
        if not key:
            self.facts.append(create_fact(self.manifest, FactTypes.ANALYSIS_GAP, RuleIds.CONFIG, EvidenceTiers.TIER4, self._span(node, "PythonAstExtractor", ScannerVersions.CONFIG), source_symbol=self.containing_symbol, target_symbol="dynamic-config-key", properties={"gapKind": "dynamic-config-key", "expressionHash": _node_hash(node)}))
            return
        self.facts.append(create_fact(self.manifest, FactTypes.CONFIG_KEY_DECLARED, RuleIds.CONFIG, EvidenceTiers.TIER2, self._span(node, "PythonAstExtractor", ScannerVersions.CONFIG), source_symbol=self.containing_symbol, target_symbol=key, contract_element=key, properties={"keyPath": key, "name": key, "accessKind": name}))

    def _record_config_subscript(self, node: ast.Subscript) -> None:
        if self._resolve_name(_safe_name(node.value)) != "os.environ":
            return
        key = _constant_string(node.slice)
        if not key:
            self.facts.append(create_fact(self.manifest, FactTypes.ANALYSIS_GAP, RuleIds.CONFIG, EvidenceTiers.TIER4, self._span(node, "PythonAstExtractor", ScannerVersions.CONFIG), source_symbol=self.containing_symbol, target_symbol="dynamic-config-key", properties={"gapKind": "dynamic-config-key", "expressionHash": _node_hash(node)}))
            return
        self.facts.append(create_fact(self.manifest, FactTypes.CONFIG_KEY_DECLARED, RuleIds.CONFIG, EvidenceTiers.TIER2, self._span(node, "PythonAstExtractor", ScannerVersions.CONFIG), source_symbol=self.containing_symbol, target_symbol=key, contract_element=key, properties={"keyPath": key, "name": key, "accessKind": "os.environ[]"}))

    def _record_sql_call(self, node: ast.Call) -> None:
        literal = _first_string(node)
        if not literal:
            if any(isinstance(arg, (ast.JoinedStr, ast.BinOp)) for arg in node.args):
                self.facts.append(create_fact(self.manifest, FactTypes.ANALYSIS_GAP, RuleIds.SQL, EvidenceTiers.TIER4, self._span(node, "PythonAstExtractor", ScannerVersions.SQL), source_symbol=self.containing_symbol, target_symbol="dynamic-sql", properties={"gapKind": "dynamic-sql", "expressionHash": _node_hash(node)}))
            return
        call = self._call_name(node)
        if not (call.endswith(".execute") or call.endswith(".executemany") or call.endswith("text") or is_sql_like(literal)):
            return
        source_kind = "orm-text" if call.endswith("text") else "dbapi-execute" if ".execute" in call else "literal-string"
        tier = EvidenceTiers.TIER2 if call.endswith("text") and "sqlalchemy" in self.ctx.imports else EvidenceTiers.TIER3
        self.facts.append(create_fact(self.manifest, FactTypes.SQL_TEXT_USED, RuleIds.SQL, tier, self._span(node, "PythonAstExtractor", ScannerVersions.SQL), source_symbol=self.containing_symbol, target_symbol=self.containing_symbol or "sql-literal", properties={"textHash": text_hash(literal), "textLength": len(literal), "operationName": operation_name(literal), "sqlSourceKind": source_kind, "targetSymbol": self.containing_symbol or "sql-literal"}))

    def _record_invocation(self, node: ast.Call) -> None:
        name = self._call_name(node)
        if not name:
            return
        self.facts.append(create_fact(self.manifest, FactTypes.INVOCATION_NAME, RuleIds.PY_INVOCATION, EvidenceTiers.TIER3, self._span(node, "PythonAstExtractor", ScannerVersions.AST), source_symbol=self.containing_symbol, target_symbol=name, contract_element=name.split(".")[-1], properties={"methodName": name.split(".")[-1], "name": name.split(".")[-1], "targetSymbol": name, "expressionHash": _node_hash(node.func)}))
        self.facts.append(create_fact(self.manifest, FactTypes.CALL_EDGE, RuleIds.PY_CALLGRAPH, EvidenceTiers.TIER3, self._span(node, "PythonAstExtractor", ScannerVersions.AST), source_symbol=self.containing_symbol, target_symbol=name, contract_element=name.split(".")[-1], properties={"callKind": "Invocation", "methodName": name.split(".")[-1], "targetSymbol": name}))

    def _record_object_creation(self, node: ast.Call) -> None:
        name = self._call_name(node)
        if not name or not name.split(".")[-1][:1].isupper():
            return
        assigned = ""
        self.facts.append(create_fact(self.manifest, FactTypes.OBJECT_CREATED, RuleIds.PY_OBJECT, EvidenceTiers.TIER3, self._span(node, "PythonAstExtractor", ScannerVersions.AST), source_symbol=self.containing_symbol, target_symbol=name, contract_element=name.split(".")[-1], properties={"createdType": name, "typeName": name.split(".")[-1], "constructorSymbol": name, "assignedTo": assigned, "argumentCount": len(node.args)}))

    def _record_argument_flow(self, node: ast.Call) -> None:
        if not self.parameters_stack:
            return
        params = self.parameters_stack[-1]
        callee = self._call_name(node)
        if not callee:
            return
        for idx, arg in enumerate(node.args):
            if isinstance(arg, ast.Name) and arg.id in params:
                self.facts.append(create_fact(self.manifest, FactTypes.ARGUMENT_PASSED, RuleIds.PY_ARGUMENT, EvidenceTiers.TIER3, self._span(node, "PythonAstExtractor", ScannerVersions.AST), source_symbol=self.containing_symbol, target_symbol=callee, contract_element=callee.split(".")[-1], properties={"parameterOrdinal": idx, "parameterName": f"arg{idx}", "argumentOrdinal": idx, "argumentExpressionKind": "Name", "argumentExpressionHash": _node_hash(arg), "argumentSymbol": arg.id, "argumentSymbolKind": "parameter", "targetSymbol": callee}))

    def _record_local_alias(self, node: ast.Assign) -> None:
        if len(node.targets) != 1 or not isinstance(node.targets[0], ast.Name):
            return
        if not isinstance(node.value, (ast.Name, ast.Attribute)):
            return
        alias = node.targets[0].id
        origin = _safe_name(node.value)
        self.facts.append(create_fact(self.manifest, FactTypes.LOCAL_ALIAS, RuleIds.PY_ARGUMENT, EvidenceTiers.TIER3, self._span(node, "PythonAstExtractor", ScannerVersions.AST), source_symbol=self.containing_symbol, target_symbol=alias, contract_element=alias, properties={"aliasSymbolKind": "local", "originSymbol": origin, "originSymbolKind": "name", "expressionHash": _node_hash(node.value)}))

    def _call_name(self, node: ast.AST | None) -> str:
        if not isinstance(node, ast.Call):
            return ""
        return self._resolve_name(_call_name(node))

    def _resolve_name(self, value: str) -> str:
        if not value:
            return value
        head, sep, tail = value.partition(".")
        resolved = self.ctx.import_aliases.get(head)
        if not resolved:
            return value
        return resolved + (sep + tail if sep else "")

    def _span(self, node: ast.AST, extractor: str, version: str):
        return evidence(self.rel, getattr(node, "lineno", 1), getattr(node, "end_lineno", getattr(node, "lineno", 1)), extractor, version)


def _gap(manifest: ScanManifest, rel: str, kind: str, message: str, line: int = 1) -> CodeFact:
    return create_fact(manifest, FactTypes.ANALYSIS_GAP, RuleIds.PY_BOUNDARY, EvidenceTiers.TIER4, evidence(rel, line, line, "PythonAstExtractor", ScannerVersions.AST), target_symbol=rel, properties={"gapKind": kind, "messageHash": sha256_hex(message, 32)})


def _prepass_python_files(files: list[ParsedPythonFile]) -> PythonPrepass:
    aliases_by_module: dict[str, dict[str, str]] = {}
    local_router_prefixes: dict[str, str] = {}
    include_prefixes: dict[str, str] = {}
    for item in files:
        aliases = _import_aliases(item.root)
        aliases_by_module[item.module] = aliases
        for node in ast.walk(item.root):
            if isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name) and isinstance(node.value, ast.Call):
                call = _resolve_alias(_call_name(node.value), aliases)
                if call.endswith("APIRouter"):
                    local_router_prefixes[f"{item.module}.{node.targets[0].id}"] = _kw_literal(node.value, "prefix") or ""
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute) and node.func.attr == "include_router" and node.args:
                router_symbol = _resolve_router_symbol(_safe_name(node.args[0]), item.module, aliases)
                if router_symbol:
                    include_prefixes[router_symbol] = _kw_literal(node, "prefix") or ""
    include_prefixes = _normalize_include_prefixes(include_prefixes, local_router_prefixes)
    router_prefixes_by_module: dict[str, dict[str, str]] = {}
    for item in files:
        aliases = aliases_by_module.get(item.module, {})
        module_prefixes: dict[str, str] = {}
        for symbol, local_prefix in local_router_prefixes.items():
            module, _, local_name = symbol.rpartition(".")
            if module != item.module:
                continue
            module_prefixes[local_name] = combine_paths(include_prefixes.get(symbol, ""), local_prefix)
        for local_name, imported in aliases.items():
            if imported in local_router_prefixes:
                module_prefixes[local_name] = combine_paths(include_prefixes.get(imported, ""), local_router_prefixes[imported])
        if module_prefixes:
            router_prefixes_by_module[item.module] = module_prefixes
    return PythonPrepass(aliases_by_module, router_prefixes_by_module)


def _normalize_include_prefixes(include_prefixes: dict[str, str], local_router_prefixes: dict[str, str]) -> dict[str, str]:
    normalized = dict(include_prefixes)
    for symbol, prefix in include_prefixes.items():
        parts = symbol.split(".")
        if len(parts) > 2:
            normalized.setdefault(".".join(parts[1:]), prefix)
        for candidate in local_router_prefixes:
            if symbol.endswith("." + candidate) or candidate.endswith("." + symbol):
                normalized.setdefault(candidate, prefix)
    return normalized


def _import_aliases(root: ast.Module) -> dict[str, str]:
    aliases: dict[str, str] = {}
    for node in root.body:
        if isinstance(node, ast.Import):
            for alias in node.names:
                root_name = alias.name.split(".")[0]
                aliases[alias.asname or root_name] = alias.name
        elif isinstance(node, ast.ImportFrom) and node.module:
            for alias in node.names:
                if alias.name != "*":
                    aliases[alias.asname or alias.name] = f"{node.module}.{alias.name}"
    return aliases


def _resolve_router_symbol(value: str, module: str, aliases: dict[str, str]) -> str:
    if not value:
        return ""
    resolved = _resolve_alias(value, aliases)
    if "." in resolved:
        return resolved
    return f"{module}.{resolved}"


def _resolve_alias(value: str, aliases: dict[str, str]) -> str:
    head, sep, tail = value.partition(".")
    resolved = aliases.get(head)
    if not resolved:
        return value
    return resolved + (sep + tail if sep else "")


def _seed_context_imports(ctx: PythonContext, aliases: dict[str, str]) -> None:
    for target in aliases.values():
        root = target.split(".", 1)[0]
        ctx.imports.add(root)
        if root == "sqlalchemy":
            ctx.sqlalchemy_imported = True


def _safe_name(node: ast.AST | None) -> str:
    if node is None:
        return ""
    if isinstance(node, ast.Name):
        return node.id
    if isinstance(node, ast.Attribute):
        base = _safe_name(node.value)
        return f"{base}.{node.attr}" if base else node.attr
    if isinstance(node, ast.Subscript):
        return _safe_name(node.value)
    if isinstance(node, ast.Call):
        return _safe_name(node.func)
    if isinstance(node, ast.Constant):
        return str(node.value)
    return node.__class__.__name__


def _call_name(node: ast.Call) -> str:
    return _safe_name(node.func)


def _name_of(node: ast.AST) -> str:
    return _safe_name(node)


def _decorator_name(node: ast.AST) -> str:
    return _safe_name(node.func if isinstance(node, ast.Call) else node)


def _first_string(node: ast.Call) -> str | None:
    return _literal_arg(node, 0)


def _literal_arg(node: ast.Call, index: int) -> str | None:
    if index >= len(node.args):
        return None
    value = node.args[index]
    return value.value if isinstance(value, ast.Constant) and isinstance(value.value, str) else None


def _kw_literal(node: ast.Call, key: str) -> str | None:
    for keyword in node.keywords:
        if keyword.arg == key and isinstance(keyword.value, ast.Constant) and isinstance(keyword.value.value, str):
            return keyword.value.value
    return None


def _methods_kw(node: ast.Call) -> list[str] | None:
    for keyword in node.keywords:
        if keyword.arg == "methods" and isinstance(keyword.value, (ast.List, ast.Tuple)):
            result = [elt.value.upper() for elt in keyword.value.elts if isinstance(elt, ast.Constant) and isinstance(elt.value, str)]
            return result or None
    return None


def _constant_string(node: ast.AST) -> str | None:
    if isinstance(node, ast.Constant) and isinstance(node.value, str):
        return node.value
    return None


def _first_call_string(node: ast.AST | None) -> str | None:
    if not isinstance(node, ast.Call) or not node.args:
        return None
    return _constant_string(node.args[0])


def _is_sqlalchemy_column_value(name: str) -> bool:
    return name.endswith("Column") or name.endswith("mapped_column")


def _tablename(statements: Iterable[ast.stmt]) -> str | None:
    for stmt in statements:
        if not _is_tablename_assignment(stmt):
            continue
        value = stmt.value if isinstance(stmt, (ast.Assign, ast.AnnAssign)) else None
        return _constant_string(value) if value else None
    return None


def _is_tablename_assignment(node: ast.stmt) -> bool:
    if isinstance(node, ast.Assign) and len(node.targets) == 1:
        target = node.targets[0]
    elif isinstance(node, ast.AnnAssign):
        target = node.target
    else:
        return False
    return isinstance(target, ast.Name) and target.id == "__tablename__"


def _node_hash(node: ast.AST) -> str:
    return sha256_hex(ast.dump(node, include_attributes=False), 32)


def _symbol_id(display: str, kind: str) -> str:
    return f"py:{kind}:{display}"


def _symbol_props(role: str, symbol_id: str, kind: str, display: str) -> dict[str, str]:
    return {
        f"{role}SymbolId": symbol_id,
        f"{role}SymbolLanguage": "python",
        f"{role}SymbolKind": kind,
        f"{role}SymbolDisplayName": display,
    }
