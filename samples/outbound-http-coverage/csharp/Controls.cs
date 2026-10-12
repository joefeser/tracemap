using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
public class Controls
{
    public void DynamicDestination(HttpClient client, string destination) { client.GetStringAsync(destination); }
    public void RequestVariable(HttpClient client, HttpRequestMessage message) { client.SendAsync(message); }
    public void Wrapper(HttpClient client, string destination) { DynamicDestination(client, destination); }
    public void Inherited(InheritedClient client) { client.GetStreamAsync("https://example.invalid/inherited"); }
    public void FakeCalls(FakeClient client) { client.GetStringAsync("ignored"); client.UploadString("ignored", "ignored"); client.DownloadString("ignored"); client.GetResponse(); }
    public void ContentOnly(HttpContent content) { content.ReadFromJsonAsync<string>(); }
    public void FileProtocol(WebClient client) { client.DownloadString("file:///public-fixture.txt"); }
    public void DynamicVerb(WebClient client, string verb) { client.UploadString("https://example.invalid/dynamic", verb, "synthetic"); }
}
public class InheritedClient : HttpClient { }
public class FakeClient
{
    public void GetStringAsync(string value) { }
    public void UploadString(string value, string data) { }
    public void DownloadString(string value) { }
    public void GetResponse() { }
}
