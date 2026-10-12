Imports System.Data
Imports System.Data.SqlClient

Namespace PublicLazy.Framework
    ' Analysis-only runtime identity boundary. No session or database is executed.
    Public Class RuntimeIdentity
        Public Identifier As String
        Public Impersonated As RuntimeIdentity
        Public Shared ReadOnly Property ActiveIdentifier As String
            Get
                Try
                    If Current.Impersonated IsNot Nothing Then
                        Return Current.Impersonated.Identifier
                    End If
                    Return Current.Identifier
                Catch ex As Exception
                    Return Nothing
                End Try
            End Get
        End Property
        Private Shared ReadOnly Property Current As RuntimeIdentity
            Get
                Dim context = System.Web.HttpContext.Current
                Dim key = context.User.Identity.Name & ":synthetic-user"
                If context.Session IsNot Nothing Then
                    Return DirectCast(context.Session(key), RuntimeIdentity)
                End If
                Return DirectCast(context.Cache(key), RuntimeIdentity)
            End Get
        End Property
    End Class
    Public Class PublicInput
        Public Shared ReadOnly Property Outer As String
            Get
                Return Inner
            End Get
        End Property
        Private Shared ReadOnly Property Inner As String
            Get
                Return "public-user"
            End Get
        End Property
    End Class
    Public Class ProfileData
        Public Function GetEmail(user As String) As String
            ' Deliberately unused: creating parameters does not bind them to SQL.
            Dim parameters As New System.Collections.ArrayList()
            parameters.Add(New SqlParameter("@user", user))
            Dim receiver As ProfileData = Me
            Return CStr(receiver.ExecuteSql("SELECT Email FROM public_people WHERE UserId = '" & user & "'"))
        End Function

        Public Function ExecuteSql(text As String) As Object
            Dim command As New SqlCommand(text)
            command.CommandType = CommandType.Text
            Return command.ExecuteScalar()
        End Function

        Public Sub WriteAudit(message As String)
            Dim command As New SqlCommand("public.synthetic_audit")
            command.CommandType = CommandType.StoredProcedure
            command.Parameters.AddWithValue("@message", message)
            command.ExecuteScalar()
        End Sub
    End Class

    ' Synthetic analysis-only provider, compiled into a separate assembly.
    Public Class PublicLog
        Public Sub InsertLog(message As String)
            Dim text As String = BuildText(message)
            ExecuteText(text)
        End Sub

        Public Sub InsertLiteral()
            ExecuteText("SELECT 1 /* public literal control */")
        End Sub

        Public Sub InsertReturnedLiteral()
            ExecuteText(LiteralText())
        End Sub

        ' Analysis-only getter/return matrix. Never execute database calls.
        Private _dynamicText As String
        Private Shared _sharedDynamicText As String
        Private Shared _choice As Boolean
        Private ReadOnly Property GetterOuter As String
            Get
                Return GetterInner
            End Get
        End Property
        Private ReadOnly Property GetterInner As String
            Get
                Return LiteralText()
            End Get
        End Property
        Private ReadOnly Property DynamicOuter As String
            Get
                Return DynamicInner
            End Get
        End Property
        Private ReadOnly Property DynamicInner As String
            Get
                Return _dynamicText
            End Get
        End Property
        Private Shared ReadOnly Property SharedDynamic As String
            Get
                Return _sharedDynamicText
            End Get
        End Property
        Private Shared Function ReturnedConcat(value As String) As String
            Return String.Concat("public-prefix:", value)
        End Function
        Private Shared Function ReturnedChoice(value As String) As String
            Dim result As String = value
            If _choice Then result = String.Concat("public-choice:", value)
            Return result
        End Function
        Public Sub InsertGetterLiteral()
            ExecuteText(GetterOuter)
        End Sub
        Public Sub InsertReturnFrameLimit()
            ExecuteText(Frame0())
        End Sub
        Private Shared Function Frame0() As String
            Return Frame1()
        End Function
        Private Shared Function Frame1() As String
            Return Frame2()
        End Function
        Private Shared Function Frame2() As String
            Return Frame3()
        End Function
        Private Shared Function Frame3() As String
            Return Frame4()
        End Function
        Private Shared Function Frame4() As String
            Return Frame5()
        End Function
        Private Shared Function Frame5() As String
            Return Frame6()
        End Function
        Private Shared Function Frame6() As String
            Return Frame7()
        End Function
        Private Shared Function Frame7() As String
            Return Frame8()
        End Function
        Private Shared Function Frame8() As String
            Return Frame9()
        End Function
        Private Shared Function Frame9() As String
            Return Frame10()
        End Function
        Private Shared Function Frame10() As String
            Return Frame11()
        End Function
        Private Shared Function Frame11() As String
            Return Frame12()
        End Function
        Private Shared Function Frame12() As String
            Return Frame13()
        End Function
        Private Shared Function Frame13() As String
            Return Frame14()
        End Function
        Private Shared Function Frame14() As String
            Return Frame15()
        End Function
        Private Shared Function Frame15() As String
            Return Frame16()
        End Function
        Private Shared Function Frame16() As String
            Return Frame17()
        End Function
        Private Shared Function Frame17() As String
            Return "public-frame-limit"
        End Function
        Public Sub InsertGetterDynamic()
            ExecuteText(DynamicOuter)
        End Sub
        Public Sub InsertGetterSharedDynamic()
            ExecuteText(SharedDynamic)
        End Sub
        Public Sub InsertReturnedConcat()
            ExecuteText(ReturnedConcat(ForwardText(LiteralText())))
        End Sub
        Public Sub InsertReturnedChoice()
            ExecuteText(ReturnedChoice(LiteralText()))
        End Sub
        Public Sub InsertReturnedConcatArgument(value As String)
            ExecuteText(ReturnedConcat(ForwardText(value)))
        End Sub

        Public Sub InsertReturnedLiteralTwoHops()
            ForwardToExecutor(LiteralText())
        End Sub

        Public Sub InsertComposedTextTwoHops(value As String)
            ' Framework producer is intentionally not supplied as a scanned DLL.
            ForwardToExecutor(String.Concat("SELECT ", value))
        End Sub

        Private Sub ForwardToExecutor(text As String)
            ExecuteText(text)
        End Sub

        Private Shared Function LiteralText() As String
            Return "SELECT 1 /* public literal control */"
        End Function

        Public Sub InsertForwardedLiteral()
            ExecuteText(ForwardText(LiteralText()))
        End Sub

        Private Shared Function ForwardText(value As String) As String
            Return value
        End Function

        Public Sub InsertRecursiveText()
            ExecuteText(RecursiveText())
        End Sub

        Private Shared Function RecursiveText() As String
            Return RecursiveOther()
        End Function

        Private Shared Function RecursiveOther() As String
            Return RecursiveText()
        End Function

        Public Sub InsertVirtualText()
            ExecuteText(VirtualText())
        End Sub

        Public Overridable Function VirtualText() As String
            Return "SELECT 1 /* virtual target not proven */"
        End Function

        Private Function BuildText(message As String) As String
            ' A returned string is deliberately distinct from caller-slot forwarding.
            Return String.Concat("SELECT LEN('", message.Replace("'", "''"), "')")
        End Function

        Public Overridable Function ExecuteText(text As String) As Object
            Dim command As New SqlCommand()
            command.CommandText = text
            command.CommandType = CommandType.Text
            Return command.ExecuteScalar()
        End Function
    End Class

    Public Class PublicQueries
        Public Shared Function GetRegion(user As String) As String
            Return user
        End Function

        Public Shared Sub Lookup(region As String)
            Dim command As New SqlCommand("public.synthetic_lookup")
            command.CommandType = CommandType.StoredProcedure
            command.Parameters.AddWithValue("@region", region)
            Dim adapter As New SqlDataAdapter(command)
            adapter.Fill(New DataSet())
        End Sub
    End Class
End Namespace
