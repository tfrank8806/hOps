import zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
path=Path(r"C:\Users\Tyler Frankenberger\Dropbox\Aimbridge\Hotel Docs\0 - Area\Housekeeping\HK Daily Recap.xlsx")
with zipfile.ZipFile(path) as zf:
    data = zf.read('xl/sharedStrings.xml').decode('utf-8', errors='ignore')
    if '7.' in data:
        print('found 7 in shared strings')
    else:
        print('no 7 label in shared strings? length', len(data))
