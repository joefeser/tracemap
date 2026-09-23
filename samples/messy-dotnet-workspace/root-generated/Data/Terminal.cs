namespace Generated.Data;

public static class Terminal
{
    public static void Query()
    {
        var command = new TextCommand("SELECT Id FROM generated_queue WHERE Id = @id");
        _ = command.ExecuteReader();
    }
}

public sealed class TextCommand(string commandText) : System.Data.Common.DbCommand
{
    public string CommandText => commandText;
}
