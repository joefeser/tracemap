Imports System.Net

' Public synthetic source. Scanning must never execute these requests.
Public Module RequestMethodEvidence
    Public Sub Proven(url As String)
        Dim req = CType(WebRequest.Create(url), HttpWebRequest)
        req.Method = "POST"
        req.ContentType = "application/json"
        req.Headers.Add("X-Synthetic", "fixture")
        Dim stream = req.GetRequestStream()
        stream.WriteByte(0)
        req.GetResponse()
    End Sub

    Public Sub Conditional(url As String, flag As Boolean)
        Dim req = CType(WebRequest.Create(url), HttpWebRequest)
        If flag Then req.Method = "POST"
        req.GetResponse()
    End Sub
End Module
