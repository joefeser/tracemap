using PublicGenericFixture;

namespace PublicGenericCaller;

public static class Entry
{
    public static string ReadText(Container.Slot<string> slot) => slot.Current;
    public static int ReadNumber(Container.Slot<int> slot) => slot.Current;
    public static string ReadOther(OtherContainer.Slot<string> slot) => slot.Current;
    public static string CopyText(Container.Slot<string> slot, ref string value, string[] values)
        => slot.Copy(ref value, values);
    public static string ReadPlain(PlainProvider provider) => provider.Current;
}
