using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;

namespace TraceMap.Tests;

public sealed class VbNetFixtureTests
{
    [Fact]
    public void Checked_in_vb_fixture_files_parse_without_errors()
    {
        var repoRoot = FindRepoRoot();
        var fixtureDirectories = new[]
        {
            "vb-modern-sample",
            "vb-legacy-sample",
            "vb-webforms-sample"
        };
        var files = fixtureDirectories
            .SelectMany(directory => Directory.EnumerateFiles(
                Path.Combine(repoRoot, "samples", directory),
                "*.vb",
                SearchOption.AllDirectories))
            .Where(path => !ContainsDirectory(path, "bin") && !ContainsDirectory(path, "obj"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(13, files.Length);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            var tree = VisualBasicSyntaxTree.ParseText(File.ReadAllText(file), path: relativePath);
            AssertNoParseErrors(tree, relativePath);
        }
    }

    // The legacy fixture models the ToolsVersion=3.5 (vbc 9.0) toolset, so its
    // readable files must stay valid under Visual Basic 9: implicit line
    // continuation and other VB 10+ syntax would inject unrelated compile errors
    // on top of the intentionally unresolved vendor reference.
    [Fact]
    public void Legacy_vb_fixture_files_parse_as_visual_basic_9()
    {
        var repoRoot = FindRepoRoot();
        var files = Directory.EnumerateFiles(
                Path.Combine(repoRoot, "samples", "vb-legacy-sample"),
                "*.vb",
                SearchOption.AllDirectories)
            .Where(path => !ContainsDirectory(path, "bin") && !ContainsDirectory(path, "obj"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(6, files.Length);

        var parseOptions = VisualBasicParseOptions.Default
            .WithLanguageVersion(LanguageVersion.VisualBasic9);
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            var tree = VisualBasicSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, path: relativePath);
            AssertNoParseErrors(tree, relativePath);
        }
    }

    private static void AssertNoParseErrors(SyntaxTree tree, string relativePath)
    {
        var errors = tree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToArray();

        Assert.True(
            errors.Length == 0,
            $"{relativePath} contains VB syntax errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }

    private static bool ContainsDirectory(string path, string directoryName) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals(directoryName, StringComparison.OrdinalIgnoreCase));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (File.Exists(gitPath) || Directory.Exists(gitPath))
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "samples")))
                {
                    return directory.FullName;
                }
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
