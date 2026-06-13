def upgrade(op):
    op.execute("CREATE TABLE orders (id integer primary key, status text, total real)")
