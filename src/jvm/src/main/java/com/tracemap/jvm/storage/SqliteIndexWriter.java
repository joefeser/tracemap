package com.tracemap.jvm.storage;

import com.tracemap.jvm.model.CodeFact;
import com.tracemap.jvm.model.FactTypes;
import com.tracemap.jvm.model.ScanManifest;
import com.tracemap.jvm.util.Hashes;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.PreparedStatement;
import java.sql.SQLException;
import java.sql.Statement;
import java.util.List;
import java.util.Map;

public final class SqliteIndexWriter {
    private SqliteIndexWriter() {
    }

    public static void write(Path path, ScanManifest manifest, List<CodeFact> facts) throws IOException, SQLException {
        Files.createDirectories(path.toAbsolutePath().normalize().getParent());
        Files.deleteIfExists(path);
        boolean committed = false;
        try (Connection connection = DriverManager.getConnection("jdbc:sqlite:" + path.toAbsolutePath().normalize())) {
            connection.setAutoCommit(false);
            try {
                createSchema(connection);
                insertManifest(connection, manifest);
                for (CodeFact fact : facts) {
                    insertFact(connection, fact);
                    insertSymbolRows(connection, fact);
                    insertDerivedRows(connection, fact);
                }
                connection.commit();
                committed = true;
            } catch (SQLException exception) {
                rollbackQuietly(connection);
                throw exception;
            } catch (IOException exception) {
                rollbackQuietly(connection);
                throw exception;
            } finally {
                connection.setAutoCommit(true);
            }
        } catch (IOException | SQLException exception) {
            if (!committed) {
                Files.deleteIfExists(path);
            }
            throw exception;
        }
    }

    private static void rollbackQuietly(Connection connection) {
        try {
            connection.rollback();
        } catch (SQLException ignored) {
            // Preserve the original write failure.
        }
    }

    private static void createSchema(Connection connection) throws SQLException {
        try (Statement statement = connection.createStatement()) {
            statement.executeUpdate("""
                create table scan_manifest (
                  scan_id text primary key,
                  repo text not null,
                  commit_sha text not null,
                  scanner_version text not null,
                  scanned_at text not null,
                  analysis_level text not null,
                  build_status text not null,
                  manifest_json text not null
                );
                """);
            statement.executeUpdate("""
                create table facts (
                  fact_id text primary key,
                  scan_id text not null,
                  repo text not null,
                  commit_sha text not null,
                  project_path text,
                  fact_type text not null,
                  rule_id text not null,
                  evidence_tier text not null,
                  source_symbol text,
                  target_symbol text,
                  contract_element text,
                  file_path text not null,
                  start_line integer not null,
                  end_line integer not null,
                  snippet_hash text,
                  extractor_id text not null,
                  extractor_version text not null,
                  properties_json text not null
                );
                """);
            statement.executeUpdate("""
                create table symbols (
                  scan_id text not null,
                  symbol_id text not null,
                  language text not null,
                  symbol_kind text not null,
                  display_name text not null,
                  assembly_name text,
                  assembly_version text,
                  containing_symbol_id text,
                  primary key (scan_id, symbol_id)
                );
                """);
            statement.executeUpdate("""
                create table symbol_occurrences (
                  occurrence_id text primary key,
                  scan_id text not null,
                  symbol_id text not null,
                  fact_id text not null,
                  role text not null,
                  occurrence_kind text not null,
                  evidence_tier text not null,
                  rule_id text not null,
                  file_path text not null,
                  start_line integer not null,
                  end_line integer not null
                );
                """);
            statement.executeUpdate("""
                create table fact_symbols (
                  fact_id text not null,
                  scan_id text not null,
                  symbol_id text not null,
                  role text not null,
                  primary key (fact_id, symbol_id, role)
                );
                """);
            statement.executeUpdate("""
                create table symbol_relationships (
                  relationship_id text primary key,
                  scan_id text not null,
                  source_symbol_id text not null,
                  target_symbol_id text not null,
                  relationship_kind text not null,
                  rule_id text not null,
                  evidence_tier text not null,
                  file_path text not null,
                  start_line integer not null,
                  end_line integer not null
                );
                """);
            statement.executeUpdate("""
                create table call_edges (
                  fact_id text primary key,
                  scan_id text not null,
                  repo text not null,
                  commit_sha text not null,
                  evidence_tier text not null,
                  rule_id text not null,
                  caller_symbol text,
                  caller_assembly_name text,
                  caller_assembly_version text,
                  callee_symbol text not null,
                  callee_assembly_name text,
                  callee_assembly_version text,
                  callee_containing_type text,
                  call_kind text,
                  file_path text not null,
                  start_line integer not null,
                  end_line integer not null
                );
                """);
            statement.executeUpdate("""
                create table object_creations (
                  fact_id text primary key,
                  scan_id text not null,
                  repo text not null,
                  commit_sha text not null,
                  evidence_tier text not null,
                  rule_id text not null,
                  caller_symbol text,
                  caller_assembly_name text,
                  caller_assembly_version text,
                  created_type text not null,
                  created_type_assembly_name text,
                  created_type_assembly_version text,
                  constructor_symbol text,
                  assigned_to text,
                  file_path text not null,
                  start_line integer not null,
                  end_line integer not null
                );
                """);
            statement.executeUpdate("""
                create table argument_flows (
                  fact_id text primary key,
                  scan_id text not null,
                  repo text not null,
                  commit_sha text not null,
                  evidence_tier text not null,
                  rule_id text not null,
                  caller_symbol text,
                  caller_assembly_name text,
                  caller_assembly_version text,
                  callee_symbol text not null,
                  callee_assembly_name text,
                  callee_assembly_version text,
                  call_kind text,
                  parameter_ordinal integer not null,
                  parameter_name text not null,
                  parameter_type text,
                  argument_ordinal integer not null,
                  argument_expression_kind text,
                  argument_expression_hash text,
                  argument_symbol text,
                  argument_symbol_kind text,
                  argument_type text,
                  argument_assembly_name text,
                  argument_assembly_version text,
                  argument_source_file text,
                  argument_source_start_line integer,
                  argument_source_end_line integer,
                  file_path text not null,
                  start_line integer not null,
                  end_line integer not null
                );
                """);
            statement.executeUpdate("""
                create table local_aliases (
                  fact_id text primary key,
                  scan_id text not null,
                  repo text not null,
                  commit_sha text not null,
                  evidence_tier text not null,
                  rule_id text not null,
                  containing_symbol text,
                  alias_symbol text not null,
                  alias_symbol_kind text,
                  alias_type text,
                  origin_symbol text not null,
                  origin_symbol_kind text,
                  origin_type text,
                  file_path text not null,
                  start_line integer not null,
                  end_line integer not null
                );
                """);
            statement.executeUpdate("""
                create table field_aliases (
                  fact_id text primary key,
                  scan_id text not null,
                  repo text not null,
                  commit_sha text not null,
                  evidence_tier text not null,
                  rule_id text not null,
                  containing_symbol text,
                  field_symbol text not null,
                  field_symbol_kind text,
                  field_type text,
                  origin_symbol text not null,
                  origin_symbol_kind text,
                  origin_type text,
                  file_path text not null,
                  start_line integer not null,
                  end_line integer not null
                );
                """);
            statement.executeUpdate("""
                create table parameter_forward_edges (
                  fact_id text primary key,
                  scan_id text not null,
                  repo text not null,
                  commit_sha text not null,
                  evidence_tier text not null,
                  rule_id text not null,
                  source_method_symbol text not null,
                  source_parameter_symbol text not null,
                  source_node_key text not null,
                  target_method_symbol text not null,
                  target_parameter_name text not null,
                  target_parameter_type text,
                  target_parameter_symbol text not null,
                  target_node_key text not null,
                  target_assembly_name text,
                  target_assembly_version text,
                  file_path text not null,
                  start_line integer not null,
                  end_line integer not null
                );
                """);
            statement.executeUpdate("create index ix_facts_type on facts(fact_type);");
            statement.executeUpdate("create index ix_facts_rule on facts(rule_id);");
            statement.executeUpdate("create index ix_facts_target_symbol on facts(target_symbol);");
            statement.executeUpdate("create index ix_facts_contract_element on facts(contract_element);");
            statement.executeUpdate("create index ix_facts_file on facts(file_path);");
            statement.executeUpdate("create index ix_symbols_display on symbols(display_name);");
            statement.executeUpdate("create index ix_symbols_kind on symbols(symbol_kind);");
            statement.executeUpdate("create index ix_symbols_assembly on symbols(assembly_name, display_name);");
            statement.executeUpdate("create index ix_fact_symbols_symbol on fact_symbols(scan_id, symbol_id);");
            statement.executeUpdate("create index ix_symbol_relationships_source on symbol_relationships(scan_id, source_symbol_id);");
            statement.executeUpdate("create index ix_symbol_relationships_target on symbol_relationships(scan_id, target_symbol_id);");
            statement.executeUpdate("create index ix_call_edges_callee on call_edges(callee_symbol);");
            statement.executeUpdate("create index ix_object_creations_type on object_creations(created_type);");
            statement.executeUpdate("create index ix_argument_flows_callee on argument_flows(callee_symbol);");
            statement.executeUpdate("create index ix_local_aliases_origin on local_aliases(origin_symbol);");
        }
    }

    private static void insertManifest(Connection connection, ScanManifest manifest) throws SQLException, IOException {
        try (PreparedStatement command = connection.prepareStatement("""
            insert into scan_manifest (scan_id, repo, commit_sha, scanner_version, scanned_at, analysis_level, build_status, manifest_json)
            values (?, ?, ?, ?, ?, ?, ?, ?);
            """)) {
            command.setString(1, manifest.scanId());
            command.setString(2, manifest.repoName());
            command.setString(3, manifest.commitSha());
            command.setString(4, manifest.scannerVersion());
            command.setString(5, manifest.scannedAt());
            command.setString(6, manifest.analysisLevel());
            command.setString(7, manifest.buildStatus());
            command.setString(8, JsonSupport.JSON.writeValueAsString(manifest));
            command.executeUpdate();
        }
    }

    private static void insertFact(Connection connection, CodeFact fact) throws SQLException, IOException {
        try (PreparedStatement command = connection.prepareStatement("""
            insert into facts (
              fact_id, scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier,
              source_symbol, target_symbol, contract_element, file_path, start_line, end_line, snippet_hash,
              extractor_id, extractor_version, properties_json
            ) values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
            """)) {
            command.setString(1, fact.factId());
            command.setString(2, fact.scanId());
            command.setString(3, fact.repo());
            command.setString(4, fact.commitSha());
            command.setString(5, fact.projectPath());
            command.setString(6, fact.factType());
            command.setString(7, fact.ruleId());
            command.setString(8, fact.evidenceTier());
            command.setString(9, fact.sourceSymbol());
            command.setString(10, fact.targetSymbol());
            command.setString(11, fact.contractElement());
            command.setString(12, fact.evidence().filePath());
            command.setInt(13, fact.evidence().startLine());
            command.setInt(14, fact.evidence().endLine());
            command.setString(15, fact.evidence().snippetHash());
            command.setString(16, fact.evidence().extractorId());
            command.setString(17, fact.evidence().extractorVersion());
            command.setString(18, JsonSupport.JSON.writeValueAsString(fact.properties()));
            command.executeUpdate();
        }
    }

    private static void insertSymbolRows(Connection connection, CodeFact fact) throws SQLException {
        insertSymbolRole(connection, fact, "source", fact.properties().get("sourceSymbolId"), fact.sourceSymbol(), fact.properties().get("sourceSymbolKind"));
        insertSymbolRole(connection, fact, "target", fact.properties().get("targetSymbolId"), fact.targetSymbol(), fact.properties().get("targetSymbolKind"));
        insertSymbolRole(connection, fact, "argument", fact.properties().get("argumentSymbolId"), fact.properties().get("argumentSymbolDisplayName"), fact.properties().get("argumentSymbolKind"));
        insertSymbolRole(connection, fact, "parameter", fact.properties().get("parameterSymbolId"), fact.properties().get("parameterSymbolDisplayName"), fact.properties().get("parameterSymbolKind"));
        insertSymbolRole(connection, fact, "origin", fact.properties().get("originSymbolId"), fact.properties().get("originSymbolDisplayName"), fact.properties().get("originSymbolKind"));
        insertSymbolRole(connection, fact, "constructor", fact.properties().get("constructorSymbolId"), fact.properties().get("constructorSymbolDisplayName"), fact.properties().get("constructorSymbolKind"));
    }

    private static void insertSymbolRole(Connection connection, CodeFact fact, String role, String symbolId, String displayName, String symbolKind) throws SQLException {
        if (symbolId == null || symbolId.isBlank()) {
            return;
        }
        String safeDisplay = displayName == null || displayName.isBlank() ? symbolId : displayName;
        String language = fact.properties().getOrDefault(role + "Language", fact.properties().getOrDefault("language", "jvm"));
        String assemblyName = fact.properties().get(role + "AssemblyName");
        String assemblyVersion = fact.properties().get(role + "AssemblyVersion");
        String containingSymbolId = fact.properties().get(role + "ContainingSymbolId");
        try (PreparedStatement symbol = connection.prepareStatement("""
            insert or ignore into symbols (scan_id, symbol_id, language, symbol_kind, display_name, assembly_name, assembly_version, containing_symbol_id)
            values (?, ?, ?, ?, ?, ?, ?, ?);
            """)) {
            symbol.setString(1, fact.scanId());
            symbol.setString(2, symbolId);
            symbol.setString(3, language);
            symbol.setString(4, symbolKind == null ? "Unknown" : symbolKind);
            symbol.setString(5, safeDisplay);
            symbol.setString(6, assemblyName);
            symbol.setString(7, assemblyVersion);
            symbol.setString(8, containingSymbolId);
            symbol.executeUpdate();
        }
        try (PreparedStatement link = connection.prepareStatement("""
            insert or ignore into fact_symbols (fact_id, scan_id, symbol_id, role) values (?, ?, ?, ?);
            """)) {
            link.setString(1, fact.factId());
            link.setString(2, fact.scanId());
            link.setString(3, symbolId);
            link.setString(4, role);
            link.executeUpdate();
        }
        String occurrenceId = "occ-" + Hashes.sha256(fact.factId() + "|" + role + "|" + symbolId, 20);
        try (PreparedStatement occurrence = connection.prepareStatement("""
            insert or ignore into symbol_occurrences (
              occurrence_id, scan_id, symbol_id, fact_id, role, occurrence_kind, evidence_tier, rule_id, file_path, start_line, end_line
            ) values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
            """)) {
            occurrence.setString(1, occurrenceId);
            occurrence.setString(2, fact.scanId());
            occurrence.setString(3, symbolId);
            occurrence.setString(4, fact.factId());
            occurrence.setString(5, role);
            occurrence.setString(6, fact.factType());
            occurrence.setString(7, fact.evidenceTier());
            occurrence.setString(8, fact.ruleId());
            occurrence.setString(9, fact.evidence().filePath());
            occurrence.setInt(10, fact.evidence().startLine());
            occurrence.setInt(11, fact.evidence().endLine());
            occurrence.executeUpdate();
        }
    }

    private static void insertDerivedRows(Connection connection, CodeFact fact) throws SQLException {
        Map<String, String> p = fact.properties();
        if (FactTypes.SYMBOL_RELATIONSHIP.equals(fact.factType()) && p.containsKey("sourceSymbolId") && p.containsKey("targetSymbolId")) {
            try (PreparedStatement command = connection.prepareStatement("""
                insert or ignore into symbol_relationships (
                  relationship_id, scan_id, source_symbol_id, target_symbol_id, relationship_kind, rule_id, evidence_tier, file_path, start_line, end_line
                ) values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
                """)) {
                command.setString(1, "rel-" + Hashes.sha256(fact.factId(), 20));
                command.setString(2, fact.scanId());
                command.setString(3, p.get("sourceSymbolId"));
                command.setString(4, p.get("targetSymbolId"));
                command.setString(5, p.getOrDefault("relationshipKind", "Unknown"));
                command.setString(6, fact.ruleId());
                command.setString(7, fact.evidenceTier());
                command.setString(8, fact.evidence().filePath());
                command.setInt(9, fact.evidence().startLine());
                command.setInt(10, fact.evidence().endLine());
                command.executeUpdate();
            }
        }
        if (FactTypes.CALL_EDGE.equals(fact.factType()) || FactTypes.METHOD_INVOKED.equals(fact.factType())) {
            String callee = valueOr(fact.targetSymbol(), p.get("calleeSymbol"));
            if (callee != null) {
                try (PreparedStatement command = connection.prepareStatement("""
                    insert or ignore into call_edges (
                      fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name,
                      caller_assembly_version, callee_symbol, callee_assembly_name, callee_assembly_version,
                      callee_containing_type, call_kind, file_path, start_line, end_line
                    ) values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
                    """)) {
                    command.setString(1, fact.factId());
                    command.setString(2, fact.scanId());
                    command.setString(3, fact.repo());
                    command.setString(4, fact.commitSha());
                    command.setString(5, fact.evidenceTier());
                    command.setString(6, fact.ruleId());
                    command.setString(7, valueOr(fact.sourceSymbol(), p.get("callerSymbol")));
                    command.setString(8, p.get("callerAssemblyName"));
                    command.setString(9, p.get("callerAssemblyVersion"));
                    command.setString(10, callee);
                    command.setString(11, p.get("calleeAssemblyName"));
                    command.setString(12, p.get("calleeAssemblyVersion"));
                    command.setString(13, p.get("containingType"));
                    command.setString(14, p.getOrDefault("callKind", "Method"));
                    command.setString(15, fact.evidence().filePath());
                    command.setInt(16, fact.evidence().startLine());
                    command.setInt(17, fact.evidence().endLine());
                    command.executeUpdate();
                }
            }
        }
        if (FactTypes.OBJECT_CREATED.equals(fact.factType())) {
            String createdType = valueOr(p.get("createdType"), fact.targetSymbol());
            if (createdType != null) {
                try (PreparedStatement command = connection.prepareStatement("""
                    insert or ignore into object_creations (
                      fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name,
                      caller_assembly_version, created_type, created_type_assembly_name, created_type_assembly_version,
                      constructor_symbol, assigned_to, file_path, start_line, end_line
                    ) values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
                    """)) {
                    command.setString(1, fact.factId());
                    command.setString(2, fact.scanId());
                    command.setString(3, fact.repo());
                    command.setString(4, fact.commitSha());
                    command.setString(5, fact.evidenceTier());
                    command.setString(6, fact.ruleId());
                    command.setString(7, fact.sourceSymbol());
                    command.setString(8, p.get("callerAssemblyName"));
                    command.setString(9, p.get("callerAssemblyVersion"));
                    command.setString(10, createdType);
                    command.setString(11, p.get("createdTypeAssemblyName"));
                    command.setString(12, p.get("createdTypeAssemblyVersion"));
                    command.setString(13, p.get("constructorSymbol"));
                    command.setString(14, p.get("assignedTo"));
                    command.setString(15, fact.evidence().filePath());
                    command.setInt(16, fact.evidence().startLine());
                    command.setInt(17, fact.evidence().endLine());
                    command.executeUpdate();
                }
            }
        }
        if (FactTypes.ARGUMENT_PASSED.equals(fact.factType()) && fact.targetSymbol() != null) {
            try (PreparedStatement command = connection.prepareStatement("""
                insert or ignore into argument_flows (
                  fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name,
                  caller_assembly_version, callee_symbol, callee_assembly_name, callee_assembly_version, call_kind,
                  parameter_ordinal, parameter_name, parameter_type, argument_ordinal, argument_expression_kind,
                  argument_expression_hash, argument_symbol, argument_symbol_kind, argument_type, argument_assembly_name,
                  argument_assembly_version, argument_source_file, argument_source_start_line, argument_source_end_line,
                  file_path, start_line, end_line
                ) values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
                """)) {
                command.setString(1, fact.factId());
                command.setString(2, fact.scanId());
                command.setString(3, fact.repo());
                command.setString(4, fact.commitSha());
                command.setString(5, fact.evidenceTier());
                command.setString(6, fact.ruleId());
                command.setString(7, fact.sourceSymbol());
                command.setString(8, p.get("callerAssemblyName"));
                command.setString(9, p.get("callerAssemblyVersion"));
                command.setString(10, fact.targetSymbol());
                command.setString(11, p.get("calleeAssemblyName"));
                command.setString(12, p.get("calleeAssemblyVersion"));
                command.setString(13, p.getOrDefault("callKind", "Method"));
                command.setInt(14, intProperty(p, "parameterOrdinal"));
                command.setString(15, p.getOrDefault("parameterName", "arg" + intProperty(p, "parameterOrdinal")));
                command.setString(16, p.get("parameterType"));
                command.setInt(17, intProperty(p, "argumentOrdinal"));
                command.setString(18, p.get("argumentExpressionKind"));
                command.setString(19, p.get("argumentExpressionHash"));
                command.setString(20, p.get("argumentSymbol"));
                command.setString(21, p.get("argumentSymbolKind"));
                command.setString(22, p.get("argumentType"));
                command.setString(23, p.get("argumentAssemblyName"));
                command.setString(24, p.get("argumentAssemblyVersion"));
                command.setString(25, p.get("argumentSourceFile"));
                setNullableInt(command, 26, p.get("argumentSourceStartLine"));
                setNullableInt(command, 27, p.get("argumentSourceEndLine"));
                command.setString(28, fact.evidence().filePath());
                command.setInt(29, fact.evidence().startLine());
                command.setInt(30, fact.evidence().endLine());
                command.executeUpdate();
            }
        }
        if (FactTypes.LOCAL_ALIAS.equals(fact.factType()) && p.containsKey("aliasSymbol") && p.containsKey("originSymbol")) {
            try (PreparedStatement command = connection.prepareStatement("""
                insert or ignore into local_aliases (
                  fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, containing_symbol, alias_symbol,
                  alias_symbol_kind, alias_type, origin_symbol, origin_symbol_kind, origin_type, file_path, start_line, end_line
                ) values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
                """)) {
                command.setString(1, fact.factId());
                command.setString(2, fact.scanId());
                command.setString(3, fact.repo());
                command.setString(4, fact.commitSha());
                command.setString(5, fact.evidenceTier());
                command.setString(6, fact.ruleId());
                command.setString(7, p.get("containingSymbol"));
                command.setString(8, p.get("aliasSymbol"));
                command.setString(9, p.get("aliasSymbolKind"));
                command.setString(10, p.get("aliasType"));
                command.setString(11, p.get("originSymbol"));
                command.setString(12, p.get("originSymbolKind"));
                command.setString(13, p.get("originType"));
                command.setString(14, fact.evidence().filePath());
                command.setInt(15, fact.evidence().startLine());
                command.setInt(16, fact.evidence().endLine());
                command.executeUpdate();
            }
        }
    }

    private static String valueOr(String first, String second) {
        return first != null && !first.isBlank() ? first : second;
    }

    private static int intProperty(Map<String, String> properties, String key) {
        try {
            return Integer.parseInt(properties.getOrDefault(key, "0"));
        } catch (NumberFormatException exception) {
            return 0;
        }
    }

    private static void setNullableInt(PreparedStatement command, int index, String value) throws SQLException {
        if (value == null || value.isBlank()) {
            command.setObject(index, null);
            return;
        }
        try {
            command.setInt(index, Integer.parseInt(value));
        } catch (NumberFormatException exception) {
            command.setObject(index, null);
        }
    }
}
