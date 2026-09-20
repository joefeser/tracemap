namespace TraceMap.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory(string? root = null)
    {
        Path = System.IO.Path.Combine(root ?? System.IO.Path.GetTempPath(), "tracemap-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // Test cleanup should not hide assertion failures.
        }
    }
}
