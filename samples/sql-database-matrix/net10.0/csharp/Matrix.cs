#pragma warning disable CS0618 // Intentional public obsolete-provider compatibility fixture; never executed.
public class SqlMatrix {
public void Case000(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.Common.DbDataReader arg0) { receiver.WriteToServer(arg0); }
public void Case001(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataRow[] arg0) { receiver.WriteToServer(arg0); }
public void Case002(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0) { receiver.WriteToServer(arg0); }
public void Case003(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0, global::System.Data.DataRowState arg1) { receiver.WriteToServer(arg0, arg1); }
public void Case004(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.IDataReader arg0) { receiver.WriteToServer(arg0); }
public void Case005(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.Common.DbDataReader arg0) { receiver.WriteToServerAsync(arg0); }
public void Case006(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.Common.DbDataReader arg0, global::System.Threading.CancellationToken arg1) { receiver.WriteToServerAsync(arg0, arg1); }
public void Case007(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataRow[] arg0) { receiver.WriteToServerAsync(arg0); }
public void Case008(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataRow[] arg0, global::System.Threading.CancellationToken arg1) { receiver.WriteToServerAsync(arg0, arg1); }
public void Case009(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0) { receiver.WriteToServerAsync(arg0); }
public void Case010(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0, global::System.Data.DataRowState arg1) { receiver.WriteToServerAsync(arg0, arg1); }
public void Case011(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0, global::System.Data.DataRowState arg1, global::System.Threading.CancellationToken arg2) { receiver.WriteToServerAsync(arg0, arg1, arg2); }
public void Case012(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0, global::System.Threading.CancellationToken arg1) { receiver.WriteToServerAsync(arg0, arg1); }
public void Case013(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.IDataReader arg0) { receiver.WriteToServerAsync(arg0); }
public void Case014(global::Microsoft.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.IDataReader arg0, global::System.Threading.CancellationToken arg1) { receiver.WriteToServerAsync(arg0, arg1); }
public void Case015(global::Microsoft.Data.SqlClient.SqlCommand receiver) { receiver.BeginExecuteNonQuery(); }
public void Case016(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.AsyncCallback arg0, global::System.Object arg1) { receiver.BeginExecuteNonQuery(arg0, arg1); }
public void Case017(global::Microsoft.Data.SqlClient.SqlCommand receiver) { receiver.BeginExecuteReader(); }
public void Case018(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.AsyncCallback arg0, global::System.Object arg1) { receiver.BeginExecuteReader(arg0, arg1); }
public void Case019(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.AsyncCallback arg0, global::System.Object arg1, global::System.Data.CommandBehavior arg2) { receiver.BeginExecuteReader(arg0, arg1, arg2); }
public void Case020(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.BeginExecuteReader(arg0); }
public void Case021(global::Microsoft.Data.SqlClient.SqlCommand receiver) { receiver.BeginExecuteXmlReader(); }
public void Case022(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.AsyncCallback arg0, global::System.Object arg1) { receiver.BeginExecuteXmlReader(arg0, arg1); }
public void Case023(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.IAsyncResult arg0) { receiver.EndExecuteNonQuery(arg0); }
public void Case024(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.IAsyncResult arg0) { receiver.EndExecuteReader(arg0); }
public void Case025(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.IAsyncResult arg0) { receiver.EndExecuteXmlReader(arg0); }
public void Case026(global::Microsoft.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteNonQuery(); }
public void Case027(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.Threading.CancellationToken arg0) { receiver.ExecuteNonQueryAsync(arg0); }
public void Case028(global::Microsoft.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteReader(); }
public void Case029(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.ExecuteReader(arg0); }
public void Case030(global::Microsoft.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteReaderAsync(); }
public void Case031(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.ExecuteReaderAsync(arg0); }
public void Case032(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.Data.CommandBehavior arg0, global::System.Threading.CancellationToken arg1) { receiver.ExecuteReaderAsync(arg0, arg1); }
public void Case033(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.Threading.CancellationToken arg0) { receiver.ExecuteReaderAsync(arg0); }
public void Case034(global::Microsoft.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteScalar(); }
public void Case035(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.Threading.CancellationToken arg0) { receiver.ExecuteScalarAsync(arg0); }
public void Case036(global::Microsoft.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteXmlReader(); }
public void Case037(global::Microsoft.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteXmlReaderAsync(); }
public void Case038(global::Microsoft.Data.SqlClient.SqlCommand receiver, global::System.Threading.CancellationToken arg0) { receiver.ExecuteXmlReaderAsync(arg0); }
public void Case039(global::System.Data.Common.DataAdapter receiver, global::System.Data.DataSet arg0) { receiver.Fill(arg0); }
public void Case040(global::System.Data.Common.DataAdapter receiver, global::System.Data.DataSet arg0, global::System.Data.SchemaType arg1) { receiver.FillSchema(arg0, arg1); }
public void Case041(global::System.Data.Common.DataAdapter receiver, global::System.Data.DataSet arg0) { receiver.Update(arg0); }
public void Case042(global::System.Data.Common.DbCommand receiver) { receiver.ExecuteNonQuery(); }
public void Case043(global::System.Data.Common.DbCommand receiver) { receiver.ExecuteNonQueryAsync(); }
public void Case044(global::System.Data.Common.DbCommand receiver, global::System.Threading.CancellationToken arg0) { receiver.ExecuteNonQueryAsync(arg0); }
public void Case045(global::System.Data.Common.DbCommand receiver) { receiver.ExecuteReader(); }
public void Case046(global::System.Data.Common.DbCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.ExecuteReader(arg0); }
public void Case047(global::System.Data.Common.DbCommand receiver) { receiver.ExecuteReaderAsync(); }
public void Case048(global::System.Data.Common.DbCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.ExecuteReaderAsync(arg0); }
public void Case049(global::System.Data.Common.DbCommand receiver, global::System.Data.CommandBehavior arg0, global::System.Threading.CancellationToken arg1) { receiver.ExecuteReaderAsync(arg0, arg1); }
public void Case050(global::System.Data.Common.DbCommand receiver, global::System.Threading.CancellationToken arg0) { receiver.ExecuteReaderAsync(arg0); }
public void Case051(global::System.Data.Common.DbCommand receiver) { receiver.ExecuteScalar(); }
public void Case052(global::System.Data.Common.DbCommand receiver) { receiver.ExecuteScalarAsync(); }
public void Case053(global::System.Data.Common.DbCommand receiver, global::System.Threading.CancellationToken arg0) { receiver.ExecuteScalarAsync(arg0); }
public void Case054(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0) { receiver.Fill(arg0); }
public void Case055(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0, global::System.Int32 arg1, global::System.Int32 arg2, global::System.String arg3) { receiver.Fill(arg0, arg1, arg2, arg3); }
public void Case056(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0, global::System.String arg1) { receiver.Fill(arg0, arg1); }
public void Case057(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataTable arg0) { receiver.Fill(arg0); }
public void Case058(global::System.Data.Common.DbDataAdapter receiver, global::System.Int32 arg0, global::System.Int32 arg1, global::System.Data.DataTable[] arg2) { receiver.Fill(arg0, arg1, arg2); }
public void Case059(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0, global::System.Data.SchemaType arg1) { receiver.FillSchema(arg0, arg1); }
public void Case060(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0, global::System.Data.SchemaType arg1, global::System.String arg2) { receiver.FillSchema(arg0, arg1, arg2); }
public void Case061(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataTable arg0, global::System.Data.SchemaType arg1) { receiver.FillSchema(arg0, arg1); }
public void Case062(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataRow[] arg0) { receiver.Update(arg0); }
public void Case063(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0) { receiver.Update(arg0); }
public void Case064(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0, global::System.String arg1) { receiver.Update(arg0, arg1); }
public void Case065(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataTable arg0) { receiver.Update(arg0); }
public void Case066(global::System.Data.IDataAdapter receiver, global::System.Data.DataSet arg0) { receiver.Fill(arg0); }
public void Case067(global::System.Data.IDataAdapter receiver, global::System.Data.DataSet arg0, global::System.Data.SchemaType arg1) { receiver.FillSchema(arg0, arg1); }
public void Case068(global::System.Data.IDataAdapter receiver, global::System.Data.DataSet arg0) { receiver.Update(arg0); }
public void Case069(global::System.Data.IDbCommand receiver) { receiver.ExecuteNonQuery(); }
public void Case070(global::System.Data.IDbCommand receiver) { receiver.ExecuteReader(); }
public void Case071(global::System.Data.IDbCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.ExecuteReader(arg0); }
public void Case072(global::System.Data.IDbCommand receiver) { receiver.ExecuteScalar(); }
public void Case073(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.Common.DbDataReader arg0) { receiver.WriteToServer(arg0); }
public void Case074(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataRow[] arg0) { receiver.WriteToServer(arg0); }
public void Case075(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0) { receiver.WriteToServer(arg0); }
public void Case076(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0, global::System.Data.DataRowState arg1) { receiver.WriteToServer(arg0, arg1); }
public void Case077(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.IDataReader arg0) { receiver.WriteToServer(arg0); }
public void Case078(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.Common.DbDataReader arg0) { receiver.WriteToServerAsync(arg0); }
public void Case079(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.Common.DbDataReader arg0, global::System.Threading.CancellationToken arg1) { receiver.WriteToServerAsync(arg0, arg1); }
public void Case080(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataRow[] arg0) { receiver.WriteToServerAsync(arg0); }
public void Case081(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataRow[] arg0, global::System.Threading.CancellationToken arg1) { receiver.WriteToServerAsync(arg0, arg1); }
public void Case082(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0) { receiver.WriteToServerAsync(arg0); }
public void Case083(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0, global::System.Data.DataRowState arg1) { receiver.WriteToServerAsync(arg0, arg1); }
public void Case084(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0, global::System.Data.DataRowState arg1, global::System.Threading.CancellationToken arg2) { receiver.WriteToServerAsync(arg0, arg1, arg2); }
public void Case085(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0, global::System.Threading.CancellationToken arg1) { receiver.WriteToServerAsync(arg0, arg1); }
public void Case086(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.IDataReader arg0) { receiver.WriteToServerAsync(arg0); }
public void Case087(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.IDataReader arg0, global::System.Threading.CancellationToken arg1) { receiver.WriteToServerAsync(arg0, arg1); }
public void Case088(global::System.Data.SqlClient.SqlCommand receiver) { receiver.BeginExecuteNonQuery(); }
public void Case089(global::System.Data.SqlClient.SqlCommand receiver, global::System.AsyncCallback arg0, global::System.Object arg1) { receiver.BeginExecuteNonQuery(arg0, arg1); }
public void Case090(global::System.Data.SqlClient.SqlCommand receiver) { receiver.BeginExecuteReader(); }
public void Case091(global::System.Data.SqlClient.SqlCommand receiver, global::System.AsyncCallback arg0, global::System.Object arg1) { receiver.BeginExecuteReader(arg0, arg1); }
public void Case092(global::System.Data.SqlClient.SqlCommand receiver, global::System.AsyncCallback arg0, global::System.Object arg1, global::System.Data.CommandBehavior arg2) { receiver.BeginExecuteReader(arg0, arg1, arg2); }
public void Case093(global::System.Data.SqlClient.SqlCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.BeginExecuteReader(arg0); }
public void Case094(global::System.Data.SqlClient.SqlCommand receiver) { receiver.BeginExecuteXmlReader(); }
public void Case095(global::System.Data.SqlClient.SqlCommand receiver, global::System.AsyncCallback arg0, global::System.Object arg1) { receiver.BeginExecuteXmlReader(arg0, arg1); }
public void Case096(global::System.Data.SqlClient.SqlCommand receiver, global::System.IAsyncResult arg0) { receiver.EndExecuteNonQuery(arg0); }
public void Case097(global::System.Data.SqlClient.SqlCommand receiver, global::System.IAsyncResult arg0) { receiver.EndExecuteReader(arg0); }
public void Case098(global::System.Data.SqlClient.SqlCommand receiver, global::System.IAsyncResult arg0) { receiver.EndExecuteXmlReader(arg0); }
public void Case099(global::System.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteNonQuery(); }
public void Case100(global::System.Data.SqlClient.SqlCommand receiver, global::System.Threading.CancellationToken arg0) { receiver.ExecuteNonQueryAsync(arg0); }
public void Case101(global::System.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteReader(); }
public void Case102(global::System.Data.SqlClient.SqlCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.ExecuteReader(arg0); }
public void Case103(global::System.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteReaderAsync(); }
public void Case104(global::System.Data.SqlClient.SqlCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.ExecuteReaderAsync(arg0); }
public void Case105(global::System.Data.SqlClient.SqlCommand receiver, global::System.Data.CommandBehavior arg0, global::System.Threading.CancellationToken arg1) { receiver.ExecuteReaderAsync(arg0, arg1); }
public void Case106(global::System.Data.SqlClient.SqlCommand receiver, global::System.Threading.CancellationToken arg0) { receiver.ExecuteReaderAsync(arg0); }
public void Case107(global::System.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteScalar(); }
public void Case108(global::System.Data.SqlClient.SqlCommand receiver, global::System.Threading.CancellationToken arg0) { receiver.ExecuteScalarAsync(arg0); }
public void Case109(global::System.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteXmlReader(); }
public void Case110(global::System.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteXmlReaderAsync(); }
public void Case111(global::System.Data.SqlClient.SqlCommand receiver, global::System.Threading.CancellationToken arg0) { receiver.ExecuteXmlReaderAsync(arg0); }
}
