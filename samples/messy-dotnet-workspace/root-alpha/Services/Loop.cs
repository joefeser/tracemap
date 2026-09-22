namespace Alpha.Services;

public static class Loop
{
    public static void Enter()
    {
        First();
    }

    private static void First()
    {
        Second();
    }

    private static void Second()
    {
        Third();
    }

    private static void Third()
    {
        First();
    }

    public static void Self()
    {
        Self();
    }
}
