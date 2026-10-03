namespace Recon.Services;

// Deliberately same-named methods in multiple classes in one file. Both
// routes converge on the same retained SQL terminal after two call levels.
public static class RouteOne
{
    public static void Run() => StepOne.Run();
}

public static class RouteTwo
{
    public static void Run() => StepTwo.Run();
}

public static class StepOne
{
    public static void Run() => Store.Read();
}

public static class StepTwo
{
    public static void Run() => Store.Read();
}

public static class Store
{
    public static object Read() => new Recon.Data.TextCommand("SELECT Id FROM shared_ledger WHERE Id = @id").ExecuteReader();
}
