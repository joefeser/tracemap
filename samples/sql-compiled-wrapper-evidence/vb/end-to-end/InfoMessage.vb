Imports System
Imports System.Collections
Imports System.Data
Imports System.Data.SqlClient
Imports System.Xml
Namespace PublicSql.VisualBasic
    Public NotInheritable Class EventNamingRoot
        Public Function EventNamingProduce(connection As String) As DataSet
            Dim wrapper = New EventNamingWrapper(connection)
            wrapper.Open()
            Return wrapper.Execute()
        End Function
        Public Function EventNamingReused(connection As String) As DataSet
            Dim wrapper = New EventNamingWrapper(connection)
            wrapper.Open()
            wrapper.Execute()
            wrapper.Open()
            Return wrapper.Execute()
        End Function
        Public Function EventNameListMutation(connection As String) As DataSet
            Dim wrapper = New EventNamingWrapper(connection)
            wrapper.Open()
            wrapper.ReplaceNames()
            Return wrapper.Execute()
        End Function
        Public Function EventNameListEscape(connection As String) As DataSet
            Dim wrapper = New EventNamingWrapper(connection)
            Return wrapper.EscapeNames()
        End Function
        Public Function EventNamingMismatch(connection As String) As DataSet
            Dim wrapper = New EventNamingWrapper(connection)
            wrapper.Open()
            Return wrapper.ExecuteOtherConnection()
        End Function
        Public Function EventNamingSeparate(connection As String) As DataSet
            EventNamingProduce(connection)
            Return EventNamingProduce(connection)
        End Function
    End Class
    Public NotInheritable Class EventNamingWrapper
        Private names As New ArrayList()
        Private ReadOnly connection As SqlConnection
        Private ReadOnly handler As SqlInfoMessageEventHandler
        Public Sub New(value As String)
            connection = New SqlConnection(value)
            handler = New SqlInfoMessageEventHandler(AddressOf OnMessage)
        End Sub
        Private Sub OnMessage(sender As Object, args As SqlInfoMessageEventArgs)
            Try
                If args.Message.Substring(0, 12) = "public-meta:" Then
                    Dim document As New XmlDocument()
                    document.LoadXml(args.Message.Substring(12))
                    Dim primary = document.SelectSingleNode("/public/name")
                    If primary IsNot Nothing Then
                        names.Add(primary.Attributes("label").Value)
                        Dim ordinalAttribute As XmlNode = primary.Attributes(0)
                        names.Add(ordinalAttribute.InnerXml)
                    Else
                        For Each node As XmlNode In document.SelectNodes("/public/names/name")
                            names.Add(node.Attributes("label").Value)
                        Next
                    End If
                Else
                    names.Add("public-default")
                End If
            Catch ex As Exception
            End Try

        End Sub
        Public Sub Open()
            AddHandler connection.InfoMessage, handler
            connection.Open()
        End Sub
        Public Function Execute() As DataSet
            Dim command As New SqlCommand("public.event_result", connection)
            command.CommandType = CommandType.StoredProcedure
            Dim parameters As New ArrayList()
            parameters.Add(New FieldParameter("@public", SqlDbType.Int, 4))
            For Each value As FieldParameter In parameters
                command.Parameters.Add(New SqlParameter(value.Name, value.Type, value.Size))
            Next
            Dim result As New DataSet()
            Dim adapter As New SqlDataAdapter(command)
            adapter.Fill(result)
            Rename(result)
            RemoveHandler connection.InfoMessage, handler
            connection.Close()
            Return result
        End Function
        Public Sub ReplaceNames()
            names = New ArrayList()
        End Sub
        Public Function EscapeNames() As DataSet
            Dim parameter As New SqlParameter("@public", SqlDbType.Variant, 4)
            parameter.Value = names
            Return New DataSet()
        End Function
        Public Function ExecuteOtherConnection() As DataSet
            Dim command As New SqlCommand("public.other", New SqlConnection("public-other"))
            command.CommandType = CommandType.StoredProcedure
            Dim result As New DataSet()
            Dim adapter As New SqlDataAdapter(command)
            adapter.Fill(result)
            Rename(result)
            Return result
        End Function
        Private Sub Rename(result As DataSet)
            For index As Integer = 0 To names.Count - 1
                If index = 0 Then
                    result.DataSetName = names(index).ToString()
                Else
                    result.Tables(index - 1).TableName = names(index).ToString()
                End If
            Next
        End Sub
    End Class
    Public NotInheritable Class EventUnsafeRoot
        Private ReadOnly names As New ArrayList()
        Private ReadOnly captured As New SqlCommand("public.unsafe")
        Private Sub Unsafe(sender As Object, args As SqlInfoMessageEventArgs)
            captured.CommandText = args.Message
            names.Add(args.Message)
        End Sub
        Public Function EventUnsafeProduce() As DataSet
            Dim connection As New SqlConnection("public")
            AddHandler connection.InfoMessage, New SqlInfoMessageEventHandler(AddressOf Unsafe)
            Return EntryProtocol.ProduceInputs(1, 2, "public", True)
        End Function
        Private Sub SavedReceiver(sender As Object, args As SqlInfoMessageEventArgs)
            Dim saved = Me
            Try
                names.Add(args.Message)
            Catch ex As Exception
                names.Add(saved.ToString())
            End Try
        End Sub
        Public Function EventReceiverCatchRefusal() As DataSet
            Dim connection As New SqlConnection("public")
            AddHandler connection.InfoMessage, New SqlInfoMessageEventHandler(AddressOf SavedReceiver)
            Return EntryProtocol.ProduceInputs(1, 2, "public", True)
        End Function
        Private Sub Sender(sender As Object, args As SqlInfoMessageEventArgs)
            names.Add(sender.ToString())
        End Sub
        Public Function EventSenderRefusal() As DataSet
            Dim connection As New SqlConnection("public")
            AddHandler connection.InfoMessage, New SqlInfoMessageEventHandler(AddressOf Sender)
            Return EntryProtocol.ProduceInputs(1, 2, "public", True)
        End Function
    End Class
End Namespace
