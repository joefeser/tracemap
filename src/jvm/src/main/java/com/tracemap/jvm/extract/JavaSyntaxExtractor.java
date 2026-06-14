package com.tracemap.jvm.extract;

import com.tracemap.jvm.facts.FactFactory;
import com.tracemap.jvm.model.CodeFact;
import com.tracemap.jvm.model.EvidenceTiers;
import com.tracemap.jvm.model.FactTypes;
import com.tracemap.jvm.model.FileInventoryItem;
import com.tracemap.jvm.model.RuleIds;
import com.tracemap.jvm.model.ScanManifest;
import com.tracemap.jvm.model.ScannerVersions;
import com.tracemap.jvm.util.Hashes;
import java.io.IOException;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public final class JavaSyntaxExtractor {
    private static final Pattern PACKAGE = Pattern.compile("^\\s*package\\s+([A-Za-z_][\\w.]*)\\s*;");
    private static final Pattern IMPORT = Pattern.compile("^\\s*import\\s+([A-Za-z_][\\w.]*)(?:\\.\\*)?\\s*;");
    private static final Pattern TYPE = Pattern.compile("\\b(class|interface|enum|record)\\s+([A-Za-z_][\\w]*)([^\\{;]*)");
    private static final Pattern METHOD = Pattern.compile("(?:public|protected|private|static|final|native|synchronized|abstract|\\s)+[\\w<>\\[\\].?,\\s]+\\s+([A-Za-z_][\\w]*)\\s*\\(([^)]*)\\)\\s*(?:throws[^{]+)?\\{?");
    private static final Pattern FIELD = Pattern.compile("(?:public|protected|private|static|final|volatile|transient|\\s)+([A-Za-z_][\\w<>\\[\\].?,]*)\\s+([A-Za-z_][\\w]*)\\s*(?:=|;)");
    private static final Pattern MEMBER = Pattern.compile("\\.([A-Za-z_][\\w]*)\\b");
    private static final Pattern INVOCATION = Pattern.compile("(?<!new\\s)\\b([A-Za-z_][\\w.]*)\\s*\\(([^;{}]*)\\)");
    private static final Pattern NEW_OBJECT = Pattern.compile("\\bnew\\s+([A-Za-z_][\\w.]*)\\s*\\(([^)]*)\\)");
    private static final Pattern ANNOTATION = Pattern.compile("@([A-Za-z_][\\w.]*)\\s*(?:\\((.*)\\))?");
    private static final Pattern STRING = Pattern.compile("\"((?:\\\\.|[^\"\\\\])*)\"");
    private static final int ROUTE_ANNOTATION_LOOKBACK_LINES = 24;

    private JavaSyntaxExtractor() {
    }

    public static List<CodeFact> extract(ScanManifest manifest, List<FileInventoryItem> files) {
        List<CodeFact> facts = new ArrayList<>();
        for (FileInventoryItem file : files) {
            if (!"Java".equals(file.kind()) || file.skipped()) {
                continue;
            }
            try {
                extractFile(manifest, file, facts);
            } catch (IOException ignored) {
                // The scan engine emits a gap for unreadable files during orchestration.
            }
        }
        return facts;
    }

    private static void extractFile(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts) throws IOException {
        List<String> lines = Files.readAllLines(file.absolutePath());
        String pkg = "";
        List<String> imports = new ArrayList<>();
        String currentType = null;
        String currentSymbol = null;
        String classRoute = "";
        String classRouteMethod = "";
        boolean springEvidence = false;
        boolean jaxEvidence = false;
        for (int i = 0; i < lines.size(); i++) {
            String line = lines.get(i);
            int lineNo = i + 1;
            Matcher pkgMatcher = PACKAGE.matcher(line);
            if (pkgMatcher.find()) {
                pkg = pkgMatcher.group(1);
            }
            Matcher importMatcher = IMPORT.matcher(line);
            if (importMatcher.find()) {
                imports.add(importMatcher.group(1));
                springEvidence |= importMatcher.group(1).startsWith("org.springframework.");
                jaxEvidence |= importMatcher.group(1).startsWith("jakarta.ws.rs.") || importMatcher.group(1).startsWith("javax.ws.rs.");
            }

            Matcher annotationMatcher = ANNOTATION.matcher(line);
            while (annotationMatcher.find()) {
                String annotation = simple(annotationMatcher.group(1));
                String annotationArgs = annotationMatcher.group(2) == null ? "" : annotationMatcher.group(2);
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.ATTRIBUTE_USED,
                    RuleIds.JAVA_SYNTAX_DECLARATIONS,
                    EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                    FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                    file.relativePath(),
                    currentSymbol,
                    annotation,
                    null,
                    props("attributeName", annotation, "name", annotation, "argumentHash", Hashes.sha256(annotationArgs, 32))));
                RouteAnnotation route = routeAnnotation(annotation, annotationArgs);
                if (route != null && currentType == null) {
                    classRoute = route.path();
                    classRouteMethod = route.method();
                }
            }

            Matcher typeMatcher = TYPE.matcher(line);
            if (typeMatcher.find()) {
                String kind = typeMatcher.group(1);
                String name = typeMatcher.group(2);
                String tail = typeMatcher.group(3);
                currentType = name;
                currentSymbol = qualify(pkg, name);
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.TYPE_DECLARED,
                    RuleIds.JAVA_SYNTAX_DECLARATIONS,
                    EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                    FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                    file.relativePath(),
                    null,
                    currentSymbol,
                    currentSymbol,
                    props("language", "java", "declarationKind", kind, "name", name, "typeName", name, "namespace", pkg, "targetSymbol", currentSymbol)));
                emitRelationships(manifest, file, facts, currentSymbol, tail, lineNo);
                if (line.contains("@Entity") || hasNearbyAnnotation(lines, i, "Entity")) {
                    facts.add(FactFactory.create(
                        manifest,
                        FactTypes.DATABASE_COLUMN_MAPPING,
                        RuleIds.JPA,
                        springEvidence ? EvidenceTiers.TIER2_STRUCTURAL : EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                        FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.INTEGRATION),
                        file.relativePath(),
                        currentSymbol,
                        currentSymbol,
                        currentSymbol,
                        props("mappingKind", "Entity", "className", name, "containingType", currentSymbol, "targetSymbol", currentSymbol, "name", name)));
                }
            }

            Matcher methodMatcher = METHOD.matcher(line);
            if (currentType != null && methodMatcher.find() && !line.trim().startsWith("if ") && !line.trim().startsWith("for ") && !line.trim().startsWith("while ")) {
                String methodName = methodMatcher.group(1);
                String parameters = methodMatcher.group(2);
                String methodSymbol = qualify(pkg, currentType) + "." + methodName;
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.METHOD_DECLARED,
                    RuleIds.JAVA_SYNTAX_DECLARATIONS,
                    EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                    FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                    file.relativePath(),
                    currentSymbol,
                    methodSymbol,
                    methodSymbol,
                    props("language", "java", "methodName", methodName, "name", methodName, "containingType", qualify(pkg, currentType), "targetSymbol", methodSymbol, "parameterCount", countArgs(parameters))));
                emitParameters(manifest, file, facts, methodSymbol, parameters, lineNo);
                RouteAnnotation route = routeBefore(lines, i);
                if (route != null) {
                    String method = route.method().isBlank() ? (classRouteMethod.isBlank() ? "ANY" : classRouteMethod) : route.method();
                    String path = normalizeRoute(classRoute + "/" + route.path());
                    String tier = (springEvidence || jaxEvidence) ? EvidenceTiers.TIER2_STRUCTURAL : EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL;
                    facts.add(FactFactory.create(
                        manifest,
                        FactTypes.HTTP_ROUTE_BINDING,
                        RuleIds.HTTP_ROUTE,
                        tier,
                        FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.INTEGRATION),
                        file.relativePath(),
                        methodSymbol,
                        methodSymbol,
                        methodSymbol,
                        props("httpMethod", method, "normalizedPathTemplate", path, "normalizedPathKey", method + " " + path.toLowerCase(), "methodName", methodName, "controllerName", currentType, "containingType", qualify(pkg, currentType), "targetSymbol", methodSymbol, "routePatternHash", Hashes.sha256(path, 32))));
                }
            }

            Matcher fieldMatcher = FIELD.matcher(line);
            if (currentType != null && fieldMatcher.find() && !line.contains("(")) {
                String fieldType = fieldMatcher.group(1);
                String fieldName = fieldMatcher.group(2);
                String fieldSymbol = qualify(pkg, currentType) + "." + fieldName;
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.FIELD_DECLARED,
                    RuleIds.JAVA_SYNTAX_DECLARATIONS,
                    EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                    FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                    file.relativePath(),
                    currentSymbol,
                    fieldSymbol,
                    fieldSymbol,
                    props("fieldName", fieldName, "fieldType", fieldType, "containingType", qualify(pkg, currentType), "targetSymbol", fieldSymbol)));
                if (hasNearbyAnnotation(lines, i, "Column") || hasNearbyAnnotation(lines, i, "Id")) {
                    facts.add(FactFactory.create(
                        manifest,
                        FactTypes.DATABASE_COLUMN_MAPPING,
                        RuleIds.JPA,
                        EvidenceTiers.TIER2_STRUCTURAL,
                        FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.INTEGRATION),
                        file.relativePath(),
                        currentSymbol,
                        fieldSymbol,
                        fieldSymbol,
                        props("mappingKind", "Column", "fieldName", fieldName, "columnName", annotationStringBefore(lines, i, "Column"), "containingType", qualify(pkg, currentType), "targetSymbol", fieldSymbol, "name", fieldName)));
                }
                String jsonName = annotationStringBefore(lines, i, "JsonProperty");
                if (!jsonName.isBlank()) {
                    facts.add(FactFactory.create(
                        manifest,
                        FactTypes.SERIALIZER_CONTRACT_MEMBER,
                        RuleIds.SERIALIZER,
                        EvidenceTiers.TIER2_STRUCTURAL,
                        FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.INTEGRATION),
                        file.relativePath(),
                        currentSymbol,
                        fieldSymbol,
                        qualify(pkg, currentType) + "." + jsonName,
                        props("contractName", jsonName, "memberName", fieldName, "containingType", qualify(pkg, currentType), "targetSymbol", fieldSymbol, "serializer", "Jackson")));
                }
            }

            emitMemberAndCallFacts(manifest, file, facts, currentSymbol, currentType == null ? "" : qualify(pkg, currentType), line, lineNo);
            emitIntegrationFacts(manifest, file, facts, currentSymbol, currentType == null ? "" : qualify(pkg, currentType), line, lineNo);
            emitLogicFacts(manifest, file, facts, currentSymbol, line, lineNo);
        }
    }

    private static void emitRelationships(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, String sourceSymbol, String tail, int lineNo) {
        Matcher extendsMatcher = Pattern.compile("\\bextends\\s+([A-Za-z_][\\w.]*)").matcher(tail);
        if (extendsMatcher.find()) {
            emitRelationship(manifest, file, facts, sourceSymbol, extendsMatcher.group(1), "ExtendsClass", lineNo);
        }
        Matcher implementsMatcher = Pattern.compile("\\bimplements\\s+([A-Za-z_][\\w.,\\s]*)").matcher(tail);
        if (implementsMatcher.find()) {
            for (String target : implementsMatcher.group(1).split(",")) {
                emitRelationship(manifest, file, facts, sourceSymbol, target.trim(), "ImplementsInterface", lineNo);
            }
        }
    }

    private static void emitRelationship(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, String sourceSymbol, String target, String kind, int lineNo) {
        if (target.isBlank()) return;
        String sourceId = "jvm:java:source:" + sourceSymbol;
        String targetId = "jvm:java:source:" + target;
        facts.add(FactFactory.create(
            manifest,
            FactTypes.SYMBOL_RELATIONSHIP,
            RuleIds.JAVA_SYNTAX_DECLARATIONS,
            EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
            FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
            file.relativePath(),
            sourceSymbol,
            target,
            target,
            props("relationshipKind", kind, "sourceSymbol", sourceSymbol, "targetSymbol", target, "sourceSymbolId", sourceId, "targetSymbolId", targetId, "sourceSymbolKind", "Type", "targetSymbolKind", "Type", "sourceLanguage", "java", "targetLanguage", "java")));
    }

    private static void emitParameters(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, String methodSymbol, String parameters, int lineNo) {
        if (parameters == null || parameters.isBlank()) return;
        int ordinal = 0;
        for (String parameter : parameters.split(",")) {
            String[] parts = parameter.trim().split("\\s+");
            if (parts.length < 2) continue;
            String name = parts[parts.length - 1].replace("...", "").trim();
            String type = parts[parts.length - 2].trim();
            facts.add(FactFactory.create(
                manifest,
                FactTypes.PARAMETER_DECLARED,
                RuleIds.JAVA_SYNTAX_DECLARATIONS,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                file.relativePath(),
                methodSymbol,
                methodSymbol + "(" + name + ")",
                methodSymbol + "." + name,
                props("parameterName", name, "parameterType", type, "parameterOrdinal", String.valueOf(ordinal), "sourceSymbol", methodSymbol)));
            ordinal++;
        }
    }

    private static void emitMemberAndCallFacts(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, String currentSymbol, String containingType, String line, int lineNo) {
        Matcher memberMatcher = MEMBER.matcher(line);
        while (memberMatcher.find()) {
            String member = memberMatcher.group(1);
            if ("class".equals(member)) continue;
            facts.add(FactFactory.create(
                manifest,
                FactTypes.MEMBER_ACCESS_NAME,
                RuleIds.JAVA_SYNTAX_INVOCATION,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                file.relativePath(),
                currentSymbol,
                member,
                member,
                props("memberName", member, "name", member, "containingType", containingType, "expressionHash", Hashes.sha256(line.trim(), 32))));
        }
        Matcher invocationMatcher = INVOCATION.matcher(line);
        while (invocationMatcher.find()) {
            String callee = invocationMatcher.group(1);
            String methodName = simple(callee);
            if (List.of("if", "for", "while", "switch", "catch", "return", "new").contains(methodName)) continue;
            facts.add(FactFactory.create(
                manifest,
                FactTypes.INVOCATION_NAME,
                RuleIds.JAVA_SYNTAX_INVOCATION,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                file.relativePath(),
                currentSymbol,
                callee,
                callee,
                props("methodName", methodName, "name", methodName, "targetSymbol", callee, "containingType", containingType, "argumentCount", countArgs(invocationMatcher.group(2)), "expressionHash", Hashes.sha256(invocationMatcher.group(0), 32))));
            facts.add(FactFactory.create(
                manifest,
                FactTypes.CALL_EDGE,
                RuleIds.JAVA_SYNTAX_CALLGRAPH,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                file.relativePath(),
                currentSymbol,
                callee,
                callee,
                props("callerSymbol", safe(currentSymbol), "calleeSymbol", callee, "methodName", methodName, "targetSymbol", callee, "containingType", containingType, "callKind", "Method")));
            emitArgumentFacts(manifest, file, facts, currentSymbol, callee, invocationMatcher.group(2), lineNo);
        }
        Matcher newMatcher = NEW_OBJECT.matcher(line);
        while (newMatcher.find()) {
            String type = newMatcher.group(1);
            facts.add(FactFactory.create(
                manifest,
                FactTypes.OBJECT_CREATED,
                RuleIds.JAVA_SYNTAX_OBJECT_CREATION,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                file.relativePath(),
                currentSymbol,
                type,
                type,
                props("createdType", type, "constructorSymbol", type + ".<init>", "argumentCount", countArgs(newMatcher.group(2)), "assignedTo", assignedName(line), "targetSymbol", type)));
        }
    }

    private static void emitArgumentFacts(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, String currentSymbol, String callee, String args, int lineNo) {
        if (args == null || args.isBlank()) return;
        String[] parts = args.split(",");
        for (int i = 0; i < parts.length; i++) {
            String arg = parts[i].trim();
            if (arg.isBlank()) continue;
            facts.add(FactFactory.create(
                manifest,
                FactTypes.ARGUMENT_PASSED,
                RuleIds.JAVA_SYNTAX_CALLGRAPH,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                file.relativePath(),
                currentSymbol,
                callee,
                callee,
                props("parameterOrdinal", String.valueOf(i), "parameterName", "arg" + i, "argumentOrdinal", String.valueOf(i), "argumentExpressionKind", expressionKind(arg), "argumentExpressionHash", Hashes.sha256(arg, 32), "callKind", "Method")));
        }
    }

    private static void emitIntegrationFacts(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, String currentSymbol, String containingType, String line, int lineNo) {
        Matcher queryMatcher = Pattern.compile("@Query\\s*\\(\\s*\"((?:\\\\.|[^\"\\\\])*)\"").matcher(line);
        if (queryMatcher.find()) {
            String sql = queryMatcher.group(1);
            facts.addAll(sqlFacts(manifest, file, currentSymbol, containingType, sql, lineNo, "JpaQuery"));
        }
        Matcher jdbcMatcher = Pattern.compile("\\.(prepareStatement|execute|executeQuery|executeUpdate)\\s*\\(\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*\\)").matcher(line);
        if (jdbcMatcher.find()) {
            facts.addAll(sqlFacts(manifest, file, currentSymbol, containingType, jdbcMatcher.group(2), lineNo, jdbcMatcher.group(1)));
        }
        Matcher valueMatcher = Pattern.compile("@Value\\s*\\(\\s*\"((?:\\\\.|[^\"\\\\])*)\"").matcher(line);
        if (valueMatcher.find()) {
            String key = valueMatcher.group(1).replace("${", "").replace("}", "");
            facts.add(configUseFact(manifest, file, currentSymbol, key, lineNo, "@Value"));
        }
        Matcher configMatcher = Pattern.compile("(?:System\\.getenv|System\\.getProperty|\\.getProperty)\\s*\\(\\s*\"((?:\\\\.|[^\"\\\\])*)\"").matcher(line);
        if (configMatcher.find()) {
            facts.add(configUseFact(manifest, file, currentSymbol, configMatcher.group(1), lineNo, "getProperty"));
        }
        if (line.contains("ObjectMapper") || line.contains(".readValue(") || line.contains(".writeValueAsString(")) {
            facts.add(FactFactory.create(
                manifest,
                FactTypes.SERIALIZATION_LOGIC,
                RuleIds.SERIALIZER,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.INTEGRATION),
                file.relativePath(),
                currentSymbol,
                currentSymbol,
                currentSymbol,
                props("operationName", "JacksonObjectMapper", "targetSymbol", safe(currentSymbol), "containingType", containingType, "expressionHash", Hashes.sha256(line.trim(), 32))));
        }
    }

    private static List<CodeFact> sqlFacts(ScanManifest manifest, FileInventoryItem file, String currentSymbol, String containingType, String sql, int lineNo, String methodName) {
        String sourceKind = "JpaQuery".equals(methodName) ? "orm-text" : "literal-string";
        Map<String, String> textProps = props("textHash", Hashes.sha256(sql, 32), "textLength", String.valueOf(sql.length()), "sqlSourceKind", sourceKind, "targetSymbol", safe(currentSymbol), "containingType", containingType, "methodName", methodName);
        String operation = SqlShapeExtractor.operationName(sql);
        if (!operation.isBlank()) {
            textProps.put("operationName", operation);
        }
        List<CodeFact> facts = new ArrayList<>();
        facts.add(FactFactory.create(
            manifest,
            FactTypes.SQL_TEXT_USED,
            RuleIds.SQL,
            EvidenceTiers.TIER2_STRUCTURAL,
            FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.INTEGRATION),
            file.relativePath(),
            currentSymbol,
            currentSymbol,
            currentSymbol,
            textProps));
        if (SqlShapeExtractor.isSqlLike(sql)) {
            Map<String, String> shapeProps = new LinkedHashMap<>(SqlShapeExtractor.queryShapeProperties(sql, sourceKind));
            shapeProps.put("containingType", containingType);
            shapeProps.put("methodName", methodName);
            String target = shapeProps.getOrDefault("tableName", safe(currentSymbol));
            shapeProps.put("targetSymbol", target);
            facts.add(FactFactory.create(
                manifest,
                FactTypes.QUERY_PATTERN_DETECTED,
                RuleIds.SQL,
                EvidenceTiers.TIER2_STRUCTURAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.INTEGRATION),
                file.relativePath(),
                currentSymbol,
                target,
                target,
                shapeProps));
        }
        return facts;
    }

    private static CodeFact configUseFact(ScanManifest manifest, FileInventoryItem file, String currentSymbol, String key, int lineNo, String operation) {
        return FactFactory.create(
            manifest,
            FactTypes.CONFIG_KEY_DECLARED,
            RuleIds.CONFIG_USE,
            EvidenceTiers.TIER2_STRUCTURAL,
            FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.INTEGRATION),
            file.relativePath(),
            currentSymbol,
            key,
            key,
            props("operationName", operation, "keyPath", key, "name", key, "targetSymbol", key));
    }

    private static void emitLogicFacts(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, String currentSymbol, String line, int lineNo) {
        String trimmed = line.trim();
        String calculationSource = STRING.matcher(line).replaceAll("\"\"");
        if (!trimmed.startsWith("@") && calculationSource.matches(".*[A-Za-z0-9_)]\\s*[+\\-*/%]\\s*[A-Za-z0-9_(].*")) {
            facts.add(FactFactory.create(
                manifest,
                FactTypes.CALCULATION_EXPRESSION,
                RuleIds.JAVA_SYNTAX_LOGIC,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                file.relativePath(),
                currentSymbol,
                currentSymbol,
                currentSymbol,
                props("expressionHash", Hashes.sha256(line.trim(), 32), "operatorKinds", operators(line))));
        }
        if (line.matches("\\s*(if|switch|for|while)\\b.*")) {
            facts.add(FactFactory.create(
                manifest,
                FactTypes.BRANCHING_LOGIC,
                RuleIds.JAVA_SYNTAX_LOGIC,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                file.relativePath(),
                currentSymbol,
                currentSymbol,
                currentSymbol,
                props("statementKind", line.trim().split("\\s+")[0], "conditionExpressionHash", Hashes.sha256(line.trim(), 32))));
        }
        if (file.relativePath().contains("/generated/") || file.relativePath().contains("/test/") || line.contains("@SpringBootApplication")) {
            facts.add(FactFactory.create(
                manifest,
                FactTypes.INFRASTRUCTURE_BOILERPLATE,
                RuleIds.JAVA_SYNTAX_LOGIC,
                EvidenceTiers.TIER2_STRUCTURAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "JavaSyntaxExtractor", ScannerVersions.JAVA_SYNTAX),
                file.relativePath(),
                currentSymbol,
                currentSymbol,
                null,
                props("boilerplateKind", line.contains("@SpringBootApplication") ? "SpringBootEntrypoint" : "PathPattern")));
        }
    }

    private static boolean hasNearbyAnnotation(List<String> lines, int index, String annotation) {
        for (int i = Math.max(0, index - 3); i < index; i++) {
            if (lines.get(i).contains("@" + annotation)) return true;
        }
        return false;
    }

    private static String annotationStringBefore(List<String> lines, int index, String annotation) {
        for (int i = Math.max(0, index - 3); i < index; i++) {
            String line = lines.get(i);
            if (line.contains("@" + annotation)) {
                Matcher matcher = STRING.matcher(line);
                if (matcher.find()) return matcher.group(1);
            }
        }
        return "";
    }

    private static RouteAnnotation routeBefore(List<String> lines, int index) {
        StringBuilder annotationBlock = new StringBuilder();
        int firstLine = Math.max(0, index - ROUTE_ANNOTATION_LOOKBACK_LINES);
        for (int i = index - 1; i >= firstLine; i--) {
            String line = lines.get(i).trim();
            if (line.isBlank() || line.startsWith("//") || line.startsWith("/*") || line.startsWith("*")) {
                continue;
            }
            if (isRouteAnnotationBoundary(line)) {
                return null;
            }
            annotationBlock.insert(0, line + " ");
            if (line.startsWith("@")) {
                Matcher matcher = ANNOTATION.matcher(annotationBlock.toString().trim());
                while (matcher.find()) {
                    RouteAnnotation route = routeAnnotation(simple(matcher.group(1)), matcher.group(2) == null ? "" : matcher.group(2));
                    if (route != null) return route;
                }
                annotationBlock.setLength(0);
            }
        }
        return null;
    }

    private static boolean isRouteAnnotationBoundary(String line) {
        return line.equals("}")
            || TYPE.matcher(line).find()
            || METHOD.matcher(line).find()
            || FIELD.matcher(line).find();
    }

    private static RouteAnnotation routeAnnotation(String annotation, String args) {
        String method = switch (annotation) {
            case "GetMapping", "GET" -> "GET";
            case "PostMapping", "POST" -> "POST";
            case "PutMapping", "PUT" -> "PUT";
            case "PatchMapping", "PATCH" -> "PATCH";
            case "DeleteMapping", "DELETE" -> "DELETE";
            case "RequestMapping", "Path" -> "";
            default -> null;
        };
        if (method == null) return null;
        Matcher stringMatcher = STRING.matcher(args);
        String path = stringMatcher.find() ? stringMatcher.group(1) : "";
        return new RouteAnnotation(method, path);
    }

    private record RouteAnnotation(String method, String path) {
    }

    private static String normalizeRoute(String value) {
        String normalized = value == null ? "" : value.trim();
        normalized = normalized.replace("[controller]", "{controller}");
        normalized = normalized.replaceAll("\\{([^}:?]+)(?::[^}?]+)?\\??}", "{$1}");
        normalized = normalized.replaceAll("/+", "/");
        if (!normalized.startsWith("/")) normalized = "/" + normalized;
        if (normalized.length() > 1 && normalized.endsWith("/")) normalized = normalized.substring(0, normalized.length() - 1);
        return normalized;
    }

    private static String assignedName(String line) {
        Matcher matcher = Pattern.compile("\\b([A-Za-z_][\\w]*)\\s*=\\s*new\\b").matcher(line);
        return matcher.find() ? matcher.group(1) : "";
    }

    private static String expressionKind(String expression) {
        if (expression.matches("[A-Za-z_][\\w]*")) return "Identifier";
        if (expression.startsWith("\"")) return "StringLiteral";
        if (expression.matches("-?\\d+(\\.\\d+)?")) return "NumericLiteral";
        if (expression.contains(".")) return "MemberAccess";
        return "Expression";
    }

    private static String operators(String line) {
        List<String> ops = new ArrayList<>();
        if (line.contains("+")) ops.add("Add");
        if (line.contains("-")) ops.add("Subtract");
        if (line.contains("*")) ops.add("Multiply");
        if (line.contains("/")) ops.add("Divide");
        if (line.contains("%")) ops.add("Modulo");
        return String.join(",", ops);
    }

    private static String countArgs(String args) {
        if (args == null || args.isBlank()) return "0";
        return String.valueOf(args.split(",").length);
    }

    private static String simple(String name) {
        int dot = name.lastIndexOf('.');
        return dot >= 0 ? name.substring(dot + 1) : name;
    }

    private static String qualify(String pkg, String name) {
        return pkg == null || pkg.isBlank() ? name : pkg + "." + name;
    }

    private static String safe(String value) {
        return value == null ? "" : value;
    }

    private static Map<String, String> props(String... values) {
        Map<String, String> props = new LinkedHashMap<>();
        for (int i = 0; i + 1 < values.length; i += 2) {
            props.put(values[i], values[i + 1]);
        }
        return props;
    }
}
