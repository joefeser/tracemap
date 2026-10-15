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
        // macOS exposes /var through /private/var. Fixtures that canonicalize
        // their roots must remain inside that same OS-provided temporary tree.
        // Resolve only the trusted temp root, never the requested cleanup path.
        var temporaryRoot = new[] { System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()),
                TraceMap.Cli.WebFormsReviewPreflightCommand.PhysicalPath(System.IO.Path.GetTempPath()) }
            .Select(root => root.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar)
            .FirstOrDefault(root => fullPath.StartsWith(root,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        if (temporaryRoot is null)
            throw new InvalidOperationException("Fixture cleanup must stay beneath the temporary directory.");
        if (!Directory.Exists(fullPath)) return;
        for (var directory = new DirectoryInfo(fullPath); directory is not null
            && directory.FullName.Length >= temporaryRoot.Length; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Fixture cleanup root must not traverse a link.");
        // Git loose objects are read-only on Windows. Never traverse links while
        // changing attributes, and never clear attributes outside the fixture.
        DeleteOwnedDirectory(fullPath);

        static void DeleteOwnedDirectory(string directory)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    // Remove the link itself before recursive directory removal.
                    if ((attributes & FileAttributes.Directory) != 0) Directory.Delete(entry, recursive: false);
                    else File.Delete(entry);
                }
                else if ((attributes & FileAttributes.Directory) != 0) DeleteOwnedDirectory(entry);
                else
                {
                    File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
                    File.Delete(entry);
                }
            }
            Directory.Delete(directory, recursive: false);
        }
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
