Imports System
Imports System.Configuration
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
