using System.Linq;

namespace EndpointServerSample.Controllers;

public sealed class RunnerRepository
{
    public string Query(string runnerId)
    {
        const string sql = "select /* TRACEMAP_SQL_SENTINEL */ runner_id, status from sample_runners where runner_id = @runnerId";
        var rows = new[]
        {
            new RunnerRecord(runnerId, "active")
        };

        return rows
            .Where(row => row.RunnerId == runnerId)
            .Select(row => row.Status)
            .FirstOrDefault() ?? sql.Length.ToString();
    }
}

public sealed record RunnerRecord(string RunnerId, string Status);
