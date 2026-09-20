using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TraceMap.CompiledFixtures.CSharp.Alpha
{
    public interface IExplicit
    {
        string Format(int value);
    }

    public class Widget<T> : IExplicit
    {
        private EventHandler? explicitChanged;
        public event EventHandler? Changed;
        public T? Value;
        public virtual string Name { get; set; } = string.Empty;

        public Widget()
        {
        }

        public Widget(T value) => Value = value;

        public string this[int index]
        {
            get => index.ToString();
            set => _ = value;
        }

        public event EventHandler? ExplicitChanged
        {
            add => explicitChanged += value;
            remove => explicitChanged -= value;
        }

        public int Overload(int value) => value;
        public string Overload(string value) => value;
        public int Optional(int value = 7) => value;
        public TResult Generic<TResult>(TResult value) => value;
        public void Shapes(ref int byRef, out string output, in Guid readOnly, int[] values, int? nullable)
        {
            output = $"{byRef}:{readOnly}:{values.Length}:{nullable}";
        }

        public unsafe int* Pointer(int* value) => value;
        public unsafe delegate* unmanaged[Cdecl]<int> CdeclPointer(delegate* unmanaged[Cdecl]<int> value) => value;
        public unsafe delegate* unmanaged[Stdcall]<int> StdcallPointer(delegate* unmanaged[Stdcall]<int> value) => value;
        string IExplicit.Format(int value) => value.ToString();
        public async Task<int> AsyncShape(int value) { await Task.Yield(); return value; }
        public IEnumerable<int> IteratorShape(int value) { yield return value; }
        public Func<int, int> LambdaShape(int offset) => value => value + offset;
        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);

        public class Nested<TNested>
        {
            public (T, TNested) Pair(T left, TNested right) => (left, right);
        }
    }

    public sealed class DerivedWidget : Widget<string>
    {
        public override string Name { get; set; } = "derived";
    }
}

namespace TraceMap.CompiledFixtures.CSharp.Beta
{
    public sealed class Widget
    {
        public int Overload(int value) => value + 1;
    }

    public sealed class GlobalNamespaceSentinel
    {
    }
}
