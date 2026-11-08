using System.Web.Optimization;

public static class BundleConfig
{
    public static void RegisterBundles(BundleCollection bundles)
    {
        bundles.Add(new ScriptBundle("~/bundles/app").Include(
            "~/Scripts/global.js",
            "~/Scripts/main.js"));
    }
}
