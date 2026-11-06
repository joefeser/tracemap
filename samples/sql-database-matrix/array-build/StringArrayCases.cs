using System;
using System.Data.SqlClient;

// Public compile-only fixtures. No connections; never invoke these methods.
public static class StringArrayCases
{
    public static void FourPieces(string first, string second)
    {
        new SqlCommand("UPDATE dbo.PublicWindow " + first + second + "WHERE Code = @code").ExecuteNonQuery();
    }

    public static void CompilerSelected(string first, string second, string third)
    {
        new SqlCommand("UPDATE dbo.PublicWindow " + first + second + third + "WHERE Code = @code").ExecuteNonQuery();
    }

    public static void Nested(string fragment)
    {
        var inner = String.Concat(new[] { "UPDATE dbo.PublicWindow ", fragment, " " });
        new SqlCommand(String.Concat(inner, "WHERE Code = @code")).ExecuteNonQuery();
    }

    public static void Direct(string fragment)
    {
        var text = String.Concat(new[] { "UPDATE dbo.PublicWindow ", "SET Enabled = @enabled ", fragment, " ", "WHERE Code = @code" });
        new SqlCommand(text).ExecuteNonQuery();
    }

    public static void LocalWithFinally()
    {
        var parts = new string[3];
        parts[0] = "UPDATE dbo.PublicWindow ";
        parts[1] = "SET Enabled = @enabled ";
        parts[2] = "WHERE Code = @code";
        using (var command = new SqlCommand(String.Concat(parts))) command.ExecuteNonQuery();
    }

    public static void ObjectArrayRefused()
    {
        new SqlCommand(String.Concat(new object[] { "UPDATE dbo.PublicWindow ", 7 })).ExecuteNonQuery();
    }
}
