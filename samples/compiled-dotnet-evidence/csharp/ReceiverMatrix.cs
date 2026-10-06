namespace TraceMap.CompiledFixtures.Equivalence;

public class ReceiverBase
{
    public virtual int Read() => 1;
}

public class ReceiverDerived : ReceiverBase
{
    public override int Read() => 2;
    public int InvokeVirtual() => Read();
    public int InvokeBase() => base.Read();
}
