"""Build a read-only fleet reference database from a reviewed source snapshot.
Capture input is visible-table JSON from Airfleets; no network requests are made.
"""
import argparse
import collections
import csv
import datetime as dt
import hashlib
import json
import pathlib
import re
import sqlite3
from contextlib import closing

ROOT = pathlib.Path(__file__).resolve().parents[1]
FAMILIES = {'a319':'Airbus A319','a320':'Airbus A320','a321':'Airbus A321','a330':'Airbus A330',
            'a350':'Airbus A350','b717':'Boeing 717','b737ng':'Boeing 737 NG / Max',
            'b757':'Boeing 757','b767':'Boeing 767','csr':'Airbus A220'}
EXPECTED = {'a319':57,'a320':44,'a321':229,'a330':81,'a350':41,'b717':78,'b737ng':239,'b757':90,'b767':57,'csr':90}

def normalize(pages):
    records = []
    urls = set()
    for page in pages:
        url = page['url']
        if url in urls: raise ValueError('Duplicate source page')
        urls.add(url)
        family = re.search(r'-active-([a-z0-9]+)(?:-\d+)?\.htm$', url).group(1)
        for row in page['rows']:
            c = row['cells']
            offset = 1 if family.startswith('b') else 0
            msn, model, reg, delivery = c[0], c[1+offset], c[2+offset], c[3+offset]
            remark = c[4+offset]
            status = 'Parked' if re.search(r'\bParked since\b', remark, re.I) else 'Active'
            records.append(dict(registration=reg, manufacturerSerial=msn, lineNumber=c[1] if offset else None,
                family=FAMILIES[family], familyCode=family, model=model, status=status,
                deliveryDateText=delivery, sourceUrl=url))
    counts = dict(collections.Counter(r['familyCode'] for r in records))
    if counts != EXPECTED: raise ValueError(f'Incomplete capture: {counts} != published totals {EXPECTED}')
    return {'schemaVersion':1, 'snapshotId':'airfleets-dal-2026-09-02', 'observedDate':'2026-09-02',
            'source':'Airfleets.net', 'sourceUrl':'https://www.airfleets.net/flottecie/Delta%20Air%20Lines.htm',
            'operatorCode':'DAL','operatorName':'Delta Air Lines','scope':'Mainline current fleet (active and parked)',
            'expectedCount':1006, 'expectedActive':999, 'expectedParked':7, 'pageCount':len(pages),
            'aircraft':sorted(records,key=lambda r:r['registration'])}

def validate(snapshot):
    rows = snapshot['aircraft']
    if len(rows) != snapshot['expectedCount']: raise ValueError('Aircraft count mismatch')
    if len({r['registration'] for r in rows}) != len(rows): raise ValueError('Duplicate registration')
    if len({(r['familyCode'],r['manufacturerSerial']) for r in rows}) != len(rows): raise ValueError('Duplicate manufacturer identity')
    if collections.Counter(r['status'] for r in rows) != {'Active':snapshot['expectedActive'],'Parked':snapshot['expectedParked']}: raise ValueError('Status counts mismatch')
    for r in rows:
        if not re.fullmatch(r'N[0-9A-Z]+', r['registration']): raise ValueError('Invalid US registration')
        if not r['model'] or not r['manufacturerSerial']: raise ValueError('Missing aircraft identity')
        if not r['sourceUrl'].startswith('https://www.airfleets.net/flottecie/Delta%20Air%20Lines-active-'): raise ValueError('Unexpected source')
        # Preserve source date text. Month/year-only dates must never become invented days.
        if r['deliveryDateText'] and not re.fullmatch(r'(?:\d{2}/)?(?:\d{2}/)?\d{4}', r['deliveryDateText']): raise ValueError('Unknown date precision')

def build(snapshot, output):
    validate(snapshot)
    output.parent.mkdir(parents=True, exist_ok=True)
    temp = output.with_suffix('.building.sqlite')
    if temp.exists(): raise ValueError(f'Remove stale build file after inspection: {temp}')
    with closing(sqlite3.connect(temp)) as db:
        db.executescript('''
        PRAGMA foreign_keys=ON;
        CREATE TABLE source_snapshot (snapshot_id TEXT PRIMARY KEY, source_name TEXT NOT NULL, source_url TEXT NOT NULL,
            observed_date TEXT NOT NULL, operator_code TEXT NOT NULL, operator_name TEXT NOT NULL, scope TEXT NOT NULL, expected_count INTEGER NOT NULL);
        CREATE TABLE aircraft_reference (snapshot_id TEXT NOT NULL REFERENCES source_snapshot(snapshot_id),
            registration TEXT NOT NULL, manufacturer_serial TEXT NOT NULL, line_number TEXT,
            family TEXT NOT NULL, model TEXT NOT NULL, status TEXT NOT NULL CHECK(status IN ('Active','Parked')),
            delivery_date_text TEXT NOT NULL, source_url TEXT NOT NULL,
            PRIMARY KEY(snapshot_id,registration), UNIQUE(snapshot_id,family,manufacturer_serial));
        CREATE INDEX aircraft_family ON aircraft_reference(family);
        CREATE INDEX aircraft_status ON aircraft_reference(status);
        PRAGMA user_version=1;
        ''')
        db.execute('INSERT INTO source_snapshot VALUES (?,?,?,?,?,?,?,?)',
            tuple(snapshot[k] for k in ('snapshotId','source','sourceUrl','observedDate','operatorCode','operatorName','scope','expectedCount')))
        db.executemany('INSERT INTO aircraft_reference VALUES (?,?,?,?,?,?,?,?,?)',
            [(snapshot['snapshotId'],r['registration'],r['manufacturerSerial'],r['lineNumber'],r['family'],r['model'],r['status'],r['deliveryDateText'],r['sourceUrl']) for r in snapshot['aircraft']])
        if db.execute('PRAGMA integrity_check').fetchone()[0] != 'ok': raise ValueError('SQLite integrity check failed')
        db.commit()
    temp.replace(output)
    with closing(sqlite3.connect(output)) as db:
        check = dict(db.execute('SELECT status,COUNT(*) FROM aircraft_reference GROUP BY status'))
        assert check == {'Active':999,'Parked':7}
        selected = db.execute("SELECT registration,model,manufacturer_serial,status FROM aircraft_reference WHERE registration='N414DZ'").fetchall()
    print(json.dumps({'aircraft':len(snapshot['aircraft']),'statuses':check,'N414DZ':selected,'database':str(output)},indent=2))

if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--capture',type=pathlib.Path)
    parser.add_argument('--snapshot',type=pathlib.Path,default=ROOT/'datasets/delta-airfleets-2026-09-02.json')
    args=parser.parse_args()
    if args.capture:
        snapshot=normalize(json.loads(args.capture.read_text(encoding='utf-8')))
        validate(snapshot)
        args.snapshot.parent.mkdir(parents=True,exist_ok=True)
        args.snapshot.write_text(json.dumps(snapshot,indent=2)+'\n',encoding='utf-8')
    else: snapshot=json.loads(args.snapshot.read_text(encoding='utf-8'))
    build(snapshot, ROOT/'assets/fleet/aircraft.sqlite')
    export=ROOT/'outputs/Delta-Air-Lines-Fleet-2026-09-02.csv'
    with export.open('w',newline='',encoding='utf-8-sig') as f:
        fields=list(snapshot['aircraft'][0])
        writer=csv.DictWriter(f,fieldnames=fields)
        writer.writeheader(); writer.writerows(snapshot['aircraft'])
