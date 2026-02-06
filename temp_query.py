import sqlite3
from pathlib import Path
path = Path('hOps.web/hOps.db.backup_20251211080216')
conn = sqlite3.connect(path)
cur = conn.cursor()
cur.execute("SELECT name FROM sqlite_master WHERE type='table' AND name='SalesLeadSubmissions';")
print(cur.fetchall())
cur.close()
conn.close()
