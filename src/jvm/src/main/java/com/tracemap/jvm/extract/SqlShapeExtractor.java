package com.tracemap.jvm.extract;

import com.tracemap.jvm.util.Hashes;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Locale;
import java.util.Set;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public final class SqlShapeExtractor {
    private static final Set<String> SQL_VERBS = Set.of("SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "CREATE", "ALTER", "DROP", "TRUNCATE", "CALL", "EXEC", "EXECUTE");
    private static final Set<String> SQL_STOP_WORDS = Set.of(
        "AND", "AS", "ASC", "BETWEEN", "BY", "CASE", "DESC", "DISTINCT", "ELSE", "END", "FALSE",
        "FROM", "GROUP", "HAVING", "IN", "IS", "JOIN", "LEFT", "LIKE", "LIMIT", "NOT", "NULL",
        "ON", "OR", "ORDER", "RIGHT", "SELECT", "SET", "THEN", "TRUE", "VALUES", "WHEN", "WHERE");

    private SqlShapeExtractor() {
    }

    public record Shape(String operationName, List<String> tableNames, List<String> columnNames, String queryShapeHash) {
        public String primaryTable() {
            return tableNames.isEmpty() ? "" : tableNames.get(0);
        }
    }

    public static boolean isSqlLike(String value) {
        String first = firstToken(value);
        return SQL_VERBS.contains(first) || "WITH".equals(first);
    }

    public static String operationName(String value) {
        String first = firstToken(value);
        return SQL_VERBS.contains(first) ? first : "";
    }

    public static Shape queryShape(String value) {
        String normalized = normalizeSql(value);
        String operation = shapeOperation(normalized);
        return new Shape(operation, tableNames(normalized, operation), columnNames(normalized, operation), Hashes.sha256(normalized, 32));
    }

    public static Map<String, String> queryShapeProperties(String value, String sourceKind) {
        Shape shape = queryShape(value);
        Map<String, String> props = new LinkedHashMap<>();
        props.put("textHash", Hashes.sha256(value, 32));
        props.put("queryShapeHash", shape.queryShapeHash());
        props.put("sqlSourceKind", sourceKind);
        if (!shape.operationName().isBlank()) {
            props.put("operationName", shape.operationName());
        }
        if (!shape.primaryTable().isBlank()) {
            props.put("tableName", shape.primaryTable());
        }
        if (!shape.tableNames().isEmpty()) {
            props.put("tableNames", String.join(";", shape.tableNames()));
        }
        if (!shape.columnNames().isEmpty()) {
            props.put("columnNames", String.join(";", shape.columnNames()));
            props.put("fieldNames", String.join(";", shape.columnNames()));
        }
        return props;
    }

    public static String normalizeSql(String value) {
        value = value.replaceAll("--[^\\n\\r]*", " ");
        value = Pattern.compile("/\\*.*?\\*/", Pattern.DOTALL).matcher(value).replaceAll(" ");
        value = value.replaceAll("'(?:''|\\\\['\"]|[^'])*'", "' '");
        value = value.replaceAll("\"(?:\"\"|\\\\[\"']|[^\"])*\"", "\" \"");
        value = value.replaceAll("\\s+", " ").trim();
        return value.replaceAll(";+\\z", "");
    }

    private static String shapeOperation(String value) {
        String first = firstToken(value);
        return SQL_VERBS.contains(first) ? first : "";
    }

    private static String firstToken(String value) {
        String trimmed = value.stripLeading();
        if (trimmed.isEmpty()) {
            return "";
        }
        return trimmed.split("\\s+", 2)[0].toUpperCase(Locale.ROOT);
    }

    private static List<String> tableNames(String sql, String operation) {
        List<String> candidates = new ArrayList<>();
        switch (operation) {
            case "SELECT" -> {
                candidates.addAll(matches(sql, "\\bFROM\\s+([A-Za-z_][A-Za-z0-9_.$\\[\\]\"`]*)"));
                candidates.addAll(matches(sql, "\\bJOIN\\s+([A-Za-z_][A-Za-z0-9_.$\\[\\]\"`]*)"));
            }
            case "INSERT" -> candidates.addAll(matches(sql, "\\bINSERT\\s+INTO\\s+([A-Za-z_][A-Za-z0-9_.$\\[\\]\"`]*)"));
            case "UPDATE" -> candidates.addAll(matches(sql, "\\bUPDATE\\s+([A-Za-z_][A-Za-z0-9_.$\\[\\]\"`]*)"));
            case "DELETE" -> candidates.addAll(matches(sql, "\\bDELETE\\s+FROM\\s+([A-Za-z_][A-Za-z0-9_.$\\[\\]\"`]*)"));
            case "CREATE" -> candidates.addAll(matches(sql, "\\bCREATE\\s+(?:TEMP(?:ORARY)?\\s+)?TABLE\\s+(?:IF\\s+NOT\\s+EXISTS\\s+)?([A-Za-z_][A-Za-z0-9_.$\\[\\]\"`]*)"));
            case "DROP", "TRUNCATE", "ALTER" -> candidates.addAll(matches(sql, "\\b" + operation + "\\s+(?:TABLE\\s+)?([A-Za-z_][A-Za-z0-9_.$\\[\\]\"`]*)"));
            default -> {
            }
        }
        return unique(candidates.stream().map(SqlShapeExtractor::cleanIdentifier).toList());
    }

    private static List<String> columnNames(String sql, String operation) {
        return switch (operation) {
            case "SELECT" -> selectColumns(between(sql, "SELECT", "FROM"));
            case "INSERT" -> splitIdentifierList(matchGroup(sql, "\\bINSERT\\s+INTO\\s+[A-Za-z_][A-Za-z0-9_.$\\[\\]\"`]*\\s*\\(([^)]*)\\)", 0));
            case "UPDATE" -> unique(splitCsv(between(sql, "SET", "WHERE")).stream().map(part -> cleanIdentifier(part.split("=", 2)[0])).toList());
            case "CREATE" -> createTableColumns(matchGroup(sql, "\\bCREATE\\s+(?:TEMP(?:ORARY)?\\s+)?TABLE\\s+(?:IF\\s+NOT\\s+EXISTS\\s+)?[A-Za-z_][A-Za-z0-9_.$\\[\\]\"`]*\\s*\\((.*)\\)", Pattern.DOTALL));
            default -> List.of();
        };
    }

    private static List<String> matches(String sql, String pattern) {
        Matcher matcher = Pattern.compile(pattern, Pattern.CASE_INSENSITIVE).matcher(sql);
        List<String> result = new ArrayList<>();
        while (matcher.find()) {
            result.add(matcher.group(1));
        }
        return result;
    }

    private static String matchGroup(String sql, String pattern, int flags) {
        Matcher matcher = Pattern.compile(pattern, Pattern.CASE_INSENSITIVE | flags).matcher(sql);
        return matcher.find() ? matcher.group(1) : "";
    }

    private static String between(String sql, String start, String end) {
        Matcher matcher = Pattern.compile("\\b" + start + "\\b(.*?)(?:\\b" + end + "\\b|$)", Pattern.CASE_INSENSITIVE | Pattern.DOTALL).matcher(sql);
        return matcher.find() ? matcher.group(1) : "";
    }

    private static List<String> selectColumns(String text) {
        List<String> columns = new ArrayList<>();
        for (String part : splitCsv(text)) {
            String cleaned = part.trim().replaceAll("(?i)\\bAS\\b\\s+[A-Za-z_][A-Za-z0-9_]*$", "");
            String token = cleaned.contains(".") ? cleaned.substring(cleaned.lastIndexOf('.') + 1).trim() : cleaned.trim();
            if (Pattern.compile("\\s").matcher(token).find()) {
                String[] pieces = token.split("\\s+");
                token = pieces.length == 0 ? "" : pieces[pieces.length - 1];
            }
            String name = cleanIdentifier(token);
            if (!name.isBlank() && !"*".equals(name) && !SQL_STOP_WORDS.contains(name.toUpperCase(Locale.ROOT)) && name.matches("^[A-Za-z_][A-Za-z0-9_]*$")) {
                columns.add(name);
            }
        }
        return unique(columns);
    }

    private static List<String> createTableColumns(String text) {
        List<String> columns = new ArrayList<>();
        for (String part : splitCsv(text)) {
            String[] pieces = part.trim().split("\\s+", 2);
            String name = cleanIdentifier(pieces.length == 0 ? "" : pieces[0]);
            if (!name.isBlank() && !Set.of("CONSTRAINT", "PRIMARY", "FOREIGN", "UNIQUE", "CHECK", "KEY").contains(name.toUpperCase(Locale.ROOT))) {
                columns.add(name);
            }
        }
        return unique(columns);
    }

    private static List<String> splitIdentifierList(String text) {
        return unique(splitCsv(text).stream().map(SqlShapeExtractor::cleanIdentifier).toList());
    }

    private static List<String> splitCsv(String text) {
        List<String> parts = new ArrayList<>();
        int depth = 0;
        int start = 0;
        for (int index = 0; index < text.length(); index++) {
            char current = text.charAt(index);
            if (current == '(') {
                depth++;
            } else if (current == ')' && depth > 0) {
                depth--;
            } else if (current == ',' && depth == 0) {
                parts.add(text.substring(start, index).trim());
                start = index + 1;
            }
        }
        String tail = text.substring(start).trim();
        if (!tail.isBlank()) {
            parts.add(tail);
        }
        return parts;
    }

    private static String cleanIdentifier(String value) {
        String cleaned = value.trim().replaceAll("^[,;]+|[,;]+$", "").replaceAll("^[\"`\\[\\]]+|[\"`\\[\\]]+$", "");
        if (cleaned.contains(".")) {
            cleaned = cleaned.substring(cleaned.lastIndexOf('.') + 1).replaceAll("^[\"`\\[\\]]+|[\"`\\[\\]]+$", "");
        }
        return cleaned.matches("^[A-Za-z_][A-Za-z0-9_]*$") ? cleaned : "";
    }

    private static List<String> unique(List<String> values) {
        LinkedHashSet<String> seen = new LinkedHashSet<>();
        for (String value : values) {
            if (value != null && !value.isBlank()) {
                seen.add(value);
            }
        }
        return new ArrayList<>(seen);
    }
}
