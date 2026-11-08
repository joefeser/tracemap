Imports System.Collections
Imports System.Data
Imports System.Data.SqlClient
Imports System.Data.Odbc

Public Structure LedgerSlot
    Public ReadOnly Item As SqlParameter

    Public Sub New(key As String, number As Integer)
        Item = New SqlParameter(key, SqlDbType.Int) With {.Value = number}
    End Sub
End Structure

Public Structure DriverSlot
    Public ReadOnly Item As OdbcParameter

    Public Sub New(key As String, number As Integer)
        Item = New OdbcParameter(key, OdbcType.Int) With {.Value = number}
    End Sub
End Structure

Public Class LedgerReader
    Public Function ReadBatch(routine As String, ByRef items As ArrayList) As DataSet
        Dim slots = DirectCast(items.ToArray(GetType(LedgerSlot)), LedgerSlot())
        Return ReadBatch(routine, slots)
    End Function

    Private Function ReadBatch(routine As String, ByRef slots As LedgerSlot()) As DataSet
        Dim rows As New DataSet()
        Using cmd As New SqlCommand(routine)
            cmd.CommandType = CommandType.StoredProcedure
            For Each slot As LedgerSlot In slots
                cmd.Parameters.Add(slot.Item)
            Next
            Using adapter As New SqlDataAdapter(cmd)
                adapter.Fill(rows)
            End Using
        End Using
        Return rows
    End Function
End Class

Public Class DriverReader
    Public Function ReadBatch(routine As String, ByRef items As ArrayList) As DataSet
        Dim slots = DirectCast(items.ToArray(GetType(DriverSlot)), DriverSlot())
        Return ReadBatch(routine, slots)
    End Function

    Private Function ReadBatch(routine As String, ByRef slots As DriverSlot()) As DataSet
        Dim rows As New DataSet()
        Using cmd As New OdbcCommand(routine)
            cmd.CommandType = CommandType.StoredProcedure
            For Each slot As DriverSlot In slots
                cmd.Parameters.Add(slot.Item)
            Next
            Using adapter As New OdbcDataAdapter(cmd)
                adapter.Fill(rows)
            End Using
        End Using
        Return rows
    End Function
End Class

Public Module ReadingDesk
    Public Function FromLedger(reader As LedgerReader) As DataSet
        Dim items As New ArrayList From {New LedgerSlot("@edition", 3)}
        Return reader.ReadBatch("sample_read", items)
    End Function

    Public Function FromDriver(reader As DriverReader) As DataSet
        Dim items As New ArrayList From {New DriverSlot("edition", 3)}
        Return reader.ReadBatch("sample_read", items)
    End Function
End Module
