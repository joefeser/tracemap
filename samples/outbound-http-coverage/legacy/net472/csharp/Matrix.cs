public class LegacyMatrix {
public void Case000(global::System.Net.Http.HttpClient receiver) { receiver.DeleteAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case001(global::System.Net.Http.HttpClient receiver) { receiver.DeleteAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Threading.CancellationToken)); }
public void Case002(global::System.Net.Http.HttpClient receiver) { receiver.DeleteAsync("https://example.invalid/public-fixture"); }
public void Case003(global::System.Net.Http.HttpClient receiver) { receiver.DeleteAsync("https://example.invalid/public-fixture", default(global::System.Threading.CancellationToken)); }
public void Case004(global::System.Net.Http.HttpClient receiver) { receiver.GetAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case005(global::System.Net.Http.HttpClient receiver) { receiver.GetAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Net.Http.HttpCompletionOption)); }
public void Case006(global::System.Net.Http.HttpClient receiver) { receiver.GetAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Net.Http.HttpCompletionOption), default(global::System.Threading.CancellationToken)); }
public void Case007(global::System.Net.Http.HttpClient receiver) { receiver.GetAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Threading.CancellationToken)); }
public void Case008(global::System.Net.Http.HttpClient receiver) { receiver.GetAsync("https://example.invalid/public-fixture"); }
public void Case009(global::System.Net.Http.HttpClient receiver) { receiver.GetAsync("https://example.invalid/public-fixture", default(global::System.Net.Http.HttpCompletionOption)); }
public void Case010(global::System.Net.Http.HttpClient receiver) { receiver.GetAsync("https://example.invalid/public-fixture", default(global::System.Net.Http.HttpCompletionOption), default(global::System.Threading.CancellationToken)); }
public void Case011(global::System.Net.Http.HttpClient receiver) { receiver.GetAsync("https://example.invalid/public-fixture", default(global::System.Threading.CancellationToken)); }
public void Case012(global::System.Net.Http.HttpClient receiver) { receiver.GetByteArrayAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case013(global::System.Net.Http.HttpClient receiver) { receiver.GetByteArrayAsync("https://example.invalid/public-fixture"); }
public void Case014(global::System.Net.Http.HttpClient receiver) { receiver.GetStreamAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case015(global::System.Net.Http.HttpClient receiver) { receiver.GetStreamAsync("https://example.invalid/public-fixture"); }
public void Case016(global::System.Net.Http.HttpClient receiver) { receiver.GetStringAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case017(global::System.Net.Http.HttpClient receiver) { receiver.GetStringAsync("https://example.invalid/public-fixture"); }
public void Case018(global::System.Net.Http.HttpClient receiver) { receiver.PostAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Net.Http.HttpContent)); }
public void Case019(global::System.Net.Http.HttpClient receiver) { receiver.PostAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Net.Http.HttpContent), default(global::System.Threading.CancellationToken)); }
public void Case020(global::System.Net.Http.HttpClient receiver) { receiver.PostAsync("https://example.invalid/public-fixture", default(global::System.Net.Http.HttpContent)); }
public void Case021(global::System.Net.Http.HttpClient receiver) { receiver.PostAsync("https://example.invalid/public-fixture", default(global::System.Net.Http.HttpContent), default(global::System.Threading.CancellationToken)); }
public void Case022(global::System.Net.Http.HttpClient receiver) { receiver.PutAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Net.Http.HttpContent)); }
public void Case023(global::System.Net.Http.HttpClient receiver) { receiver.PutAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Net.Http.HttpContent), default(global::System.Threading.CancellationToken)); }
public void Case024(global::System.Net.Http.HttpClient receiver) { receiver.PutAsync("https://example.invalid/public-fixture", default(global::System.Net.Http.HttpContent)); }
public void Case025(global::System.Net.Http.HttpClient receiver) { receiver.PutAsync("https://example.invalid/public-fixture", default(global::System.Net.Http.HttpContent), default(global::System.Threading.CancellationToken)); }
public void Case026(global::System.Net.Http.HttpClient receiver) { receiver.SendAsync(new global::System.Net.Http.HttpRequestMessage(global::System.Net.Http.HttpMethod.Post, "https://example.invalid/message")); }
public void Case027(global::System.Net.Http.HttpClient receiver) { receiver.SendAsync(new global::System.Net.Http.HttpRequestMessage(global::System.Net.Http.HttpMethod.Post, "https://example.invalid/message"), default(global::System.Net.Http.HttpCompletionOption)); }
public void Case028(global::System.Net.Http.HttpClient receiver) { receiver.SendAsync(new global::System.Net.Http.HttpRequestMessage(global::System.Net.Http.HttpMethod.Post, "https://example.invalid/message"), default(global::System.Net.Http.HttpCompletionOption), default(global::System.Threading.CancellationToken)); }
public void Case029(global::System.Net.Http.HttpClient receiver) { receiver.SendAsync(new global::System.Net.Http.HttpRequestMessage(global::System.Net.Http.HttpMethod.Post, "https://example.invalid/message"), default(global::System.Threading.CancellationToken)); }
public void Case030(global::System.Net.HttpWebRequest receiver) { receiver.BeginGetResponse(default(global::System.AsyncCallback), default(global::System.Object)); }
public void Case031(global::System.Net.HttpWebRequest receiver) { receiver.EndGetResponse(default(global::System.IAsyncResult)); }
public void Case032(global::System.Net.HttpWebRequest receiver) { receiver.GetResponse(); }
public void Case033(global::System.Net.WebClient receiver) { receiver.DownloadData(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case034(global::System.Net.WebClient receiver) { receiver.DownloadData("https://example.invalid/public-fixture"); }
public void Case035(global::System.Net.WebClient receiver) { receiver.DownloadDataAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case036(global::System.Net.WebClient receiver) { receiver.DownloadDataAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Object)); }
public void Case037(global::System.Net.WebClient receiver) { receiver.DownloadDataTaskAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case038(global::System.Net.WebClient receiver) { receiver.DownloadDataTaskAsync("https://example.invalid/public-fixture"); }
public void Case039(global::System.Net.WebClient receiver) { receiver.DownloadFile(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.String)); }
public void Case040(global::System.Net.WebClient receiver) { receiver.DownloadFile("https://example.invalid/public-fixture", default(global::System.String)); }
public void Case041(global::System.Net.WebClient receiver) { receiver.DownloadFileAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.String)); }
public void Case042(global::System.Net.WebClient receiver) { receiver.DownloadFileAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.String), default(global::System.Object)); }
public void Case043(global::System.Net.WebClient receiver) { receiver.DownloadFileTaskAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.String)); }
public void Case044(global::System.Net.WebClient receiver) { receiver.DownloadFileTaskAsync("https://example.invalid/public-fixture", default(global::System.String)); }
public void Case045(global::System.Net.WebClient receiver) { receiver.DownloadString(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case046(global::System.Net.WebClient receiver) { receiver.DownloadString("https://example.invalid/public-fixture"); }
public void Case047(global::System.Net.WebClient receiver) { receiver.DownloadStringAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case048(global::System.Net.WebClient receiver) { receiver.DownloadStringAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Object)); }
public void Case049(global::System.Net.WebClient receiver) { receiver.DownloadStringTaskAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case050(global::System.Net.WebClient receiver) { receiver.DownloadStringTaskAsync("https://example.invalid/public-fixture"); }
public void Case051(global::System.Net.WebClient receiver) { receiver.OpenRead(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case052(global::System.Net.WebClient receiver) { receiver.OpenRead("https://example.invalid/public-fixture"); }
public void Case053(global::System.Net.WebClient receiver) { receiver.OpenReadAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case054(global::System.Net.WebClient receiver) { receiver.OpenReadAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Object)); }
public void Case055(global::System.Net.WebClient receiver) { receiver.OpenReadTaskAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case056(global::System.Net.WebClient receiver) { receiver.OpenReadTaskAsync("https://example.invalid/public-fixture"); }
public void Case057(global::System.Net.WebClient receiver) { receiver.OpenWrite(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case058(global::System.Net.WebClient receiver) { receiver.OpenWrite(new global::System.Uri("https://example.invalid/public-fixture"), "PUT"); }
public void Case059(global::System.Net.WebClient receiver) { receiver.OpenWrite("https://example.invalid/public-fixture"); }
public void Case060(global::System.Net.WebClient receiver) { receiver.OpenWrite("https://example.invalid/public-fixture", "PUT"); }
public void Case061(global::System.Net.WebClient receiver) { receiver.OpenWriteAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case062(global::System.Net.WebClient receiver) { receiver.OpenWriteAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT"); }
public void Case063(global::System.Net.WebClient receiver) { receiver.OpenWriteAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.Object)); }
public void Case064(global::System.Net.WebClient receiver) { receiver.OpenWriteTaskAsync(new global::System.Uri("https://example.invalid/public-fixture")); }
public void Case065(global::System.Net.WebClient receiver) { receiver.OpenWriteTaskAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT"); }
public void Case066(global::System.Net.WebClient receiver) { receiver.OpenWriteTaskAsync("https://example.invalid/public-fixture"); }
public void Case067(global::System.Net.WebClient receiver) { receiver.OpenWriteTaskAsync("https://example.invalid/public-fixture", "PUT"); }
public void Case068(global::System.Net.WebClient receiver) { receiver.UploadData(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Byte[])); }
public void Case069(global::System.Net.WebClient receiver) { receiver.UploadData(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.Byte[])); }
public void Case070(global::System.Net.WebClient receiver) { receiver.UploadData("https://example.invalid/public-fixture", default(global::System.Byte[])); }
public void Case071(global::System.Net.WebClient receiver) { receiver.UploadData("https://example.invalid/public-fixture", "PUT", default(global::System.Byte[])); }
public void Case072(global::System.Net.WebClient receiver) { receiver.UploadDataAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Byte[])); }
public void Case073(global::System.Net.WebClient receiver) { receiver.UploadDataAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.Byte[])); }
public void Case074(global::System.Net.WebClient receiver) { receiver.UploadDataAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.Byte[]), default(global::System.Object)); }
public void Case075(global::System.Net.WebClient receiver) { receiver.UploadDataTaskAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Byte[])); }
public void Case076(global::System.Net.WebClient receiver) { receiver.UploadDataTaskAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.Byte[])); }
public void Case077(global::System.Net.WebClient receiver) { receiver.UploadDataTaskAsync("https://example.invalid/public-fixture", default(global::System.Byte[])); }
public void Case078(global::System.Net.WebClient receiver) { receiver.UploadDataTaskAsync("https://example.invalid/public-fixture", "PUT", default(global::System.Byte[])); }
public void Case079(global::System.Net.WebClient receiver) { receiver.UploadFile(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.String)); }
public void Case080(global::System.Net.WebClient receiver) { receiver.UploadFile(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.String)); }
public void Case081(global::System.Net.WebClient receiver) { receiver.UploadFile("https://example.invalid/public-fixture", default(global::System.String)); }
public void Case082(global::System.Net.WebClient receiver) { receiver.UploadFile("https://example.invalid/public-fixture", "PUT", default(global::System.String)); }
public void Case083(global::System.Net.WebClient receiver) { receiver.UploadFileAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.String)); }
public void Case084(global::System.Net.WebClient receiver) { receiver.UploadFileAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.String)); }
public void Case085(global::System.Net.WebClient receiver) { receiver.UploadFileAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.String), default(global::System.Object)); }
public void Case086(global::System.Net.WebClient receiver) { receiver.UploadFileTaskAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.String)); }
public void Case087(global::System.Net.WebClient receiver) { receiver.UploadFileTaskAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.String)); }
public void Case088(global::System.Net.WebClient receiver) { receiver.UploadFileTaskAsync("https://example.invalid/public-fixture", default(global::System.String)); }
public void Case089(global::System.Net.WebClient receiver) { receiver.UploadFileTaskAsync("https://example.invalid/public-fixture", "PUT", default(global::System.String)); }
public void Case090(global::System.Net.WebClient receiver) { receiver.UploadString(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.String)); }
public void Case091(global::System.Net.WebClient receiver) { receiver.UploadString(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.String)); }
public void Case092(global::System.Net.WebClient receiver) { receiver.UploadString("https://example.invalid/public-fixture", default(global::System.String)); }
public void Case093(global::System.Net.WebClient receiver) { receiver.UploadString("https://example.invalid/public-fixture", "PUT", default(global::System.String)); }
public void Case094(global::System.Net.WebClient receiver) { receiver.UploadStringAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.String)); }
public void Case095(global::System.Net.WebClient receiver) { receiver.UploadStringAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.String)); }
public void Case096(global::System.Net.WebClient receiver) { receiver.UploadStringAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.String), default(global::System.Object)); }
public void Case097(global::System.Net.WebClient receiver) { receiver.UploadStringTaskAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.String)); }
public void Case098(global::System.Net.WebClient receiver) { receiver.UploadStringTaskAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.String)); }
public void Case099(global::System.Net.WebClient receiver) { receiver.UploadStringTaskAsync("https://example.invalid/public-fixture", default(global::System.String)); }
public void Case100(global::System.Net.WebClient receiver) { receiver.UploadStringTaskAsync("https://example.invalid/public-fixture", "PUT", default(global::System.String)); }
public void Case101(global::System.Net.WebClient receiver) { receiver.UploadValues(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Collections.Specialized.NameValueCollection)); }
public void Case102(global::System.Net.WebClient receiver) { receiver.UploadValues(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.Collections.Specialized.NameValueCollection)); }
public void Case103(global::System.Net.WebClient receiver) { receiver.UploadValues("https://example.invalid/public-fixture", default(global::System.Collections.Specialized.NameValueCollection)); }
public void Case104(global::System.Net.WebClient receiver) { receiver.UploadValues("https://example.invalid/public-fixture", "PUT", default(global::System.Collections.Specialized.NameValueCollection)); }
public void Case105(global::System.Net.WebClient receiver) { receiver.UploadValuesAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Collections.Specialized.NameValueCollection)); }
public void Case106(global::System.Net.WebClient receiver) { receiver.UploadValuesAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.Collections.Specialized.NameValueCollection)); }
public void Case107(global::System.Net.WebClient receiver) { receiver.UploadValuesAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.Collections.Specialized.NameValueCollection), default(global::System.Object)); }
public void Case108(global::System.Net.WebClient receiver) { receiver.UploadValuesTaskAsync(new global::System.Uri("https://example.invalid/public-fixture"), default(global::System.Collections.Specialized.NameValueCollection)); }
public void Case109(global::System.Net.WebClient receiver) { receiver.UploadValuesTaskAsync(new global::System.Uri("https://example.invalid/public-fixture"), "PUT", default(global::System.Collections.Specialized.NameValueCollection)); }
public void Case110(global::System.Net.WebClient receiver) { receiver.UploadValuesTaskAsync("https://example.invalid/public-fixture", default(global::System.Collections.Specialized.NameValueCollection)); }
public void Case111(global::System.Net.WebClient receiver) { receiver.UploadValuesTaskAsync("https://example.invalid/public-fixture", "PUT", default(global::System.Collections.Specialized.NameValueCollection)); }
public void Case112(global::System.Net.WebRequest receiver) { receiver.BeginGetResponse(default(global::System.AsyncCallback), default(global::System.Object)); }
public void Case113(global::System.Net.WebRequest receiver) { receiver.EndGetResponse(default(global::System.IAsyncResult)); }
public void Case114(global::System.Net.WebRequest receiver) { receiver.GetResponse(); }
public void Case115(global::System.Net.WebRequest receiver) { receiver.GetResponseAsync(); }
}
