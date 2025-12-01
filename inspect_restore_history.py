import sqlite3
path = r"C:\\hops\\restore\\hOps.web\\hOps.db"
con = sqlite3.connect(path)
cur = con.cursor()
cur.execute("SELECT name FROM sqlite_master WHERE name='__EFMigrationsHistory'")
print('history table exists', bool(cur.fetchall()))
cur.execute("SELECT * FROM __EFMigrationsHistory")
rows = cur.fetchall()
print('history rows', rows)
con.close()
