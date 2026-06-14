import { hash } from "../util/Hash";

const SQL_VERBS = new Set(["SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "CREATE", "ALTER", "DROP", "TRUNCATE", "CALL", "EXEC", "EXECUTE"]);
const SQL_STOP_WORDS = new Set([
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
  "WHERE"
]);

export interface SqlQueryShape {
  operationName: string;
  tableNames: string[];
  columnNames: string[];
  queryShapeHash: string;
}

export function isSqlLike(value: string): boolean {
  const first = firstToken(value);
  return SQL_VERBS.has(first) || first === "WITH";
}

export function operationName(value: string): string {
  const first = firstToken(value);
  return SQL_VERBS.has(first) ? first : "";
}

export function queryShape(value: string): SqlQueryShape {
  const normalized = normalizeSql(value);
  const operation = shapeOperation(normalized);
  return {
    operationName: operation,
    tableNames: tableNames(normalized, operation),
    columnNames: columnNames(normalized, operation),
    queryShapeHash: hash(normalized, 32)
  };
}

export function queryShapeProperties(value: string, sourceKind: string): Record<string, string> {
  const shape = queryShape(value);
  const properties: Record<string, string> = {
    textHash: hash(value, 32),
    queryShapeHash: shape.queryShapeHash,
    sqlSourceKind: sourceKind
  };
  if (shape.operationName) {
    properties.operationName = shape.operationName;
  }
  if (shape.tableNames.length > 0) {
    properties.tableName = shape.tableNames[0];
    properties.tableNames = shape.tableNames.join(";");
  }
  if (shape.columnNames.length > 0) {
    properties.columnNames = shape.columnNames.join(";");
    properties.fieldNames = properties.columnNames;
  }
  return properties;
}

export function normalizeSql(value: string): string {
  return value
    .replace(/--[^\n\r]*/g, " ")
    .replace(/\/\*.*?\*\//gs, " ")
    .replace(/'(?:''|\\['"]|[^'])*'/g, "' '")
    .replace(/"(?:""|\\["']|[^"])*"/g, "\" \"")
    .replace(/\s+/g, " ")
    .trim()
    .replace(/;+$/g, "");
}

function firstToken(value: string): string {
  return value.trimStart().split(/\s+/, 1)[0]?.toUpperCase() ?? "";
}

function shapeOperation(value: string): string {
  const first = firstToken(value);
  return SQL_VERBS.has(first) ? first : "";
}

function tableNames(sql: string, operation: string): string[] {
  const candidates: string[] = [];
  if (operation === "SELECT") {
    candidates.push(...matches(sql, /\bFROM\s+([A-Za-z_][A-Za-z0-9_.$\[\]"\x60]*)/gi));
    candidates.push(...matches(sql, /\bJOIN\s+([A-Za-z_][A-Za-z0-9_.$\[\]"\x60]*)/gi));
  } else if (operation === "INSERT") {
    candidates.push(...matches(sql, /\bINSERT\s+INTO\s+([A-Za-z_][A-Za-z0-9_.$\[\]"\x60]*)/gi));
  } else if (operation === "UPDATE") {
    candidates.push(...matches(sql, /\bUPDATE\s+([A-Za-z_][A-Za-z0-9_.$\[\]"\x60]*)/gi));
  } else if (operation === "DELETE") {
    candidates.push(...matches(sql, /\bDELETE\s+FROM\s+([A-Za-z_][A-Za-z0-9_.$\[\]"\x60]*)/gi));
  } else if (operation === "CREATE") {
    candidates.push(...matches(sql, /\bCREATE\s+(?:TEMP(?:ORARY)?\s+)?TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?([A-Za-z_][A-Za-z0-9_.$\[\]"\x60]*)/gi));
  } else if (["DROP", "TRUNCATE", "ALTER"].includes(operation)) {
    candidates.push(...matches(sql, new RegExp(`\\b${operation}\\s+(?:TABLE\\s+)?([A-Za-z_][A-Za-z0-9_.$\\[\\]"\\x60]*)`, "gi")));
  }
  return unique(candidates.map(cleanIdentifier));
}

function columnNames(sql: string, operation: string): string[] {
  if (operation === "SELECT") {
    return selectColumns(between(sql, "SELECT", "FROM"));
  }
  if (operation === "INSERT") {
    const match = sql.match(/\bINSERT\s+INTO\s+[A-Za-z_][A-Za-z0-9_.$\[\]"\x60]*\s*\(([^)]*)\)/i);
    return splitIdentifierList(match?.[1] ?? "");
  }
  if (operation === "UPDATE") {
    return unique(splitCsv(between(sql, "SET", "WHERE")).map((part) => cleanIdentifier(part.split("=", 1)[0])));
  }
  if (operation === "CREATE") {
    const match = sql.match(/\bCREATE\s+(?:TEMP(?:ORARY)?\s+)?TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?[A-Za-z_][A-Za-z0-9_.$\[\]"\x60]*\s*\((.*)\)/is);
    return createTableColumns(match?.[1] ?? "");
  }
  return [];
}

function matches(sql: string, pattern: RegExp): string[] {
  return Array.from(sql.matchAll(pattern)).map((match) => match[1]);
}

function between(sql: string, start: string, end: string): string {
  const match = sql.match(new RegExp(`\\b${start}\\b(.*?)(?:\\b${end}\\b|$)`, "is"));
  return match?.[1] ?? "";
}

function selectColumns(text: string): string[] {
  const columns: string[] = [];
  for (const part of splitCsv(text)) {
    const cleaned = part.trim().replace(/\bAS\b\s+[A-Za-z_][A-Za-z0-9_]*$/i, "");
    let token = cleaned.includes(".") ? cleaned.slice(cleaned.lastIndexOf(".") + 1).trim() : cleaned.trim();
    if (/\s/.test(token)) {
      token = token.split(/\s+/).at(-1) ?? "";
    }
    const name = cleanIdentifier(token);
    if (name && name !== "*" && !SQL_STOP_WORDS.has(name.toUpperCase()) && /^[A-Za-z_][A-Za-z0-9_]*$/.test(name)) {
      columns.push(name);
    }
  }
  return unique(columns);
}

function createTableColumns(text: string): string[] {
  const columns: string[] = [];
  for (const part of splitCsv(text)) {
    const first = part.trim().split(/\s+/, 1)[0] ?? "";
    const name = cleanIdentifier(first);
    if (name && !["CONSTRAINT", "PRIMARY", "FOREIGN", "UNIQUE", "CHECK", "KEY"].includes(name.toUpperCase())) {
      columns.push(name);
    }
  }
  return unique(columns);
}

function splitIdentifierList(text: string): string[] {
  return unique(splitCsv(text).map(cleanIdentifier));
}

function splitCsv(text: string): string[] {
  const parts: string[] = [];
  let depth = 0;
  let start = 0;
  for (let index = 0; index < text.length; index += 1) {
    const char = text[index];
    if (char === "(") {
      depth += 1;
    } else if (char === ")" && depth > 0) {
      depth -= 1;
    } else if (char === "," && depth === 0) {
      parts.push(text.slice(start, index).trim());
      start = index + 1;
    }
  }
  const tail = text.slice(start).trim();
  if (tail) {
    parts.push(tail);
  }
  return parts;
}

function cleanIdentifier(value: string): string {
  let cleaned = value.trim().replace(/^[,;]+|[,;]+$/g, "").replace(/^["\x60\[\]]+|["\x60\[\]]+$/g, "");
  if (cleaned.includes(".")) {
    cleaned = cleaned.slice(cleaned.lastIndexOf(".") + 1).replace(/^["\x60\[\]]+|["\x60\[\]]+$/g, "");
  }
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(cleaned) ? cleaned : "";
}

function unique(values: string[]): string[] {
  const seen = new Set<string>();
  const result: string[] = [];
  for (const value of values) {
    if (value && !seen.has(value)) {
      seen.add(value);
      result.push(value);
    }
  }
  return result;
}
