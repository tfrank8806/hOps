import sqlite3
con=sqlite3.connect(r'C:\hops\hOps.web\bin\Debug\net8.0\hOps.db')
cur=con.cursor()
cur.execute("SELECT name FROM sqlite_master WHERE type='table'")
print(cur.fetchall())
cur.execute("SELECT Id, PropertyId, substr(Content,1,200) FROM ManagerAnnouncements LIMIT 5")
print(cur.fetchall())
con.close()
