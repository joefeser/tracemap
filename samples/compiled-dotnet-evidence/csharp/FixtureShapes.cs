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
        public int RefShape(int value) => value;
        public int RefShape(ref int value) => value;
        public int GenericArity<TOne>() => 1;
        public int GenericArity<TOne, TTwo>() => 2;
        public int Optional(int value = 7) => value;
        public TResult Generic<TResult>(TResult value) => value;
        public decimal DecimalShape(decimal value) => value;
        public DateTime DateShape(DateTime value) => value;
        public void TypedReferenceShape(TypedReference value) { }
        public Widget<int>.Nested<string> ConstructedNested(Widget<int>.Nested<string> value) => value;
        public Widget<int>.NonGenericNested ConstructedNonGenericNested(Widget<int>.NonGenericNested value) => value;
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
            public TNested Echo(T left, TNested right) => right;
        }

        public class NonGenericNested
        {
        }
    }

    public sealed class DerivedWidget : Widget<string>
    {
        public override string Name { get; set; } = "derived";
    }

    public sealed class PrimaryConstructorShape(int value)
    {
        public int Value => value;
    }

    public ref struct RefFieldShape
    {
        private ref int value;

        public RefFieldShape(ref int value) => this.value = ref value;
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
