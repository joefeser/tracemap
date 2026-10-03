namespace System.Data.Common;

// Synthetic stand-in for the framework command type so the fixture's
// ExecuteReader call patterns resolve to the known ADO.NET shape without
// referencing a real database stack. Source-declared types hide the
// framework metadata type in this compilation.
public abstract class DbCommand
{
    public object ExecuteReader() => null!;
}
