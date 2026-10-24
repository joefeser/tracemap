using System.Net;

// Public synthetic source. Scanning must never execute these requests.
public static class RequestMethodEvidence
{
    public static void Proven(string url)
    {
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.Method = "POST";
        req.ContentType = "application/json";
        req.Headers.Add("X-Synthetic", "fixture");
        var stream = req.GetRequestStream();
        stream.WriteByte(0);
        req.GetResponse();
    }

    public static void Conditional(string url, bool flag)
    {
        var req = (HttpWebRequest)WebRequest.Create(url);
        if (flag) req.Method = "POST";
        req.GetResponse();
    }
}
