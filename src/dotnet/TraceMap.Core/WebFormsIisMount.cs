namespace TraceMap.Core;

/// <summary>An operator-declared IIS URL prefix and its repository-relative application root.</summary>
public sealed class WebFormsIisMount
{
    private WebFormsIisMount(string prefix, string root) => (UrlPrefix, WebRoot) = (prefix, root);

    public string UrlPrefix { get; }
    public string WebRoot { get; }

    public static WebFormsIisMount Parse(string value)
    {
        var pair = value.Split('=', 2);
        if (value.Length > 2048 || pair.Length != 2 || !pair[0].StartsWith('/')
            || (pair[0] != "/" && !SafeRelativePath(pair[0][1..]))
            || (pair[1] != "." && !SafeRelativePath(pair[1])))
            throw new ArgumentException("Invalid --webforms-iis-mount. Use /url/prefix=repo/relative/root or /url/prefix=.");
        return new WebFormsIisMount(pair[0], pair[1]);
    }

    internal static bool SafeRelativePath(string value) => value.Length is > 0 and <= 1024
        && value.Split('/').All(segment => segment.Length is > 0 and <= 255
            && segment is not "." and not ".." && !segment.EndsWith('.') && segment.Trim() == segment
            && segment.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.' or ' '));
}
