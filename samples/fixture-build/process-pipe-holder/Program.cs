using System.Diagnostics;

// Public synthetic process-boundary fixture. Never launches a customer site.
if (args[0] == "hold")
{
    if (args.Length > 1) File.WriteAllText(args[1], Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    Thread.Sleep(10_000);
    return;
}
if (args[0] == "environment")
{
    foreach (var name in new[] { "MSBUILDDISABLENODEREUSE", "DOTNET_CLI_USE_MSBUILD_SERVER", "UseSharedCompilation" })
        Console.WriteLine(name + "=" + Environment.GetEnvironmentVariable(name));
    return;
}
var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
start.ArgumentList.Add(typeof(Program).Assembly.Location);
start.ArgumentList.Add("hold");
using var child = Process.Start(start)!;
File.WriteAllText(args[1], child.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
Console.WriteLine("parent-exiting");
Console.Error.WriteLine("parent-error");
