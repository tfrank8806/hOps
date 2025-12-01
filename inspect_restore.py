import sqlite3, os
path = r"C:\\hops\\restore\\hOps.web\\hOps.db"
print("exists", os.path.exists(path))
con = sqlite3.connect(path)
cur = con.cursor()
cur.execute("SELECT name FROM sqlite_master WHERE type='table'")
tables = [row[0] for row in cur.fetchall()]
print("table count", len(tables))
print("has AspNetUsers", 'AspNetUsers' in tables)
print("has UserNotifications", 'UserNotifications' in tables)
cur.execute("SELECT COUNT(*) FROM UserNotifications")
print("UserNotifications count", cur.fetchone()[0])
con.close()
