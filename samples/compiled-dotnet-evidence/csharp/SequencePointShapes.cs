namespace TraceMap.CompiledFixtures.CSharp.SequencePoints;

public static class SequencePointShapes
{
    public static int MultiDocumentNonMonotonic(int value)
    {
#line 20 "VirtualSequenceA.cs"
        var first = value + 1;
#line 5 "VirtualSequenceB.cs"
        var second = first + 1;
#line default
        return second;
    }
}
