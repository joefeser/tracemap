namespace Recon.Data;

public sealed class TextCommand(string commandText) : System.Data.Common.DbCommand
{
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string CommandText { get; set; } = commandText;
    public override int CommandTimeout { get; set; }
    public override System.Data.CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override System.Data.UpdateRowSource UpdatedRowSource { get; set; }
    protected override System.Data.Common.DbConnection? DbConnection { get; set; }
    protected override System.Data.Common.DbTransaction? DbTransaction { get; set; }
    protected override System.Data.Common.DbParameterCollection DbParameterCollection => throw new System.NotSupportedException();
    public override void Cancel() => throw new System.NotSupportedException();
    public override int ExecuteNonQuery() => throw new System.NotSupportedException();
    public override object ExecuteScalar() => throw new System.NotSupportedException();
    public override void Prepare() => throw new System.NotSupportedException();
    protected override System.Data.Common.DbParameter CreateDbParameter() => throw new System.NotSupportedException();
    protected override System.Data.Common.DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) => throw new System.NotSupportedException();
}
