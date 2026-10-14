namespace Alpha.UntrustedLookalikes;

// Negative-control lookalike: this type must not shadow the signed framework API.
public abstract class DbCommand
{
    public object ExecuteReader() => null!;
}
