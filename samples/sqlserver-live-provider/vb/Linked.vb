Imports System.Data
Imports Microsoft.Data.SqlClient
Namespace LinkedVisualBasic
Public Module Cases
    Public Function ReaderName(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using reader = command.ExecuteReader()
                If reader.Read() Then Return reader("Name")
            End Using
        End Using
        Return Nothing
    End Function
    Public Function TableName(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim table As New DataTable()
                adapter.Fill(table)
                Return table.Rows(0)("Name")
            End Using
        End Using
    End Function
    Public Function SetName(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim results As New DataSet()
                adapter.Fill(results)
                Return results.Tables(0).Rows(0)("Name")
            End Using
        End Using
    End Function
    Public Function MissingName(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim table As New DataTable()
                adapter.Fill(table)
                Return table.Rows(0)("Missing")
            End Using
        End Using
    End Function
    Public Function CaseOnly(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim table As New DataTable()
                adapter.Fill(table)
                Return table.Rows(0)("name")
            End Using
        End Using
    End Function
    Public Function LaterTable(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim results As New DataSet()
                adapter.Fill(results)
                Return results.Tables(1).Rows(0)("Value")
            End Using
        End Using
    End Function
    ' Phase 4: one private static helper level; each call site is bound separately.
    Private Function ReadTable(connection As SqlConnection, procedure As String) As DataTable
        Using command As New SqlCommand(procedure)
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim table As New DataTable()
                adapter.Fill(table)
                Return table
            End Using
        End Using
    End Function
    Private Function ReadSet(connection As SqlConnection, procedure As String) As DataSet
        Using command As New SqlCommand(procedure)
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim results As New DataSet()
                adapter.Fill(results)
                Return results
            End Using
        End Using
    End Function
    Public Function HelperTableName(connection As SqlConnection) As Object
        Dim table = ReadTable(connection, "dbo.ReadItems")
        Return table.Rows(0)("Name")
    End Function
    Public Function HelperOtherProcedure(connection As SqlConnection) As Object
        Dim table = ReadTable(connection, "dbo.ReadOther")
        Return table.Rows(0)("Title")
    End Function
    Public Function HelperSetName(connection As SqlConnection) As Object
        Dim results = ReadSet(connection, "dbo.ReadItems")
        Return results.Tables(0).Rows(0)("Name")
    End Function
    Public Function HelperMissingName(connection As SqlConnection) As Object
        Dim table = ReadTable(connection, "dbo.ReadItems")
        Return table.Rows(0)("Missing")
    End Function
    Public Function HelperLaterTable(connection As SqlConnection) As Object
        Dim results = ReadSet(connection, "dbo.ReadItems")
        Return results.Tables(1).Rows(0)("Value")
    End Function
    ' Phase 6: Field(Of T) name/ordinal reads on rows the unchanged DataTable/DataSet/internal-helper rules bind.
    Public Function FieldTableName(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim table As New DataTable()
                adapter.Fill(table)
                Return table.Rows(0).Field(Of String)("Name")
            End Using
        End Using
    End Function
    Public Function FieldOrdinal(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim table As New DataTable()
                adapter.Fill(table)
                Return table.Rows(0).Field(Of Integer)(0)
            End Using
        End Using
    End Function
    Public Function FieldSetName(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim results As New DataSet()
                adapter.Fill(results)
                Return results.Tables(0).Rows(0).Field(Of String)("Name")
            End Using
        End Using
    End Function
    Public Function FieldMissingName(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim table As New DataTable()
                adapter.Fill(table)
                Return table.Rows(0).Field(Of String)("Missing")
            End Using
        End Using
    End Function
    Public Function FieldCaseOnly(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim table As New DataTable()
                adapter.Fill(table)
                Return table.Rows(0).Field(Of String)("name")
            End Using
        End Using
    End Function
    Public Function InternalFieldName(connection As SqlConnection) As Object
        Dim table = SharedData.ReadShared(connection, "dbo.ReadItems")
        Return table.Rows(0).Field(Of String)("Name")
    End Function
End Module
End Namespace
