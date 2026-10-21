using System;
using System.Net;
using System.Web.UI;

// Public, compile-only synthetic fixture. Never host or execute these methods.
namespace HttpInheritance
{
    public class BaseWidget : UserControl
    {
        public string Profile { get { return Remote.Fetch(); } }
        protected override void OnLoad(EventArgs e) { Remote.Fetch(); }
    }

    public class DerivedWidget : BaseWidget
    {
        protected override void OnLoad(EventArgs e) { base.OnLoad(e); }
    }

    public class Host : Page
    {
        protected BaseWidget direct;
        protected DerivedWidget inherited;
        public string ExplicitRead() { return inherited.Profile; }
        protected void Page_Load(object sender, EventArgs e) { }
    }

    public static class Remote
    {
        public static string Fetch()
        {
            using (var client = new WebClient())
                return client.UploadString("https://example.invalid/profile", "POST", "synthetic");
        }
    }
}
