namespace TraceMap.Core;

/// <summary>UI-independent local selection validation; never builds or runs customer code.</summary>
public static class WebFormsWizardForms
{
    public const string RuleId = "workflow.webforms.wizard-forms.v1";
    public const int MaxEntries = 10_000;
    public const int MaxInventoryEntries = 100_000;
    public const int MaxSelectionChars = 1_048_576;
    public const int MaxSelectionBytes = 1_048_576;
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase) { ".git", "bin", "obj" };

    public static IReadOnlyList<string> Discover(string webRoot)
    {
        var root = Root(webRoot);
        var found = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        var visited = 0;
        while (pending.TryPop(out var directory))
        {
            foreach (var item in Directory.EnumerateFileSystemEntries(directory))
            {
                if (++visited > MaxInventoryEntries) throw Fail("INVENTORY_LIMIT");
                var attributes = File.GetAttributes(item);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw Fail("LINKED_ENTRY");
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (!ExcludedDirectories.Contains(Path.GetFileName(item))) pending.Push(item);
                }
                else if (item.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))
                {
                    if (found.Count >= MaxEntries) throw Fail("FORM_LIMIT");
                    found.Add(Path.GetRelativePath(root, item).Replace('\\', '/'));
                }
            }
        }
        if (found.Count == 0) throw Fail("NO_FORMS");
        if (found.Any(path => path.StartsWith('#') || path != path.Trim() || path.Any(char.IsControl))) throw Fail("UNREPRESENTABLE_PATH");
        if (found.Distinct(StringComparer.OrdinalIgnoreCase).Count() != found.Count) throw Fail("CASE_AMBIGUOUS");
        return found.Order(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Parse(string webRoot, string text)
    {
        ValidateSize(text);
        var root = Root(webRoot);
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(text.TrimStart('\uFEFF'));
        while (reader.ReadLine() is { } line)
        {
            var value = line.Trim();
            if (value.Length == 0 || value.StartsWith('#')) continue;
            if (selected.Count >= MaxEntries) throw Fail("FORM_LIMIT");
            if (value.Any(char.IsControl)) throw Fail("INVALID_PATH");
            value = value.Replace('\\', '/');
            if (value.Split('/').Any(part => part is "." or "..")) throw Fail("PATH_TRAVERSAL");
            // Reject foreign drive syntax on Unix instead of treating C: as a local folder.
            if (!OperatingSystem.IsWindows() && value.Contains(':')) throw Fail("FOREIGN_PATH");
            var full = Path.GetFullPath(value, root);
            var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith("../", StringComparison.Ordinal)) throw Fail("OUTSIDE_ROOT");
            if (!relative.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)) throw Fail("NOT_FORM");
            RejectLinks(full, root);
            if (!File.Exists(full)) throw Fail("FORM_MISSING");
            if (!selected.Add(relative)) throw Fail("DUPLICATE_FORM");
        }
        if (selected.Count == 0) throw Fail("SELECTION_EMPTY");
        // Publication matching is case-insensitive. Validate the surrounding inventory
        // even when only one spelling was submitted, including during saved selection reuse.
        _ = Discover(root);
        return selected.Order(StringComparer.Ordinal).ToArray();
    }

    public static string Template(string webRoot)
    {
        var text = "# Keep the forms you want, one path per line, relative to the web root.\n" +
            "# Save this file and rerun the wizard with --continue. Empty does not mean all.\n" +
            string.Join('\n', Discover(webRoot)) + "\n";
        ValidateSize(text);
        return text;
    }

    private static void ValidateSize(string text)
    {
        if (text.Length > MaxSelectionChars || System.Text.Encoding.UTF8.GetByteCount(text) > MaxSelectionBytes)
            throw Fail("SELECTION_LIMIT");
    }

    private static string Root(string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Directory.Exists(root)) throw Fail("ROOT_MISSING");
        // The chosen root is the boundary. OS aliases above it (e.g. /var on
        // macOS) are not links discovered inside customer inventory.
        RejectLinks(root, root);
        return root;
    }

    private static void RejectLinks(string path, string root)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw Fail("LINKED_ENTRY");
            if (string.Equals(current, root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) break;
        }
    }

    private static InvalidOperationException Fail(string code) => new("WEBFORMS_WIZARD_" + code);
}
