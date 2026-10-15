namespace TraceMap.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory(string? root = null)
    {
        Path = System.IO.Path.Combine(root ?? System.IO.Path.GetTempPath(), "tracemap-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public static void DeleteTree(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        var temporaryRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar)
            + System.IO.Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(temporaryRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Fixture cleanup must stay beneath the temporary directory.");
        if (!Directory.Exists(fullPath)) return;
        for (var directory = new DirectoryInfo(fullPath); directory is not null
            && directory.FullName.Length >= temporaryRoot.Length; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Fixture cleanup root must not traverse a link.");
        // Git loose objects are read-only on Windows. Never traverse links while
        // changing attributes, and never clear attributes outside the fixture.
        foreach (var file in Directory.EnumerateFiles(fullPath, "*", new EnumerationOptions
        { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false }))
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        Directory.Delete(fullPath, recursive: true);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                DeleteTree(Path);
            }
        }
        catch
        {
            // Test cleanup should not hide assertion failures.
        }
    }
}
