namespace Alpha.Data;

public static class DeepQueries
{
    public static void FinalStep()
    {
        _ = new TextCommand("SELECT Id FROM deep_orders WHERE Id = @id").ExecuteReader();
    }
}
