import sqlite3, os
path = r"C:\\hops\\hOps.web\\bin\\Debug\\net8.0\\hOps.db"
print("exists", os.path.exists(path))
con = sqlite3.connect(path)
cur = con.cursor()
cur.execute("SELECT name FROM sqlite_master WHERE type='table'")
tables = cur.fetchall()
print("tables count", len(tables))
print("first tables", tables[:5])
cur.execute("SELECT name FROM sqlite_master WHERE name='__EFMigrationsHistory'")
print("history entries", cur.fetchall())
con.close()
