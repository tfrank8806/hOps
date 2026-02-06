import sqlite3
from pathlib import Path
path = Path('hOps.web_original/hOps.web/hOps.db')
conn = sqlite3.connect(path)
cur = conn.cursor()
cur.execute("SELECT name FROM sqlite_master WHERE type='table' AND name='SalesContacts';")
print('SalesContacts table exists:', bool(cur.fetchall()))
cur.execute("SELECT COUNT(*) FROM SalesContacts")
print('SalesContacts count:', cur.fetchone()[0])
cur.close()
conn.close()
