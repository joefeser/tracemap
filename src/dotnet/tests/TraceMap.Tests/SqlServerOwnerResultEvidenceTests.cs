using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class SqlServerOwnerResultEvidenceTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private static SqlServerResultDeclarations Declaration => new(
        "sqlserver-owner-result-declarations/1", "example", Revision,
        SqlServerOwnerResultEvidence.Origin, "ordinal-exact-no-aliases",
        [new(new("ExampleServer", "ExampleDatabase", "dbo", "ReadItems"), "owner.json", 1, 1,
            "first", "complete", null, [new("Id", 0, 1, "int")])]);

    [Fact]
    public void Owner_declaration_is_deterministic_conditional_evidence()
    {
        var json = SqlServerSchemaEvidence.Json(Declaration);
        var export = SqlServerOwnerResultEvidence.Produce(json);
        Assert.Equal(SqlServerSchemaEvidence.Json(export), SqlServerSchemaEvidence.Json(SqlServerOwnerResultEvidence.Produce(json)));
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, export.Tier);
        Assert.Equal(SqlServerOwnerResultEvidence.RuleId, export.RuleId);
        Assert.Equal(SqlServerSchemaEvidence.Digest(Declaration), export.BoundedInputSha256);
        Assert.Equal(SqlServerSchemaEvidence.Json(export), SqlServerSchemaEvidence.Json(
            SqlServerOwnerResultEvidence.Admit(SqlServerSchemaEvidence.Json(export), json, "example", Revision)));
    }

    [Fact]
    public void Owner_admission_rejects_tampering_context_and_synthetic_origin()
    {
        var declaration = Declaration;
        var json = SqlServerSchemaEvidence.Json(declaration);
        var export = SqlServerOwnerResultEvidence.Produce(json);
        Assert.Throws<ArgumentException>(() => SqlServerOwnerResultEvidence.Admit(
            SqlServerSchemaEvidence.Json(export with { BoundedInputSha256 = new string('0', 64) }), json, "example", Revision));
        Assert.Throws<ArgumentException>(() => SqlServerOwnerResultEvidence.Admit(SqlServerSchemaEvidence.Json(export), json, "other", Revision));
        Assert.Throws<ArgumentException>(() => SqlServerOwnerResultEvidence.Admit(SqlServerSchemaEvidence.Json(export), json, "example", new string('2', 40)));
        Assert.Throws<ArgumentException>(() => SqlServerOwnerResultEvidence.Produce(SqlServerSchemaEvidence.Json(
            declaration with { Origin = "independent-synthetic-result-declarations" })));
        Assert.Throws<ArgumentException>(() => SqlServerResultEvidence.Admit(SqlServerSchemaEvidence.Json(export), json, "example", Revision));
    }

    [Fact]
    public void Unknown_shape_remains_unknown_and_empty_complete_shape_is_rejected()
    {
        var declaration = Declaration;
        var unknown = declaration with { Shapes = [declaration.Shapes[0] with
            { Completeness = "unknown", UnknownReason = "owner-incomplete", Columns = [] }] };
        Assert.Equal("unknown", SqlServerOwnerResultEvidence.Produce(SqlServerSchemaEvidence.Json(unknown)).Declarations.Shapes[0].Completeness);
        Assert.Throws<ArgumentException>(() => SqlServerOwnerResultEvidence.Produce(SqlServerSchemaEvidence.Json(
            declaration with { Shapes = [declaration.Shapes[0] with { Columns = [] }] })));
    }
}
