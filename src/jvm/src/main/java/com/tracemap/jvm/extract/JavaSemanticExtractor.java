package com.tracemap.jvm.extract;

import com.sun.source.tree.ClassTree;
import com.sun.source.tree.CompilationUnitTree;
import com.sun.source.tree.ExpressionTree;
import com.sun.source.tree.MemberSelectTree;
import com.sun.source.tree.MethodInvocationTree;
import com.sun.source.tree.MethodTree;
import com.sun.source.tree.NewClassTree;
import com.sun.source.tree.Tree;
import com.sun.source.tree.VariableTree;
import com.sun.source.util.JavacTask;
import com.sun.source.util.TreePath;
import com.sun.source.util.TreePathScanner;
import com.sun.source.util.Trees;
import com.tracemap.jvm.facts.FactFactory;
import com.tracemap.jvm.model.CodeFact;
import com.tracemap.jvm.model.EvidenceSpan;
import com.tracemap.jvm.model.EvidenceTiers;
import com.tracemap.jvm.model.FactTypes;
import com.tracemap.jvm.model.FileInventoryItem;
import com.tracemap.jvm.model.RuleIds;
import com.tracemap.jvm.model.ScanManifest;
import com.tracemap.jvm.model.ScannerVersions;
import com.tracemap.jvm.scan.AnalysisGapCollector;
import com.tracemap.jvm.util.Hashes;
import com.tracemap.jvm.util.PathsUtil;
import java.io.IOException;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import javax.lang.model.element.Element;
import javax.lang.model.element.ElementKind;
import javax.lang.model.element.ExecutableElement;
import javax.lang.model.element.TypeElement;
import javax.lang.model.element.VariableElement;
import javax.lang.model.type.TypeMirror;
import javax.tools.Diagnostic;
import javax.tools.DiagnosticCollector;
import javax.tools.JavaCompiler;
import javax.tools.JavaFileObject;
import javax.tools.StandardJavaFileManager;
import javax.tools.ToolProvider;

public final class JavaSemanticExtractor {
    private JavaSemanticExtractor() {
    }

    public static List<CodeFact> extract(ScanManifest manifest, Path repoPath, List<FileInventoryItem> files, AnalysisGapCollector gaps) {
        List<FileInventoryItem> javaFiles = files.stream()
            .filter(file -> "Java".equals(file.kind()) && !file.skipped())
            .toList();
        if (javaFiles.isEmpty()) {
            return List.of();
        }
        JavaCompiler compiler = ToolProvider.getSystemJavaCompiler();
        if (compiler == null) {
            gaps.add("JavaSemanticUnavailable: JDK compiler not available");
            return List.of();
        }
        DiagnosticCollector<JavaFileObject> diagnostics = new DiagnosticCollector<>();
        try (StandardJavaFileManager fileManager = compiler.getStandardFileManager(diagnostics, Locale.ROOT, null)) {
            Iterable<? extends JavaFileObject> fileObjects = fileManager.getJavaFileObjectsFromPaths(javaFiles.stream().map(FileInventoryItem::absolutePath).toList());
            List<String> options = List.of("-proc:none", "-Xlint:none");
            JavacTask task = (JavacTask) compiler.getTask(null, fileManager, diagnostics, options, null, fileObjects);
            Iterable<? extends CompilationUnitTree> parsed = task.parse();
            task.analyze();
            recordDiagnostics(diagnostics, gaps);
            Trees trees = Trees.instance(task);
            List<CodeFact> facts = new ArrayList<>();
            for (CompilationUnitTree unit : parsed) {
                new Scanner(manifest, repoPath, trees, unit, facts).scan(unit, null);
            }
            return facts;
        } catch (Exception exception) {
            gaps.add("JavaSemanticFailed: " + exception.getClass().getSimpleName());
            recordDiagnostics(diagnostics, gaps);
            return List.of();
        }
    }

    private static void recordDiagnostics(DiagnosticCollector<JavaFileObject> diagnostics, AnalysisGapCollector gaps) {
        int count = 0;
        for (Diagnostic<? extends JavaFileObject> diagnostic : diagnostics.getDiagnostics()) {
            if (count++ >= 25) {
                gaps.add("JavaCompilerDiagnosticsTruncated: " + diagnostics.getDiagnostics().size());
                break;
            }
            if (diagnostic.getKind() == Diagnostic.Kind.ERROR || diagnostic.getKind() == Diagnostic.Kind.WARNING) {
                String source = diagnostic.getSource() == null ? "<unknown>" : Path.of(diagnostic.getSource().toUri()).getFileName().toString();
                gaps.add("JavaCompilerDiagnostic:" + diagnostic.getKind() + ":" + source + ":" + diagnostic.getCode());
            }
        }
    }

    private static final class Scanner extends TreePathScanner<Void, Void> {
        private final ScanManifest manifest;
        private final Path repoPath;
        private final Trees trees;
        private final CompilationUnitTree unit;
        private final List<CodeFact> facts;
        private String currentType;
        private String currentMethod;

        private Scanner(ScanManifest manifest, Path repoPath, Trees trees, CompilationUnitTree unit, List<CodeFact> facts) {
            this.manifest = manifest;
            this.repoPath = repoPath;
            this.trees = trees;
            this.unit = unit;
            this.facts = facts;
        }

        @Override
        public Void visitClass(ClassTree node, Void unused) {
            Element element = trees.getElement(getCurrentPath());
            String previousType = currentType;
            if (element instanceof TypeElement typeElement) {
                currentType = typeElement.getQualifiedName().toString();
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.TYPE_DECLARED,
                    RuleIds.JAVA_SEMANTIC_DECLARATIONS,
                    EvidenceTiers.TIER1_SEMANTIC,
                    evidence(node),
                    relativePath(),
                    null,
                    currentType,
                    currentType,
                    props("language", "java", "declarationKind", node.getKind().name(), "name", typeElement.getSimpleName().toString(), "typeName", typeElement.getSimpleName().toString(), "namespace", packageName(currentType), "targetSymbol", currentType, "targetSymbolId", symbolId(typeElement), "targetSymbolKind", "Type", "targetLanguage", "java")));
                TypeMirror superclass = typeElement.getSuperclass();
                if (superclass != null && !"none".equals(superclass.getKind().name().toLowerCase(Locale.ROOT)) && !"java.lang.Object".equals(superclass.toString())) {
                    emitRelationship(typeElement, superclass.toString(), "ExtendsClass", node);
                }
                for (TypeMirror iface : typeElement.getInterfaces()) {
                    emitRelationship(typeElement, iface.toString(), "ImplementsInterface", node);
                }
            }
            super.visitClass(node, unused);
            currentType = previousType;
            return null;
        }

        @Override
        public Void visitMethod(MethodTree node, Void unused) {
            Element element = trees.getElement(getCurrentPath());
            String previousMethod = currentMethod;
            if (element instanceof ExecutableElement executable) {
                currentMethod = displayName(executable);
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.METHOD_DECLARED,
                    RuleIds.JAVA_SEMANTIC_DECLARATIONS,
                    EvidenceTiers.TIER1_SEMANTIC,
                    evidence(node),
                    relativePath(),
                    currentType,
                    currentMethod,
                    currentMethod,
                    props("language", "java", "methodName", executable.getSimpleName().toString(), "name", executable.getSimpleName().toString(), "containingType", currentType == null ? "" : currentType, "targetSymbol", currentMethod, "targetSymbolId", symbolId(executable), "targetSymbolKind", "Method", "targetLanguage", "java", "jvmDescriptor", descriptor(executable))));
            }
            super.visitMethod(node, unused);
            currentMethod = previousMethod;
            return null;
        }

        @Override
        public Void visitVariable(VariableTree node, Void unused) {
            Element element = trees.getElement(getCurrentPath());
            if (element instanceof VariableElement variable) {
                if (variable.getKind().isField()) {
                    String symbol = displayName(variable);
                    facts.add(FactFactory.create(
                        manifest,
                        FactTypes.FIELD_DECLARED,
                        RuleIds.JAVA_SEMANTIC_DECLARATIONS,
                        EvidenceTiers.TIER1_SEMANTIC,
                        evidence(node),
                        relativePath(),
                        currentType,
                        symbol,
                        symbol,
                        props("language", "java", "fieldName", variable.getSimpleName().toString(), "fieldType", variable.asType().toString(), "containingType", currentType == null ? "" : currentType, "targetSymbol", symbol, "targetSymbolId", symbolId(variable), "targetSymbolKind", "Field", "targetLanguage", "java")));
                } else if (variable.getKind() == ElementKind.PARAMETER) {
                    String symbol = displayName(variable);
                    facts.add(FactFactory.create(
                        manifest,
                        FactTypes.PARAMETER_DECLARED,
                        RuleIds.JAVA_SEMANTIC_DECLARATIONS,
                        EvidenceTiers.TIER1_SEMANTIC,
                        evidence(node),
                        relativePath(),
                        currentMethod,
                        symbol,
                        symbol,
                        props("language", "java", "parameterName", variable.getSimpleName().toString(), "parameterType", variable.asType().toString(), "sourceSymbol", currentMethod == null ? "" : currentMethod, "targetSymbolId", symbolId(variable), "targetSymbolKind", "Parameter", "targetLanguage", "java")));
                } else if (variable.getKind() == ElementKind.LOCAL_VARIABLE && node.getInitializer() != null) {
                    Element origin = trees.getElement(new TreePath(getCurrentPath(), node.getInitializer()));
                    if (origin != null) {
                        String aliasSymbol = displayName(variable);
                        String originSymbol = displayName(origin);
                        facts.add(FactFactory.create(
                            manifest,
                            FactTypes.LOCAL_ALIAS,
                            RuleIds.JAVA_SEMANTIC_VALUE_FLOW,
                            EvidenceTiers.TIER1_SEMANTIC,
                            evidence(node),
                            relativePath(),
                            currentMethod,
                            aliasSymbol,
                            aliasSymbol,
                            props("language", "java", "containingSymbol", currentMethod == null ? "" : currentMethod, "aliasSymbol", aliasSymbol, "aliasSymbolKind", "Local", "aliasType", variable.asType().toString(), "originSymbol", originSymbol, "originSymbolKind", origin.getKind().name(), "originType", origin.asType().toString(), "targetSymbolId", symbolId(variable), "targetSymbolKind", "Local", "targetLanguage", "java")));
                    }
                }
            }
            return super.visitVariable(node, unused);
        }

        @Override
        public Void visitMemberSelect(MemberSelectTree node, Void unused) {
            Element element = trees.getElement(getCurrentPath());
            if (element instanceof VariableElement variable && variable.getKind().isField()) {
                String symbol = displayName(variable);
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.PROPERTY_ACCESSED,
                    RuleIds.JAVA_SEMANTIC_MEMBER_ACCESS,
                    EvidenceTiers.TIER1_SEMANTIC,
                    evidence(node),
                    relativePath(),
                    currentMethod,
                    symbol,
                    symbol,
                    props("language", "java", "propertyName", variable.getSimpleName().toString(), "memberName", variable.getSimpleName().toString(), "containingType", ownerName(variable), "targetSymbol", symbol, "targetSymbolId", symbolId(variable), "targetSymbolKind", "Field", "targetLanguage", "java")));
            }
            return super.visitMemberSelect(node, unused);
        }

        @Override
        public Void visitMethodInvocation(MethodInvocationTree node, Void unused) {
            Element element = trees.getElement(getCurrentPath());
            if (element instanceof ExecutableElement executable) {
                String callee = displayName(executable);
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.METHOD_INVOKED,
                    RuleIds.JAVA_SEMANTIC_INVOCATION,
                    EvidenceTiers.TIER1_SEMANTIC,
                    evidence(node),
                    relativePath(),
                    currentMethod,
                    callee,
                    callee,
                    props("language", "java", "methodName", executable.getSimpleName().toString(), "name", executable.getSimpleName().toString(), "containingType", ownerName(executable), "targetSymbol", callee, "targetSymbolId", symbolId(executable), "targetSymbolKind", "Method", "targetLanguage", "java", "jvmDescriptor", descriptor(executable))));
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.CALL_EDGE,
                    RuleIds.JAVA_SEMANTIC_CALLGRAPH,
                    EvidenceTiers.TIER1_SEMANTIC,
                    evidence(node),
                    relativePath(),
                    currentMethod,
                    callee,
                    callee,
                    props("language", "java", "callerSymbol", currentMethod == null ? "" : currentMethod, "calleeSymbol", callee, "methodName", executable.getSimpleName().toString(), "targetSymbol", callee, "targetSymbolId", symbolId(executable), "targetSymbolKind", "Method", "targetLanguage", "java", "containingType", ownerName(executable), "callKind", "Method")));
                emitArguments(node, executable, callee);
            }
            return super.visitMethodInvocation(node, unused);
        }

        @Override
        public Void visitNewClass(NewClassTree node, Void unused) {
            Element element = trees.getElement(getCurrentPath());
            String created = node.getIdentifier().toString();
            String constructor = created + ".<init>";
            if (element instanceof ExecutableElement executable) {
                created = ownerName(executable);
                constructor = displayName(executable);
            }
            facts.add(FactFactory.create(
                manifest,
                FactTypes.OBJECT_CREATED,
                RuleIds.JAVA_SEMANTIC_OBJECT_CREATION,
                EvidenceTiers.TIER1_SEMANTIC,
                evidence(node),
                relativePath(),
                currentMethod,
                created,
                created,
                props("language", "java", "createdType", created, "constructorSymbol", constructor, "targetSymbol", created, "targetSymbolId", "jvm:java:source:" + created, "targetSymbolKind", "Type", "targetLanguage", "java", "argumentCount", String.valueOf(node.getArguments().size()))));
            return super.visitNewClass(node, unused);
        }

        private void emitArguments(MethodInvocationTree node, ExecutableElement executable, String callee) {
            List<? extends ExpressionTree> args = node.getArguments();
            for (int i = 0; i < args.size(); i++) {
                String parameterName = i < executable.getParameters().size() ? executable.getParameters().get(i).getSimpleName().toString() : "arg" + i;
                String parameterType = i < executable.getParameters().size() ? executable.getParameters().get(i).asType().toString() : "";
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.ARGUMENT_PASSED,
                    RuleIds.JAVA_SEMANTIC_VALUE_FLOW,
                    EvidenceTiers.TIER1_SEMANTIC,
                    evidence(args.get(i)),
                    relativePath(),
                    currentMethod,
                    callee,
                    callee,
                    props("language", "java", "parameterOrdinal", String.valueOf(i), "parameterName", parameterName, "parameterType", parameterType, "argumentOrdinal", String.valueOf(i), "argumentExpressionKind", args.get(i).getKind().name(), "argumentExpressionHash", Hashes.sha256(args.get(i).toString(), 32), "callKind", "Method")));
            }
        }

        private void emitRelationship(TypeElement source, String targetDisplay, String kind, Tree node) {
            String sourceSymbol = source.getQualifiedName().toString();
            facts.add(FactFactory.create(
                manifest,
                FactTypes.SYMBOL_RELATIONSHIP,
                RuleIds.JAVA_SEMANTIC_RELATIONSHIP,
                EvidenceTiers.TIER1_SEMANTIC,
                evidence(node),
                relativePath(),
                sourceSymbol,
                targetDisplay,
                targetDisplay,
                props("relationshipKind", kind, "sourceSymbol", sourceSymbol, "targetSymbol", targetDisplay, "sourceSymbolId", symbolId(source), "targetSymbolId", "jvm:java:source:" + targetDisplay, "sourceSymbolKind", "Type", "targetSymbolKind", "Type", "sourceLanguage", "java", "targetLanguage", "java")));
        }

        private EvidenceSpan evidence(Tree tree) {
            long start = trees.getSourcePositions().getStartPosition(unit, tree);
            long end = trees.getSourcePositions().getEndPosition(unit, tree);
            int startLine = start < 0 ? 1 : (int) unit.getLineMap().getLineNumber(start);
            int endLine = end < 0 ? startLine : (int) unit.getLineMap().getLineNumber(end);
            return FactFactory.evidence(relativePath(), startLine, endLine, "JavaSemanticExtractor", ScannerVersions.JAVA_SEMANTIC);
        }

        private String relativePath() {
            try {
                return PathsUtil.relativeUnix(repoPath, Path.of(unit.getSourceFile().toUri()));
            } catch (Exception exception) {
                return Path.of(unit.getSourceFile().toUri()).getFileName().toString();
            }
        }

        private static String displayName(Element element) {
            if (element instanceof TypeElement type) {
                return type.getQualifiedName().toString();
            }
            Element owner = element.getEnclosingElement();
            String ownerName = owner instanceof TypeElement type ? type.getQualifiedName().toString() : owner == null ? "" : owner.toString();
            return ownerName.isBlank() ? element.getSimpleName().toString() : ownerName + "." + element.getSimpleName();
        }

        private static String ownerName(Element element) {
            Element owner = element.getEnclosingElement();
            return owner instanceof TypeElement type ? type.getQualifiedName().toString() : owner == null ? "" : owner.toString();
        }

        private static String symbolId(Element element) {
            return "jvm:java:source:" + displayName(element);
        }

        private static String descriptor(ExecutableElement executable) {
            StringBuilder builder = new StringBuilder("(");
            for (VariableElement parameter : executable.getParameters()) {
                builder.append(parameter.asType()).append(';');
            }
            builder.append(")").append(executable.getReturnType());
            return builder.toString();
        }

        private static String packageName(String qualifiedType) {
            int dot = qualifiedType.lastIndexOf('.');
            return dot > 0 ? qualifiedType.substring(0, dot) : "";
        }
    }

    private static Map<String, String> props(String... values) {
        Map<String, String> props = new LinkedHashMap<>();
        for (int i = 0; i + 1 < values.length; i += 2) {
            props.put(values[i], values[i + 1]);
        }
        return props;
    }
}
