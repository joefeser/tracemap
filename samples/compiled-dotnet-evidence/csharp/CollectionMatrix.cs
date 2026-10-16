using System.Collections;
using System.Collections.Generic;

namespace TraceMap.CompiledFixtures.Equivalence;

public static class CollectionMatrix
{
    public static object? ReadLegacy(ArrayList values, int index) => values[index];
    public static object ReadGeneric(List<object> values, int index) => values[index];
    public static int ReadInteger(ArrayList values, int index) => (int)values[index]!;
}
