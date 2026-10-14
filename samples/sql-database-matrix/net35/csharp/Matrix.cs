public class SqlMatrix {
public void Case000(global::System.Data.Common.DataAdapter receiver, global::System.Data.DataSet arg0) { receiver.Fill(arg0); }
public void Case001(global::System.Data.Common.DataAdapter receiver, global::System.Data.DataSet arg0, global::System.Data.SchemaType arg1) { receiver.FillSchema(arg0, arg1); }
public void Case002(global::System.Data.Common.DataAdapter receiver, global::System.Data.DataSet arg0) { receiver.Update(arg0); }
public void Case003(global::System.Data.Common.DbCommand receiver) { receiver.ExecuteNonQuery(); }
public void Case004(global::System.Data.Common.DbCommand receiver) { receiver.ExecuteReader(); }
public void Case005(global::System.Data.Common.DbCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.ExecuteReader(arg0); }
public void Case006(global::System.Data.Common.DbCommand receiver) { receiver.ExecuteScalar(); }
public void Case007(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0) { receiver.Fill(arg0); }
public void Case008(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0, global::System.Int32 arg1, global::System.Int32 arg2, global::System.String arg3) { receiver.Fill(arg0, arg1, arg2, arg3); }
public void Case009(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0, global::System.String arg1) { receiver.Fill(arg0, arg1); }
public void Case010(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataTable arg0) { receiver.Fill(arg0); }
public void Case011(global::System.Data.Common.DbDataAdapter receiver, global::System.Int32 arg0, global::System.Int32 arg1, global::System.Data.DataTable[] arg2) { receiver.Fill(arg0, arg1, arg2); }
public void Case012(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0, global::System.Data.SchemaType arg1) { receiver.FillSchema(arg0, arg1); }
public void Case013(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0, global::System.Data.SchemaType arg1, global::System.String arg2) { receiver.FillSchema(arg0, arg1, arg2); }
public void Case014(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataTable arg0, global::System.Data.SchemaType arg1) { receiver.FillSchema(arg0, arg1); }
public void Case015(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataRow[] arg0) { receiver.Update(arg0); }
public void Case016(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0) { receiver.Update(arg0); }
public void Case017(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataSet arg0, global::System.String arg1) { receiver.Update(arg0, arg1); }
public void Case018(global::System.Data.Common.DbDataAdapter receiver, global::System.Data.DataTable arg0) { receiver.Update(arg0); }
public void Case019(global::System.Data.IDataAdapter receiver, global::System.Data.DataSet arg0) { receiver.Fill(arg0); }
public void Case020(global::System.Data.IDataAdapter receiver, global::System.Data.DataSet arg0, global::System.Data.SchemaType arg1) { receiver.FillSchema(arg0, arg1); }
public void Case021(global::System.Data.IDataAdapter receiver, global::System.Data.DataSet arg0) { receiver.Update(arg0); }
public void Case022(global::System.Data.IDbCommand receiver) { receiver.ExecuteNonQuery(); }
public void Case023(global::System.Data.IDbCommand receiver) { receiver.ExecuteReader(); }
public void Case024(global::System.Data.IDbCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.ExecuteReader(arg0); }
public void Case025(global::System.Data.IDbCommand receiver) { receiver.ExecuteScalar(); }
public void Case026(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataRow[] arg0) { receiver.WriteToServer(arg0); }
public void Case027(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0) { receiver.WriteToServer(arg0); }
public void Case028(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.DataTable arg0, global::System.Data.DataRowState arg1) { receiver.WriteToServer(arg0, arg1); }
public void Case029(global::System.Data.SqlClient.SqlBulkCopy receiver, global::System.Data.IDataReader arg0) { receiver.WriteToServer(arg0); }
public void Case030(global::System.Data.SqlClient.SqlCommand receiver) { receiver.BeginExecuteNonQuery(); }
public void Case031(global::System.Data.SqlClient.SqlCommand receiver, global::System.AsyncCallback arg0, global::System.Object arg1) { receiver.BeginExecuteNonQuery(arg0, arg1); }
public void Case032(global::System.Data.SqlClient.SqlCommand receiver) { receiver.BeginExecuteReader(); }
public void Case033(global::System.Data.SqlClient.SqlCommand receiver, global::System.AsyncCallback arg0, global::System.Object arg1) { receiver.BeginExecuteReader(arg0, arg1); }
public void Case034(global::System.Data.SqlClient.SqlCommand receiver, global::System.AsyncCallback arg0, global::System.Object arg1, global::System.Data.CommandBehavior arg2) { receiver.BeginExecuteReader(arg0, arg1, arg2); }
public void Case035(global::System.Data.SqlClient.SqlCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.BeginExecuteReader(arg0); }
public void Case036(global::System.Data.SqlClient.SqlCommand receiver) { receiver.BeginExecuteXmlReader(); }
public void Case037(global::System.Data.SqlClient.SqlCommand receiver, global::System.AsyncCallback arg0, global::System.Object arg1) { receiver.BeginExecuteXmlReader(arg0, arg1); }
public void Case038(global::System.Data.SqlClient.SqlCommand receiver, global::System.IAsyncResult arg0) { receiver.EndExecuteNonQuery(arg0); }
public void Case039(global::System.Data.SqlClient.SqlCommand receiver, global::System.IAsyncResult arg0) { receiver.EndExecuteReader(arg0); }
public void Case040(global::System.Data.SqlClient.SqlCommand receiver, global::System.IAsyncResult arg0) { receiver.EndExecuteXmlReader(arg0); }
public void Case041(global::System.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteNonQuery(); }
public void Case042(global::System.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteReader(); }
public void Case043(global::System.Data.SqlClient.SqlCommand receiver, global::System.Data.CommandBehavior arg0) { receiver.ExecuteReader(arg0); }
public void Case044(global::System.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteScalar(); }
public void Case045(global::System.Data.SqlClient.SqlCommand receiver) { receiver.ExecuteXmlReader(); }
}
