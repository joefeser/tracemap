using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.Data.SqlClient;

namespace SampleCatalog;

public readonly struct LedgerSlot
{
    public readonly SqlParameter Item;
    public LedgerSlot(string key, int number) =>
        Item = new SqlParameter(key, SqlDbType.Int) { Value = number };
}

public readonly struct DriverSlot
{
    public readonly OdbcParameter Item;
    public DriverSlot(string key, int number) =>
        Item = new OdbcParameter(key, OdbcType.Int) { Value = number };
}

public class LedgerReader
{
    public DataSet ReadBatch(string routine, ref ArrayList items)
    {
        var slots = (LedgerSlot[])items.ToArray(typeof(LedgerSlot));
        return ReadBatch(routine, ref slots);
    }

    private DataSet ReadBatch(string routine, ref LedgerSlot[] slots)
    {
        var rows = new DataSet();
        using var cmd = new SqlCommand(routine) { CommandType = CommandType.StoredProcedure };
        foreach (var slot in slots)
            cmd.Parameters.Add(slot.Item);
        using var adapter = new SqlDataAdapter(cmd);
        adapter.Fill(rows);
        return rows;
    }
}

public class DriverReader
{
    public DataSet ReadBatch(string routine, ref ArrayList items)
    {
        var slots = (DriverSlot[])items.ToArray(typeof(DriverSlot));
        return ReadBatch(routine, ref slots);
    }

    private DataSet ReadBatch(string routine, ref DriverSlot[] slots)
    {
        var rows = new DataSet();
        using var cmd = new OdbcCommand(routine) { CommandType = CommandType.StoredProcedure };
        foreach (var slot in slots)
            cmd.Parameters.Add(slot.Item);
        using var adapter = new OdbcDataAdapter(cmd);
        adapter.Fill(rows);
        return rows;
    }
}

public static class ReadingDesk
{
    public static DataSet FromLedger(LedgerReader reader)
    {
        var items = new ArrayList { new LedgerSlot("@edition", 3) };
        return reader.ReadBatch("sample_read", ref items);
    }

    public static DataSet FromDriver(DriverReader reader)
    {
        var items = new ArrayList { new DriverSlot("edition", 3) };
        return reader.ReadBatch("sample_read", ref items);
    }
}
