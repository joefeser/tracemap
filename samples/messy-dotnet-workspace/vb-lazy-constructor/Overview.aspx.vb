Imports System
Imports System.Configuration
Imports System.Linq
Imports PublicLazy.Framework

' Public synthetic static corpus. Never run the website or database calls.
Partial Public Class LazyOverview
    Inherits System.Web.UI.Page

    Public Sub Load_Click(sender As Object, args As EventArgs)
        Dim choices As New SyntheticChoices()
    End Sub

    Public Sub Profile_Click(sender As Object, args As EventArgs)
        PublicQueries.Lookup(ProfileContext.EmployeeInfo.Region)
    End Sub

    Public Sub BatchGetterProfile_Click(sender As Object, args As EventArgs)
        Dim service As New StructuredProfileProbe()
        service.Lookup(PublicLazy.Framework.PublicInput.Outer)
    End Sub

    Public Sub BatchProfile_Click(sender As Object, args As EventArgs)
        Dim service As New StructuredProfileProbe()
        service.Lookup("public-user")
    End Sub
End Class

Public Class ProfileContext
    Private Shared cached As ProfileEmployee
    Public Shared ReadOnly Property EmployeeInfo As ProfileEmployee
        Get
            If cached Is Nothing Then cached = New ProfileEmployee("synthetic-user")
            Return cached
        End Get
    End Property
End Class

Public Class ProfileEmployee
    Public ReadOnly Property Region As String
    Public ReadOnly Property Email As String
    Public Sub New(user As String)
        Dim service As New ProfileService()
        Email = service.GetProfile(user)
        Region = "synthetic-region"
    End Sub
End Class

Public Class ProfileService
    Public Function GetProfile(user As String) As String
        Dim data As New ProfileData()
        Try
            Return data.GetEmail(user)
        Catch failure As Exception
            data.WriteAudit(failure.Message)
            Throw
        End Try
    End Function
End Class

Public Class SyntheticChoices
    Public Sub New()
        ' Argument evaluation can initialize another object before Lookup executes.
        PublicQueries.Lookup(LazyContext.EmployeeInfo.Region)
    End Sub
End Class

Public Class LazyContext
    Private Shared cachedEmployee As SyntheticEmployee

    Public Shared ReadOnly Property EmployeeInfo As SyntheticEmployee
        Get
            If cachedEmployee Is Nothing Then
                cachedEmployee = New SyntheticEmployee("public-user")
            End If
            Return cachedEmployee
        End Get
    End Property
End Class

Public Class SyntheticEmployee
    ' Field initialization performs additional work before the constructor body.
    Private ReadOnly preferences As New SyntheticPreferences()
    Public ReadOnly Property Region As String

    Public Sub New(user As String)
        Dim logger As New PublicLog()
        Try
            Region = PublicQueries.GetRegion(user)
            If ConfigurationManager.AppSettings("public.allow") <> "yes" Then
                logger.InsertLog("authorization denied")
            End If
            logger.InsertLiteral()
            logger.InsertReturnedLiteral()
        Catch failure As Exception
            logger.InsertLog(failure.Message)
            Throw
        End Try
    End Sub
End Class

Public Class SyntheticPreferences
    Public Sub New()
        Dim logger As New PublicLog()
        logger.InsertLog("preference initialization")
    End Sub
End Class

' Same-name decoy must never be joined to the actual provider.
Public Class UnrelatedLog
    Public Sub InsertLog(message As String)
        Throw New NotSupportedException()
    End Sub
End Class

' Compile-only regression for normal flow through VB Using, delegates and EH.
' Public synthetic data only; this method and its I/O are never executed.
Public Class StructuredProfileProbe
    Public Function Lookup(user As String, Optional period As String = "") As String
        If user.StartsWith("public-") Then
            user = "normalized-" & user
        End If
        Dim result As String = ""
        Try
            If period = String.Empty Then
                Dim nextDate As Date = Date.Now.AddMonths(1)
                period = CStr(nextDate.Year) & nextDate.Month.ToString("D2")
            End If
            Using client As New System.Net.WebClient()
                client.Headers.Add("Content-Type", "application/json")
                result = client.UploadString("https://example.invalid/profile", "POST", user & period)
            End Using
            Dim rows = New String() {result, "public-language"}
            Dim selected = rows.Where(Function(item) item.Length > 0).Select(Function(item) item.ToUpperInvariant())
            result = String.Join(", ", selected)
            result = ObserveOperand(user)
        Catch failure As Exception
            result = failure.Message
        Finally
            System.Diagnostics.Debug.WriteLine(result)
        End Try
        Return result
    End Function

    Private Function ObserveOperand(value As String) As String
        Dim data As New ProfileData()
        Return CStr(data.ExecuteSql("SELECT Value FROM public_batch WHERE Key = '" & value & "'"))
    End Function
End Class
