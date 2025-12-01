import sqlite3
path = r"C:\\hops\\hOps.web\\hOps.db"
con = sqlite3.connect(path)
cur = con.cursor()
cur.execute("SELECT name FROM sqlite_master WHERE type='table'")
print([row[0] for row in cur.fetchall()])
con.close()
