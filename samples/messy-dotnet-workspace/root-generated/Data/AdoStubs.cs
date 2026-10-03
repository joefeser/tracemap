namespace System.Data.Common;

// Synthetic stand-in; no database or runtime network operation is performed.
public abstract class DbCommand
{
    public object ExecuteReader() => null!;
}
