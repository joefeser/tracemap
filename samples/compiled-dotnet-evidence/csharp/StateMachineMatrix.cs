using System.Collections.Generic;
using System.Threading.Tasks;

namespace TraceMap.CompiledFixtures.Equivalence;

public class StateMachineMatrix
{
    public static async Task<int> AwaitOne(int value)
    {
        await Task.Yield();
        return value;
    }

    public static IEnumerable<int> Enumerate(int value)
    {
        yield return value;
    }

    // A same-named ordinary method is not a generated state-machine endpoint.
    public bool MoveNext() => false;
}
