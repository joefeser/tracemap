namespace Beta.Data;

public sealed class TextCommand(string commandText) : System.Data.Common.DbCommand
{
    public string CommandText => commandText;
}
