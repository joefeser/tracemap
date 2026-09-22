namespace Alpha.Services;

public static class DeepChain
{
    public static void Run()
    {
        Step01();
    }

    private static void Step01() { Step02(); }
    private static void Step02() { Step03(); }
    private static void Step03() { Step04(); }
    private static void Step04() { Step05(); }
    private static void Step05() { Step06(); }
    private static void Step06() { Step07(); }
    private static void Step07() { Step08(); }
    private static void Step08() { Alpha.Data.DeepQueries.FinalStep(); }
}
