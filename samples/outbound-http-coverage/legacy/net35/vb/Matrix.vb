Public Class LegacyMatrix
Public Sub Case000(receiver As Global.System.Net.HttpWebRequest)
Call receiver.BeginGetResponse(CType(Nothing, Global.System.AsyncCallback), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case001(receiver As Global.System.Net.HttpWebRequest)
Call receiver.EndGetResponse(CType(Nothing, Global.System.IAsyncResult))
End Sub
Public Sub Case002(receiver As Global.System.Net.HttpWebRequest)
Call receiver.GetResponse()
End Sub
Public Sub Case003(receiver As Global.System.Net.WebClient)
Call receiver.DownloadData(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case004(receiver As Global.System.Net.WebClient)
Call receiver.DownloadData("https://example.invalid/public-fixture")
End Sub
Public Sub Case005(receiver As Global.System.Net.WebClient)
Call receiver.DownloadDataAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case006(receiver As Global.System.Net.WebClient)
Call receiver.DownloadDataAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case007(receiver As Global.System.Net.WebClient)
Call receiver.DownloadFile(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case008(receiver As Global.System.Net.WebClient)
Call receiver.DownloadFile("https://example.invalid/public-fixture", CType(Nothing, Global.System.String))
End Sub
Public Sub Case009(receiver As Global.System.Net.WebClient)
Call receiver.DownloadFileAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case010(receiver As Global.System.Net.WebClient)
Call receiver.DownloadFileAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case011(receiver As Global.System.Net.WebClient)
Call receiver.DownloadString(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case012(receiver As Global.System.Net.WebClient)
Call receiver.DownloadString("https://example.invalid/public-fixture")
End Sub
Public Sub Case013(receiver As Global.System.Net.WebClient)
Call receiver.DownloadStringAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case014(receiver As Global.System.Net.WebClient)
Call receiver.DownloadStringAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case015(receiver As Global.System.Net.WebClient)
Call receiver.OpenRead(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case016(receiver As Global.System.Net.WebClient)
Call receiver.OpenRead("https://example.invalid/public-fixture")
End Sub
Public Sub Case017(receiver As Global.System.Net.WebClient)
Call receiver.OpenReadAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case018(receiver As Global.System.Net.WebClient)
Call receiver.OpenReadAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case019(receiver As Global.System.Net.WebClient)
Call receiver.OpenWrite(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case020(receiver As Global.System.Net.WebClient)
Call receiver.OpenWrite(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT")
End Sub
Public Sub Case021(receiver As Global.System.Net.WebClient)
Call receiver.OpenWrite("https://example.invalid/public-fixture")
End Sub
Public Sub Case022(receiver As Global.System.Net.WebClient)
Call receiver.OpenWrite("https://example.invalid/public-fixture", "PUT")
End Sub
Public Sub Case023(receiver As Global.System.Net.WebClient)
Call receiver.OpenWriteAsync(New Global.System.Uri("https://example.invalid/public-fixture"))
End Sub
Public Sub Case024(receiver As Global.System.Net.WebClient)
Call receiver.OpenWriteAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT")
End Sub
Public Sub Case025(receiver As Global.System.Net.WebClient)
Call receiver.OpenWriteAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Object))
End Sub
Public Sub Case026(receiver As Global.System.Net.WebClient)
Call receiver.UploadData(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case027(receiver As Global.System.Net.WebClient)
Call receiver.UploadData(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case028(receiver As Global.System.Net.WebClient)
Call receiver.UploadData("https://example.invalid/public-fixture", CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case029(receiver As Global.System.Net.WebClient)
Call receiver.UploadData("https://example.invalid/public-fixture", "PUT", CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case030(receiver As Global.System.Net.WebClient)
Call receiver.UploadDataAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case031(receiver As Global.System.Net.WebClient)
Call receiver.UploadDataAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Byte()))
End Sub
Public Sub Case032(receiver As Global.System.Net.WebClient)
Call receiver.UploadDataAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Byte()), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case033(receiver As Global.System.Net.WebClient)
Call receiver.UploadFile(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case034(receiver As Global.System.Net.WebClient)
Call receiver.UploadFile(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case035(receiver As Global.System.Net.WebClient)
Call receiver.UploadFile("https://example.invalid/public-fixture", CType(Nothing, Global.System.String))
End Sub
Public Sub Case036(receiver As Global.System.Net.WebClient)
Call receiver.UploadFile("https://example.invalid/public-fixture", "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case037(receiver As Global.System.Net.WebClient)
Call receiver.UploadFileAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case038(receiver As Global.System.Net.WebClient)
Call receiver.UploadFileAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case039(receiver As Global.System.Net.WebClient)
Call receiver.UploadFileAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case040(receiver As Global.System.Net.WebClient)
Call receiver.UploadString(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case041(receiver As Global.System.Net.WebClient)
Call receiver.UploadString(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case042(receiver As Global.System.Net.WebClient)
Call receiver.UploadString("https://example.invalid/public-fixture", CType(Nothing, Global.System.String))
End Sub
Public Sub Case043(receiver As Global.System.Net.WebClient)
Call receiver.UploadString("https://example.invalid/public-fixture", "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case044(receiver As Global.System.Net.WebClient)
Call receiver.UploadStringAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.String))
End Sub
Public Sub Case045(receiver As Global.System.Net.WebClient)
Call receiver.UploadStringAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String))
End Sub
Public Sub Case046(receiver As Global.System.Net.WebClient)
Call receiver.UploadStringAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.String), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case047(receiver As Global.System.Net.WebClient)
Call receiver.UploadValues(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case048(receiver As Global.System.Net.WebClient)
Call receiver.UploadValues(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case049(receiver As Global.System.Net.WebClient)
Call receiver.UploadValues("https://example.invalid/public-fixture", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case050(receiver As Global.System.Net.WebClient)
Call receiver.UploadValues("https://example.invalid/public-fixture", "PUT", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case051(receiver As Global.System.Net.WebClient)
Call receiver.UploadValuesAsync(New Global.System.Uri("https://example.invalid/public-fixture"), CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case052(receiver As Global.System.Net.WebClient)
Call receiver.UploadValuesAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection))
End Sub
Public Sub Case053(receiver As Global.System.Net.WebClient)
Call receiver.UploadValuesAsync(New Global.System.Uri("https://example.invalid/public-fixture"), "PUT", CType(Nothing, Global.System.Collections.Specialized.NameValueCollection), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case054(receiver As Global.System.Net.WebRequest)
Call receiver.BeginGetResponse(CType(Nothing, Global.System.AsyncCallback), CType(Nothing, Global.System.Object))
End Sub
Public Sub Case055(receiver As Global.System.Net.WebRequest)
Call receiver.EndGetResponse(CType(Nothing, Global.System.IAsyncResult))
End Sub
Public Sub Case056(receiver As Global.System.Net.WebRequest)
Call receiver.GetResponse()
End Sub
End Class
