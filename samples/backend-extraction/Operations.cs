using System.Data;
using System.Data.Common;

namespace Synthetic.BackendExtraction;

// Library-only fixture. Tests/scans must never invoke these methods.
public sealed class Operations(DbConnection database, HttpClient http)
{
    public object? SaveClick(int item) => SharedSave(item);
    public object? JobTick(int item) => SharedSave(item);

    private object? SharedSave(int item)
    {
        using var command = database.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "synthetic.SaveItem";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@item";
        parameter.DbType = DbType.Int32;
        parameter.Value = item;
        command.Parameters.Add(parameter);
        return command.ExecuteScalar();
    }

    // No local caller: must not automatically become "unused" or "retire".
    public int UnconnectedQuery()
    {
        using var command = database.CreateCommand();
        command.CommandText = "UPDATE SyntheticItems SET Flag = 1";
        command.CommandType = CommandType.Text;
        return command.ExecuteNonQuery();
    }

    public Task<HttpResponseMessage> RefreshClick() => http.GetAsync("https://inventory.example.invalid/items");
    public Task<HttpResponseMessage> DynamicDestination(string destination) => http.GetAsync(destination);
    public string NameOnlyDecoy() => "ExecuteScalar HttpClient.GetAsync is text, not a call";
}
