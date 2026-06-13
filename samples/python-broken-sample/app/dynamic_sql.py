def query(table):
    sql = f"SELECT * FROM {table}"
    cursor.execute(sql)
