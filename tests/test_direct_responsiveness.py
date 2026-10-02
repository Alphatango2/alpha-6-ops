"""Catch wrong release authority, concealed direct failures and invalid evidence."""
import copy
import importlib.util
import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('direct_responsiveness', ROOT/'scripts/direct_responsiveness.py')
policy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(policy)

def good():
    return {'contracts': {name: {'passed': True, 'checks': 1, 'boundary': 'measured application endpoint'}
                         for name in ('input','map','telemetry','integrity','restoration','liveness','dashboard')},
            'knownBadDetected': True, 'knownGoodAccepted': True,
            'suites': {name: {'passed': True, 'checks': 1} for name in
                       ('direct','operations','presentation','delivery','restoration','interaction-control','scheduling','terminal-recovery','regression','replay')},
            'heartbeat': {'passed': False, 'baselineMaxMs': 288.1277, 'stressedMaxMs': 513.2116,
                          'role': 'diagnostic', 'thresholdsChanged': False}}

class AcceptanceRoleTests(unittest.TestCase):
    def test_a_partial_diagnostic_or_missing_supplement_cannot_pass(self):
        for name in good()['suites']:
            for mutation in ('missing','failed','empty'):
                with self.subTest(name=name,mutation=mutation):
                    evidence=good()
                    if mutation=='missing': del evidence['suites'][name]
                    elif mutation=='empty': evidence['suites'][name]['checks']=0
                    else: evidence['suites'][name]['passed']=False
                    self.assertFalse(policy.evaluate(evidence)['passed'])

    def test_preserved_bad_handoff_and_corrected_native_handoff(self):
        c1=json.loads((ROOT/'docs/flight-tracking/evidence/7.24c-native-c1-20260928-194840/native-analysis-v2.json').read_text(encoding='utf-8-sig'))
        t1=json.loads((ROOT/'docs/flight-tracking/evidence/7.24c-terminal-t1-native-20260928-195009/analysis.json').read_text(encoding='utf-8-sig'))
        bad=max(r['delayMs'] for r in c1['longestLiveNonTimerQueuedOperations'] if 'ApplyAsync' in r['name'])
        corrected=t1['trace']['liveMapQueueMs']['max']
        self.assertAlmostEqual(bad,4280.8266)
        self.assertFalse(policy.handoff_within_existing_control(bad))
        self.assertTrue(policy.handoff_within_existing_control(corrected))
        self.assertEqual(t1['trace']['arrived'],t1['trace']['processed'])
        self.assertTrue(t1['trace']['arrivalProcessedTimestampsMatch'])
        self.assertTrue(t1['trace']['traceTelemetryAllFoundInJournal'])

    def test_historical_heartbeat_failure_cannot_reject_complete_direct_pass(self):
        evidence=good(); original=copy.deepcopy(evidence)
        result=policy.evaluate(evidence)
        self.assertTrue(result['passed'])
        self.assertEqual(evidence,original)
        self.assertFalse(result['heartbeat']['passed'])

    def test_green_heartbeat_cannot_hide_any_failed_direct_contract(self):
        for name in good()['contracts']:
            with self.subTest(name=name):
                evidence=good(); evidence['heartbeat'].update(passed=True,baselineMaxMs=300,stressedMaxMs=100)
                evidence['contracts'][name]['passed']=False
                self.assertFalse(policy.evaluate(evidence)['passed'])

    def test_missing_empty_or_uncalibrated_direct_evidence_fails_closed(self):
        for change in ('missing','empty','unmeasured','calibration','heartbeat'):
            with self.subTest(change=change):
                evidence=good()
                if change=='missing': del evidence['contracts']['input']
                elif change=='empty': evidence['contracts']['input']['checks']=0
                elif change=='unmeasured': evidence['contracts']['map']['boundary']=''
                elif change=='calibration': evidence['knownBadDetected']=False
                else: del evidence['heartbeat']
                result=policy.evaluate(evidence)
                self.assertFalse(result['passed'])
                reason={'missing':'input:','empty':'input:','unmeasured':'map:','calibration':'knownBadDetected:','heartbeat':'heartbeat:'}[change]
                self.assertTrue(any(f.startswith(reason) for f in result['failures']))

if __name__=='__main__': unittest.main()
