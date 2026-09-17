import sqlite3
import shutil
import os

d = r"C:\Users\mdulche\code\Gaia-Life\docs\test-artifacts"
src = os.path.join(d, "gaialife.db")
p = os.path.join(d, "gaialife-merge.db")
shutil.copy(src, p)
for suffix in ("-wal", "-shm"):
    s = src + suffix
    if os.path.exists(s):
        shutil.copy(s, p + suffix)

c = sqlite3.connect(p)
print("tables:", [r[0] for r in c.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY 1")])
for t in [r[0] for r in c.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")]:
    try:
        n = c.execute(f"SELECT COUNT(*) FROM [{t}]").fetchone()[0]
        print(f"{t}: {n}")
    except Exception as e:
        print(t, e)
c.close()
