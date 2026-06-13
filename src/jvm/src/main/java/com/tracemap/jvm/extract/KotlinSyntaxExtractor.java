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

public final class KotlinSyntaxExtractor {
    private static final Pattern PACKAGE = Pattern.compile("^\\s*package\\s+([A-Za-z_][\\w.]*)");
    private static final Pattern TYPE = Pattern.compile("\\b(?:data\\s+|sealed\\s+|open\\s+|abstract\\s+)?(class|interface|object)\\s+([A-Za-z_][\\w]*)");
    private static final Pattern FUN = Pattern.compile("\\bfun\\s+(?:[A-Za-z_][\\w.<>]*\\.)?([A-Za-z_][\\w]*)\\s*\\(([^)]*)\\)");
    private static final Pattern MEMBER = Pattern.compile("\\.([A-Za-z_][\\w]*)\\b");
    private static final Pattern INVOCATION = Pattern.compile("\\b([A-Za-z_][\\w.]*)\\s*\\(([^)]*)\\)");
    private static final Pattern OBJECT_CREATION = Pattern.compile("\\b([A-Z][A-Za-z0-9_]*)\\s*\\(([^)]*)\\)");
    private static final Pattern ANNOTATION = Pattern.compile("@([A-Za-z_][\\w.]*)\\s*(?:\\((.*)\\))?");
    private static final Pattern STRING = Pattern.compile("\"((?:\\\\.|[^\"\\\\])*)\"");

    private KotlinSyntaxExtractor() {
    }

    public static List<CodeFact> extract(ScanManifest manifest, List<FileInventoryItem> files) {
        List<CodeFact> facts = new ArrayList<>();
        for (FileInventoryItem file : files) {
            if (!("Kotlin".equals(file.kind()) || "KotlinScript".equals(file.kind())) || file.skipped()) {
                continue;
            }
            try {
                extractFile(manifest, file, facts);
            } catch (IOException ignored) {
            }
        }
        return facts;
    }

    private static void extractFile(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts) throws IOException {
        List<String> lines = Files.readAllLines(file.absolutePath());
        String pkg = "";
        String currentType = null;
        String currentSymbol = null;
        for (int i = 0; i < lines.size(); i++) {
            String line = lines.get(i);
            int lineNo = i + 1;
            Matcher pkgMatcher = PACKAGE.matcher(line);
            if (pkgMatcher.find()) {
                pkg = pkgMatcher.group(1);
            }
            Matcher annotationMatcher = ANNOTATION.matcher(line);
            while (annotationMatcher.find()) {
                String name = simple(annotationMatcher.group(1));
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.ATTRIBUTE_USED,
                    RuleIds.KOTLIN_SYNTAX_DECLARATIONS,
                    EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                    FactFactory.evidence(file.relativePath(), lineNo, lineNo, "KotlinSyntaxExtractor", ScannerVersions.KOTLIN_SYNTAX),
                    file.relativePath(),
                    currentSymbol,
                    name,
                    null,
                    props("attributeName", name, "name", name, "argumentHash", Hashes.sha256(annotationMatcher.group(2) == null ? "" : annotationMatcher.group(2), 32))));
            }
            Matcher typeMatcher = TYPE.matcher(line);
            if (typeMatcher.find()) {
                currentType = typeMatcher.group(2);
                currentSymbol = qualify(pkg, currentType);
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.TYPE_DECLARED,
                    RuleIds.KOTLIN_SYNTAX_DECLARATIONS,
                    EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                    FactFactory.evidence(file.relativePath(), lineNo, lineNo, "KotlinSyntaxExtractor", ScannerVersions.KOTLIN_SYNTAX),
                    file.relativePath(),
                    null,
                    currentSymbol,
                    currentSymbol,
                    props("language", "kotlin", "declarationKind", typeMatcher.group(1), "name", currentType, "typeName", currentType, "namespace", pkg, "targetSymbol", currentSymbol)));
            }
            Matcher funMatcher = FUN.matcher(line);
            if (funMatcher.find()) {
                String function = funMatcher.group(1);
                String symbol = (currentSymbol == null ? qualify(pkg, file.relativePath().replace('/', '.')) : currentSymbol) + "." + function;
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.METHOD_DECLARED,
                    RuleIds.KOTLIN_SYNTAX_DECLARATIONS,
                    EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                    FactFactory.evidence(file.relativePath(), lineNo, lineNo, "KotlinSyntaxExtractor", ScannerVersions.KOTLIN_SYNTAX),
                    file.relativePath(),
                    currentSymbol,
                    symbol,
                    symbol,
                    props("language", "kotlin", "methodName", function, "name", function, "containingType", currentSymbol == null ? "" : currentSymbol, "targetSymbol", symbol, "parameterCount", countArgs(funMatcher.group(2)))));
            }
            emitCalls(manifest, file, facts, currentSymbol, line, lineNo);
            emitKtor(manifest, file, facts, currentSymbol, line, lineNo);
        }
    }

    private static void emitCalls(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, String currentSymbol, String line, int lineNo) {
        Matcher memberMatcher = MEMBER.matcher(line);
        while (memberMatcher.find()) {
            String member = memberMatcher.group(1);
            facts.add(FactFactory.create(
                manifest,
                FactTypes.MEMBER_ACCESS_NAME,
                RuleIds.KOTLIN_SYNTAX_INVOCATION,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "KotlinSyntaxExtractor", ScannerVersions.KOTLIN_SYNTAX),
                file.relativePath(),
                currentSymbol,
                member,
                member,
                props("memberName", member, "name", member, "expressionHash", Hashes.sha256(line.trim(), 32))));
        }
        Matcher invocationMatcher = INVOCATION.matcher(line);
        while (invocationMatcher.find()) {
            String callee = invocationMatcher.group(1);
            String name = simple(callee);
            if (List.of("if", "for", "while", "when").contains(name)) continue;
            facts.add(FactFactory.create(
                manifest,
                FactTypes.INVOCATION_NAME,
                RuleIds.KOTLIN_SYNTAX_INVOCATION,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "KotlinSyntaxExtractor", ScannerVersions.KOTLIN_SYNTAX),
                file.relativePath(),
                currentSymbol,
                callee,
                callee,
                props("methodName", name, "name", name, "targetSymbol", callee, "argumentCount", countArgs(invocationMatcher.group(2)), "expressionHash", Hashes.sha256(invocationMatcher.group(0), 32))));
            facts.add(FactFactory.create(
                manifest,
                FactTypes.CALL_EDGE,
                RuleIds.KOTLIN_SYNTAX_INVOCATION,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "KotlinSyntaxExtractor", ScannerVersions.KOTLIN_SYNTAX),
                file.relativePath(),
                currentSymbol,
                callee,
                callee,
                props("callerSymbol", currentSymbol == null ? "" : currentSymbol, "calleeSymbol", callee, "methodName", name, "targetSymbol", callee, "callKind", "Method")));
        }
        Matcher creationMatcher = OBJECT_CREATION.matcher(line);
        while (creationMatcher.find()) {
            String type = creationMatcher.group(1);
            facts.add(FactFactory.create(
                manifest,
                FactTypes.OBJECT_CREATED,
                RuleIds.KOTLIN_SYNTAX_OBJECT_CREATION,
                EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
                FactFactory.evidence(file.relativePath(), lineNo, lineNo, "KotlinSyntaxExtractor", ScannerVersions.KOTLIN_SYNTAX),
                file.relativePath(),
                currentSymbol,
                type,
                type,
                props("createdType", type, "constructorSymbol", type + ".<init>", "argumentCount", countArgs(creationMatcher.group(2)), "targetSymbol", type)));
        }
    }

    private static void emitKtor(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, String currentSymbol, String line, int lineNo) {
        Matcher routeMatcher = Pattern.compile("\\b(get|post|put|patch|delete|head|options|route)\\s*\\(\\s*\"((?:\\\\.|[^\"\\\\])*)\"").matcher(line);
        if (!routeMatcher.find()) {
            return;
        }
        String method = routeMatcher.group(1).equals("route") ? "ANY" : routeMatcher.group(1).toUpperCase();
        String path = normalizeRoute(routeMatcher.group(2));
        facts.add(FactFactory.create(
            manifest,
            FactTypes.HTTP_ROUTE_BINDING,
            RuleIds.HTTP_ROUTE,
            EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL,
            FactFactory.evidence(file.relativePath(), lineNo, lineNo, "KotlinSyntaxExtractor", ScannerVersions.KOTLIN_SYNTAX),
            file.relativePath(),
            currentSymbol,
            currentSymbol,
            currentSymbol,
            props("httpMethod", method, "normalizedPathTemplate", path, "normalizedPathKey", method + " " + path.toLowerCase(), "methodName", routeMatcher.group(1), "targetSymbol", currentSymbol == null ? "" : currentSymbol, "framework", "Ktor", "routePatternHash", Hashes.sha256(path, 32))));
    }

    private static String normalizeRoute(String value) {
        String normalized = value == null ? "" : value.trim().replaceAll("/+", "/");
        if (!normalized.startsWith("/")) normalized = "/" + normalized;
        if (normalized.length() > 1 && normalized.endsWith("/")) normalized = normalized.substring(0, normalized.length() - 1);
        return normalized;
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

    private static Map<String, String> props(String... values) {
        Map<String, String> props = new LinkedHashMap<>();
        for (int i = 0; i + 1 < values.length; i += 2) {
            props.put(values[i], values[i + 1]);
        }
        return props;
    }
}
