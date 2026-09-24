Imports System.Collections
Imports System.Collections.Generic
Imports System.Data
Imports Synthetic.Framework

' Entirely synthetic projectless VB shape. The SQL gateway is in a separately
' scanned framework root, as it can be in old Web Forms applications.
Namespace Synthetic.Data
    Public Class ChoiceNames
        Public ReadOnly Property MyList As List(Of String)

        Public Sub New()
            MyList = New List(Of String)()
            Dim rows = New ChoiceLogic().SelectNames()
            For Each row As DataRow In rows.Tables(0).Rows
                MyList.Add(row.Item("Name").ToString())
            Next
        End Sub
    End Class

    Public Class ChoiceLogic
        Private ReadOnly Repository As New ChoiceRepository()

        Public Function SelectNames() As DataSet
            Return Repository.SelectNames()
        End Function
    End Class

    Public Class ChoiceRepository
        Inherits RepositoryBase

        Public Function SelectNames() As DataSet
            Open()
            Dim parameters As New ArrayList()
            parameters.Add(New ParameterDescriptor("synthetic-id", 1))
            Return SQLDA.ExecuteProcedureDataSet("synthetic.lookup", parameters)
        End Function
    End Class

    Public Class RepositoryBase
        Protected SQLDA As ProcedureGateway

        Protected Sub Open()
            SQLDA = New ProcedureGateway()
        End Sub
    End Class
End Namespace
