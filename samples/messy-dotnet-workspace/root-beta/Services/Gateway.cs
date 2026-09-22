namespace Beta.Services;

public static class Gateway
{
    public static void Process()
    {
        Core();
    }

    private static void Core()
    {
        _ = new Beta.Data.TextCommand("SELECT Id FROM beta_status WHERE Id = @id").ExecuteReader();
    }
}
