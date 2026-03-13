import zipfile, xml.etree.ElementTree as ET
from pathlib import Path
path=Path(r"C:\Users\Tyler Frankenberger\Dropbox\Aimbridge\Hotel Docs\0 - Area\Housekeeping\Monthly Linen Inventory.xlsx")
with zipfile.ZipFile(path) as zf:
    sheets=zf.namelist()
    print('sheets', sheets)
