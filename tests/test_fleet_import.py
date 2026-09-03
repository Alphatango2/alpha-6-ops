import copy
import json
import pathlib
import sys
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'scripts'))
from build_fleet_database import validate

class FleetImportTests(unittest.TestCase):
    def setUp(self):
        self.data=json.loads((ROOT/'datasets/delta-airfleets-2026-09-02.json').read_text())
    def test_verified_snapshot(self): validate(self.data)
    def test_missing_page_rows(self):
        self.data['aircraft'].pop()
        with self.assertRaises(ValueError): validate(self.data)
    def test_duplicate_registration(self):
        self.data['aircraft'][1]['registration']=self.data['aircraft'][0]['registration']
        with self.assertRaises(ValueError): validate(self.data)
    def test_invalid_status(self):
        self.data['aircraft'][0]['status']='Ready to fly'
        with self.assertRaises(ValueError): validate(self.data)
    def test_foreign_source(self):
        self.data['aircraft'][0]['sourceUrl']='https://example.com'
        with self.assertRaises(ValueError): validate(self.data)
    def test_source_date_precision_preserved(self):
        self.data['aircraft'][0]['deliveryDateText']='09/2026'
        validate(self.data)
        self.assertEqual(self.data['aircraft'][0]['deliveryDateText'],'09/2026')

if __name__=='__main__': unittest.main()
