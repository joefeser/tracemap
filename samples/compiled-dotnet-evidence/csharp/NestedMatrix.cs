namespace TraceMap.CompiledFixtures.Equivalence;

public class NestedMatrix<TOuter>
{
    public class Inner<TInner>
    {
        public static TOuter Outer(TOuter value) => value;
        public static TInner InnerValue(TInner value) => value;
        public static TMethod Method<TMethod>(TMethod value) => value;
        public static NestedMatrix<int>.Inner<string> Construct(NestedMatrix<int>.Inner<string> value) => value;
        public static NestedMatrix<string>.Inner<int> Swap(NestedMatrix<string>.Inner<int> value) => value;
    }

    public class Plain
    {
        public static TOuter Outer(TOuter value) => value;
    }
}
