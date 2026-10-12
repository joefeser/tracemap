Public Class Matrix
Public Sub Case000(receiver As Global.System.Net.Http.HttpClient)
Call receiver.Send(New Global.System.Net.Http.HttpRequestMessage(Global.System.Net.Http.HttpMethod.Post, "https://example.invalid/message"))
End Sub
Public Sub Case001(receiver As Global.System.Net.Http.HttpClient)
Call receiver.Send(New Global.System.Net.Http.HttpRequestMessage(Global.System.Net.Http.HttpMethod.Post, "https://example.invalid/message"), CType(Nothing, Global.System.Net.Http.HttpCompletionOption))
End Sub
Public Sub Case002(receiver As Global.System.Net.Http.HttpClient)
Call receiver.Send(New Global.System.Net.Http.HttpRequestMessage(Global.System.Net.Http.HttpMethod.Post, "https://example.invalid/message"), CType(Nothing, Global.System.Net.Http.HttpCompletionOption), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case003(receiver As Global.System.Net.Http.HttpClient)
Call receiver.Send(New Global.System.Net.Http.HttpRequestMessage(Global.System.Net.Http.HttpMethod.Post, "https://example.invalid/message"), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case004(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetByteArrayAsync("https://example.invalid/public-fixture")
End Sub
Public Sub Case005(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetByteArrayAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case006(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetByteArrayAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case007(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetByteArrayAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case008(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetStreamAsync("https://example.invalid/public-fixture")
End Sub
Public Sub Case009(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetStreamAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case010(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetStreamAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case011(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetStreamAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case012(receiver As Global.System.Net.Http.HttpClient)
Call receiver.DeleteAsync("https://example.invalid/public-fixture")
End Sub
Public Sub Case013(receiver As Global.System.Net.Http.HttpClient)
Call receiver.DeleteAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case014(receiver As Global.System.Net.Http.HttpClient)
Call receiver.DeleteAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case015(receiver As Global.System.Net.Http.HttpClient)
Call receiver.DeleteAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case016(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetAsync("https://example.invalid/public-fixture")
End Sub
Public Sub Case017(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Net.Http.HttpCompletionOption))
End Sub
Public Sub Case018(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Net.Http.HttpCompletionOption), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case019(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case020(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case021(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Net.Http.HttpCompletionOption))
End Sub
Public Sub Case022(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Net.Http.HttpCompletionOption), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case023(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case024(receiver As Global.System.Net.Http.HttpClient)
Call receiver.PatchAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Net.Http.HttpContent))
End Sub
Public Sub Case025(receiver As Global.System.Net.Http.HttpClient)
Call receiver.PatchAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Net.Http.HttpContent), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case026(receiver As Global.System.Net.Http.HttpClient)
Call receiver.PatchAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Net.Http.HttpContent))
End Sub
Public Sub Case027(receiver As Global.System.Net.Http.HttpClient)
Call receiver.PatchAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Net.Http.HttpContent), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case028(receiver As Global.System.Net.Http.HttpClient)
Call receiver.PostAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Net.Http.HttpContent))
End Sub
Public Sub Case029(receiver As Global.System.Net.Http.HttpClient)
Call receiver.PostAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Net.Http.HttpContent), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case030(receiver As Global.System.Net.Http.HttpClient)
Call receiver.PostAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Net.Http.HttpContent))
End Sub
Public Sub Case031(receiver As Global.System.Net.Http.HttpClient)
Call receiver.PostAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Net.Http.HttpContent), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case032(receiver As Global.System.Net.Http.HttpClient)
Call receiver.PutAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Net.Http.HttpContent))
End Sub
Public Sub Case033(receiver As Global.System.Net.Http.HttpClient)
Call receiver.PutAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Net.Http.HttpContent), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case034(receiver As Global.System.Net.Http.HttpClient)
Call receiver.PutAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Net.Http.HttpContent))
End Sub
Public Sub Case035(receiver As Global.System.Net.Http.HttpClient)
Call receiver.PutAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Net.Http.HttpContent), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case036(receiver As Global.System.Net.Http.HttpClient)
Call receiver.SendAsync(New Global.System.Net.Http.HttpRequestMessage(Global.System.Net.Http.HttpMethod.Post, "https://example.invalid/message"))
End Sub
Public Sub Case037(receiver As Global.System.Net.Http.HttpClient)
Call receiver.SendAsync(New Global.System.Net.Http.HttpRequestMessage(Global.System.Net.Http.HttpMethod.Post, "https://example.invalid/message"), CType(Nothing, Global.System.Net.Http.HttpCompletionOption))
End Sub
Public Sub Case038(receiver As Global.System.Net.Http.HttpClient)
Call receiver.SendAsync(New Global.System.Net.Http.HttpRequestMessage(Global.System.Net.Http.HttpMethod.Post, "https://example.invalid/message"), CType(Nothing, Global.System.Net.Http.HttpCompletionOption), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case039(receiver As Global.System.Net.Http.HttpClient)
Call receiver.SendAsync(New Global.System.Net.Http.HttpRequestMessage(Global.System.Net.Http.HttpMethod.Post, "https://example.invalid/message"), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case040(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetStringAsync("https://example.invalid/public-fixture")
End Sub
Public Sub Case041(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetStringAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case042(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetStringAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case043(receiver As Global.System.Net.Http.HttpClient)
Call receiver.GetStringAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case044()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsAsyncEnumerable(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.Text.Json.JsonSerializerOptions), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case045()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsAsyncEnumerable(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.Text.Json.Serialization.Metadata.JsonTypeInfo(Of Global.System.String)), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case046()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsAsyncEnumerable(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case047()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsAsyncEnumerable(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Text.Json.JsonSerializerOptions), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case048()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsAsyncEnumerable(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Text.Json.Serialization.Metadata.JsonTypeInfo(Of Global.System.String)), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case049()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsAsyncEnumerable(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case050()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PatchAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Text.Json.JsonSerializerOptions), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case051()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PatchAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Text.Json.Serialization.Metadata.JsonTypeInfo(Of Global.System.String)), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case052()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PatchAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case053()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PatchAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String), CType(Nothing, Global.System.Text.Json.JsonSerializerOptions), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case054()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PatchAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String), CType(Nothing, Global.System.Text.Json.Serialization.Metadata.JsonTypeInfo(Of Global.System.String)), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case055()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PatchAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case056()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Text.Json.JsonSerializerOptions), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case057()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Text.Json.Serialization.Metadata.JsonTypeInfo(Of Global.System.String)), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case058()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case059()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String), CType(Nothing, Global.System.Text.Json.JsonSerializerOptions), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case060()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String), CType(Nothing, Global.System.Text.Json.Serialization.Metadata.JsonTypeInfo(Of Global.System.String)), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case061()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case062()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PutAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Text.Json.JsonSerializerOptions), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case063()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PutAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Text.Json.Serialization.Metadata.JsonTypeInfo(Of Global.System.String)), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case064()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PutAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case065()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PutAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String), CType(Nothing, Global.System.Text.Json.JsonSerializerOptions), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case066()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PutAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String), CType(Nothing, Global.System.Text.Json.Serialization.Metadata.JsonTypeInfo(Of Global.System.String)), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case067()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.PutAsJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case068()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.Type), CType(Nothing, Global.System.Text.Json.JsonSerializerOptions), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case069()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.Type), CType(Nothing, Global.System.Text.Json.Serialization.JsonSerializerContext), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case070()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.Type), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case071()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Type), CType(Nothing, Global.System.Text.Json.JsonSerializerOptions), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case072()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Type), CType(Nothing, Global.System.Text.Json.Serialization.JsonSerializerContext), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case073()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Type), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case074()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.Text.Json.JsonSerializerOptions), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case075()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.Text.Json.Serialization.Metadata.JsonTypeInfo(Of Global.System.String)), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case076()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), "https://example.invalid/public-fixture", CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case077()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Text.Json.JsonSerializerOptions), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case078()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Text.Json.Serialization.Metadata.JsonTypeInfo(Of Global.System.String)), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case079()
Call Global.System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync(Of String)(New Global.System.Net.Http.HttpClient(), New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Threading.CancellationToken))
End Sub
Public Sub Case080(receiver As Global.System.Net.HttpWebRequest)
Call receiver.BeginGetResponse(CType(Nothing, Global.System.AsyncCallback), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case081(receiver As Global.System.Net.HttpWebRequest)
Call receiver.EndGetResponse(CType(Nothing, Global.System.IAsyncResult))
End Sub
Public Sub Case082(receiver As Global.System.Net.HttpWebRequest)
Call receiver.GetResponse()
End Sub
Public Sub Case083(receiver As Global.System.Net.WebClient)
Call receiver.DownloadData("https://example.invalid/public-fixture")
End Sub
Public Sub Case084(receiver As Global.System.Net.WebClient)
Call receiver.DownloadData(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case085(receiver As Global.System.Net.WebClient)
Call receiver.UploadData("https://example.invalid/public-fixture", CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case086(receiver As Global.System.Net.WebClient)
Call receiver.UploadData("https://example.invalid/public-fixture", "PUT", CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case087(receiver As Global.System.Net.WebClient)
Call receiver.UploadData(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case088(receiver As Global.System.Net.WebClient)
Call receiver.UploadData(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case089(receiver As Global.System.Net.WebClient)
Call receiver.UploadFile("https://example.invalid/public-fixture", CType(Nothing, Global.System.String))
End Sub
Public Sub Case090(receiver As Global.System.Net.WebClient)
Call receiver.UploadFile("https://example.invalid/public-fixture", "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case091(receiver As Global.System.Net.WebClient)
Call receiver.UploadFile(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case092(receiver As Global.System.Net.WebClient)
Call receiver.UploadFile(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case093(receiver As Global.System.Net.WebClient)
Call receiver.UploadValues("https://example.invalid/public-fixture", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case094(receiver As Global.System.Net.WebClient)
Call receiver.UploadValues("https://example.invalid/public-fixture", "PUT", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case095(receiver As Global.System.Net.WebClient)
Call receiver.UploadValues(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case096(receiver As Global.System.Net.WebClient)
Call receiver.UploadValues(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case097(receiver As Global.System.Net.WebClient)
Call receiver.OpenRead("https://example.invalid/public-fixture")
End Sub
Public Sub Case098(receiver As Global.System.Net.WebClient)
Call receiver.OpenRead(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case099(receiver As Global.System.Net.WebClient)
Call receiver.OpenWrite("https://example.invalid/public-fixture")
End Sub
Public Sub Case100(receiver As Global.System.Net.WebClient)
Call receiver.OpenWrite("https://example.invalid/public-fixture", "PUT")
End Sub
Public Sub Case101(receiver As Global.System.Net.WebClient)
Call receiver.OpenWrite(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case102(receiver As Global.System.Net.WebClient)
Call receiver.OpenWrite(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT")
End Sub
Public Sub Case103(receiver As Global.System.Net.WebClient)
Call receiver.DownloadString("https://example.invalid/public-fixture")
End Sub
Public Sub Case104(receiver As Global.System.Net.WebClient)
Call receiver.DownloadString(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case105(receiver As Global.System.Net.WebClient)
Call receiver.UploadString("https://example.invalid/public-fixture", CType(Nothing, Global.System.String))
End Sub
Public Sub Case106(receiver As Global.System.Net.WebClient)
Call receiver.UploadString("https://example.invalid/public-fixture", "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case107(receiver As Global.System.Net.WebClient)
Call receiver.UploadString(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case108(receiver As Global.System.Net.WebClient)
Call receiver.UploadString(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case109(receiver As Global.System.Net.WebClient)
Call receiver.DownloadFileTaskAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.String))
End Sub
Public Sub Case110(receiver As Global.System.Net.WebClient)
Call receiver.DownloadFileTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case111(receiver As Global.System.Net.WebClient)
Call receiver.DownloadDataTaskAsync("https://example.invalid/public-fixture")
End Sub
Public Sub Case112(receiver As Global.System.Net.WebClient)
Call receiver.DownloadDataTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case113(receiver As Global.System.Net.WebClient)
Call receiver.UploadDataTaskAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case114(receiver As Global.System.Net.WebClient)
Call receiver.UploadDataTaskAsync("https://example.invalid/public-fixture", "PUT", CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case115(receiver As Global.System.Net.WebClient)
Call receiver.UploadDataTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case116(receiver As Global.System.Net.WebClient)
Call receiver.UploadDataTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case117(receiver As Global.System.Net.WebClient)
Call receiver.UploadFileTaskAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.String))
End Sub
Public Sub Case118(receiver As Global.System.Net.WebClient)
Call receiver.UploadFileTaskAsync("https://example.invalid/public-fixture", "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case119(receiver As Global.System.Net.WebClient)
Call receiver.UploadFileTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case120(receiver As Global.System.Net.WebClient)
Call receiver.UploadFileTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case121(receiver As Global.System.Net.WebClient)
Call receiver.UploadValuesTaskAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case122(receiver As Global.System.Net.WebClient)
Call receiver.UploadValuesTaskAsync("https://example.invalid/public-fixture", "PUT", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case123(receiver As Global.System.Net.WebClient)
Call receiver.UploadValuesTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case124(receiver As Global.System.Net.WebClient)
Call receiver.UploadValuesTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case125(receiver As Global.System.Net.WebClient)
Call receiver.OpenReadTaskAsync("https://example.invalid/public-fixture")
End Sub
Public Sub Case126(receiver As Global.System.Net.WebClient)
Call receiver.OpenReadTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case127(receiver As Global.System.Net.WebClient)
Call receiver.OpenWriteTaskAsync("https://example.invalid/public-fixture")
End Sub
Public Sub Case128(receiver As Global.System.Net.WebClient)
Call receiver.OpenWriteTaskAsync("https://example.invalid/public-fixture", "PUT")
End Sub
Public Sub Case129(receiver As Global.System.Net.WebClient)
Call receiver.OpenWriteTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case130(receiver As Global.System.Net.WebClient)
Call receiver.OpenWriteTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT")
End Sub
Public Sub Case131(receiver As Global.System.Net.WebClient)
Call receiver.DownloadStringTaskAsync("https://example.invalid/public-fixture")
End Sub
Public Sub Case132(receiver As Global.System.Net.WebClient)
Call receiver.DownloadStringTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case133(receiver As Global.System.Net.WebClient)
Call receiver.UploadStringTaskAsync("https://example.invalid/public-fixture", CType(Nothing, Global.System.String))
End Sub
Public Sub Case134(receiver As Global.System.Net.WebClient)
Call receiver.UploadStringTaskAsync("https://example.invalid/public-fixture", "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case135(receiver As Global.System.Net.WebClient)
Call receiver.UploadStringTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case136(receiver As Global.System.Net.WebClient)
Call receiver.UploadStringTaskAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case137(receiver As Global.System.Net.WebClient)
Call receiver.DownloadDataAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case138(receiver As Global.System.Net.WebClient)
Call receiver.DownloadDataAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case139(receiver As Global.System.Net.WebClient)
Call receiver.DownloadFile("https://example.invalid/public-fixture", CType(Nothing, Global.System.String))
End Sub
Public Sub Case140(receiver As Global.System.Net.WebClient)
Call receiver.DownloadFile(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case141(receiver As Global.System.Net.WebClient)
Call receiver.DownloadFileAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case142(receiver As Global.System.Net.WebClient)
Call receiver.DownloadFileAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case143(receiver As Global.System.Net.WebClient)
Call receiver.DownloadStringAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case144(receiver As Global.System.Net.WebClient)
Call receiver.DownloadStringAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case145(receiver As Global.System.Net.WebClient)
Call receiver.OpenReadAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case146(receiver As Global.System.Net.WebClient)
Call receiver.OpenReadAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case147(receiver As Global.System.Net.WebClient)
Call receiver.OpenWriteAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case148(receiver As Global.System.Net.WebClient)
Call receiver.OpenWriteAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT")
End Sub
Public Sub Case149(receiver As Global.System.Net.WebClient)
Call receiver.OpenWriteAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Object))
End Sub
Public Sub Case150(receiver As Global.System.Net.WebClient)
Call receiver.UploadDataAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case151(receiver As Global.System.Net.WebClient)
Call receiver.UploadDataAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case152(receiver As Global.System.Net.WebClient)
Call receiver.UploadDataAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Byte()), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case153(receiver As Global.System.Net.WebClient)
Call receiver.UploadFileAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case154(receiver As Global.System.Net.WebClient)
Call receiver.UploadFileAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case155(receiver As Global.System.Net.WebClient)
Call receiver.UploadFileAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case156(receiver As Global.System.Net.WebClient)
Call receiver.UploadStringAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case157(receiver As Global.System.Net.WebClient)
Call receiver.UploadStringAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case158(receiver As Global.System.Net.WebClient)
Call receiver.UploadStringAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case159(receiver As Global.System.Net.WebClient)
Call receiver.UploadValuesAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case160(receiver As Global.System.Net.WebClient)
Call receiver.UploadValuesAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case161(receiver As Global.System.Net.WebClient)
Call receiver.UploadValuesAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case162(receiver As Global.System.Net.WebRequest)
Call receiver.BeginGetResponse(CType(Nothing, Global.System.AsyncCallback), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case163(receiver As Global.System.Net.WebRequest)
Call receiver.EndGetResponse(CType(Nothing, Global.System.IAsyncResult))
End Sub
Public Sub Case164(receiver As Global.System.Net.WebRequest)
Call receiver.GetResponse()
End Sub
Public Sub Case165(receiver As Global.System.Net.WebRequest)
Call receiver.GetResponseAsync()
End Sub
End Class
