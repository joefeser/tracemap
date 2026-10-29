Option Strict Off
Imports System.Collections.Generic
Imports System.Linq
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Namespace PublicSql.VisualBasic
    Public Module JsonLinqPipelines
        Public Function Direct(tokens As IEnumerable(Of JToken)) As String
            Return String.Join(" / ", tokens.Where(Function(token) token("kind").Value(Of String)() = "public-alpha").
                Select(Function(token) token("caption").Value(Of String)()))
        End Function
        Public Function LocalPipeline(tokens As JToken) As String
            Dim captions = tokens.Where(Function(token) token("kind") = "public-alpha").
                Select(Function(token) token("caption").Value(Of String)())
            Return String.Join(" / ", captions)
        End Function
        Public Function FromJson(json As String) As String
            Dim document = CType(JsonConvert.DeserializeObject(json), JObject)
            Dim tokens = document.SelectToken("publicEntries")
            If tokens IsNot Nothing Then
                Dim captions = tokens.Where(Function(token) token("kind").Value(Of String)() = "public-alpha").
                    Select(Function(token) token("caption").Value(Of String)())
                Return String.Join(" / ", captions)
            End If
            Return ""
        End Function
        Public Function DynamicKeyRefusal(tokens As IEnumerable(Of JToken), key As String) As String
            Return String.Join(" / ", tokens.Where(Function(token) token("kind").Value(Of String)() = "public-alpha").
                Select(Function(token) token(key).Value(Of String)()))
        End Function
        Public Function HelperRefusal(tokens As IEnumerable(Of JToken)) As String
            Return String.Join(" / ", tokens.Where(Function(token) token("kind").Value(Of String)() = "public-alpha").
                Select(Function(token) Build(token)))
        End Function
        Private Function Build(token As JToken) As String
            Return token("caption").Value(Of String)()
        End Function
        Public Function ReassignmentRefusal(tokens As IEnumerable(Of JToken)) As String
            Dim captions = tokens.Where(Function(token) token("kind").Value(Of String)() = "public-alpha").
                Select(Function(token) token("caption").Value(Of String)())
            captions = Enumerable.Empty(Of String)()
            Return String.Join(" / ", captions)
        End Function
    End Module
End Namespace
