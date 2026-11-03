namespace TraceMap.Core;

/// <summary>Integrity admission of owner declarations, not verification of database metadata or execution.</summary>
public static class SqlServerOwnerResultEvidence
{
    public const string Origin = "owner-declared-result-shape";
    public const string ExtractorVersion = "sqlserver-owner-result-declarations/1";
    public const string RuleId = "database.sqlserver.owner-declared-result-shape.v1";
    public const string Limitation = "Conditional on an owner declaration; neither DDL extraction, deployed schema, result completeness nor runtime behavior is independently verified. Keep private declarations and derived reports local.";

    public static SqlServerResultSchemaExport Produce(string declarationsJson)
    {
        var declarations = SqlServerSchemaEvidence.Read<SqlServerResultDeclarations>(declarationsJson);
        SqlServerResultEvidence.Validate(declarations, "sqlserver-owner-result-declarations/1", Origin);
        return new("sqlserver-owner-result-schema/1", SqlServerSchemaEvidence.GeneratorSha256,
            SqlServerSchemaEvidence.Digest(declarations), ExtractorVersion, RuleId,
            EvidenceTiers.Tier3SyntaxOrTextual, declarations);
    }

    public static SqlServerResultSchemaExport Admit(string exportJson, string declarationsJson, string repo, string revision)
    {
        var expected = Produce(declarationsJson);
        var actual = SqlServerSchemaEvidence.Read<SqlServerResultSchemaExport>(exportJson);
        if (SqlServerSchemaEvidence.Json(actual) != SqlServerSchemaEvidence.Json(expected)
            || expected.Declarations.Repo != repo || expected.Declarations.Revision != revision)
            throw new ArgumentException("owner-result-export-or-context-mismatch");
        return actual;
    }
}
