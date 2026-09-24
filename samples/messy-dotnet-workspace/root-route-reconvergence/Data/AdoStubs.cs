namespace System.Data.Common;

// Public synthetic stand-in: no database or runtime SQL execution occurs.
public abstract class DbCommand
{
    public object ExecuteReader() => null!;
}
