"""Read preserved raw capture; produce derived evidence, never edit source data."""
import collections
import datetime
import hashlib
import json
import pathlib

root = pathlib.Path('docs/flight-tracking/evidence/7.24c-fenix-shutdown-native-20260928')
raw = root / 'raw'
manifest = json.loads((raw / 'manifest.json').read_text())
summary = json.loads((raw / 'summary.json').read_text())
provided_hashes = json.loads((raw / 'hashes.json').read_text())
for name, expected in provided_hashes.items():
    assert hashlib.sha256((raw / name).read_bytes()).hexdigest() == expected
rows = [json.loads(line) for line in (raw / 'signals.jsonl').read_text().splitlines()]
fields = collections.defaultdict(list)
for row in rows:
    if row['kind'] == 'signal':
        fields[row['name']].append(row)
assert summary['completed'] and summary['exitReason'] == 'duration-complete'
assert len(fields) == 27 and sum(map(len, fields.values())) == 4860
assert all(len(values) == 180 and all(r['finite'] for r in values) for values in fields.values())
assert {name: len(values) for name, values in fields.items()} == summary['signals']
assert not [r for r in rows if r['kind'] in ('exception', 'capture-error', 'quit')]
assert all(r['hresult'] >= 0 for r in rows if r['kind'] == 'registration')

def obs(row):
    return {key: row[key] for key in ('utc', 'elapsedSeconds', 'value')}

def transitions(name):
    values = fields[name]
    return [obs(row) for index, row in enumerate(values) if not index or row['value'] != values[index-1]['value']]

def first(name, predicate):
    return next(row for row in fields[name] if predicate(row['value']))

def at_or_before(name, elapsed):
    return next(row for row in reversed(fields[name]) if row['elapsedSeconds'] <= elapsed)

engines = {}
for engine, prompt in ((2, 15), (1, 35)):
    name = f'L:S_ENG_MASTER_{engine}'
    master = first(name, lambda value: value == 0)
    prior = fields[name][fields[name].index(master)-1]
    combustion = first(f'GENERAL ENG COMBUSTION:{engine}', lambda value: value == 0)
    selected = {
        'masterOff': master,
        'combustionAbsent': combustion,
        'fuelValveClosed': first(f'GENERAL ENG FUEL VALVE:{engine}', lambda value: value == 0),
        'n1Below3': first(f'TURB ENG N1:{engine}', lambda value: 0 <= value < 3),
        'n2Below5': first(f'TURB ENG N2:{engine}', lambda value: 0 <= value < 5),
        'fuelFlowAtMost1Pph': first(f'TURB ENG FUEL FLOW PPH:{engine}', lambda value: 0 <= value <= 1),
        'fuelFlowZero': first(f'TURB ENG FUEL FLOW PPH:{engine}', lambda value: value == 0),
    }
    assert len(transitions(name)) == 2 and transitions(name)[0]['value'] == 1
    assert master['elapsedSeconds'] < combustion['elapsedSeconds']
    assert all(row['value'] == 0 for row in fields[name] if row['elapsedSeconds'] >= master['elapsedSeconds'])
    engines[str(engine)] = {
        'promptElapsedSeconds': prompt,
        'previousMasterOn': obs(prior),
        'masterOffObservedAfterPromptSeconds': master['elapsedSeconds'] - prompt,
        'milestones': {key: {**obs(row), 'secondsSinceObservedMasterOff': row['elapsedSeconds']-master['elapsedSeconds']} for key, row in selected.items()},
        'valuesAtFirstCombustionAbsent': {
            signal: obs(at_or_before(f'{signal}:{engine}', combustion['elapsedSeconds']))
            for signal in ('TURB ENG N1', 'TURB ENG N2', 'TURB ENG FUEL FLOW PPH', 'GENERAL ENG OIL PRESSURE')
        },
    }
all_terminal = max(engines[str(e)]['milestones'][key]['elapsedSeconds'] for e in (1, 2) for key in ('masterOff', 'combustionAbsent'))
all_flow = max(engines[str(e)]['milestones']['fuelFlowAtMost1Pph']['elapsedSeconds'] for e in (1, 2))
first_second_off = engines['2']['milestones']['masterOff']['elapsedSeconds']
first_first_off = engines['1']['milestones']['masterOff']['elapsedSeconds']
independent = [row for row in fields['L:S_ENG_MASTER_1'] if first_second_off <= row['elapsedSeconds'] < first_first_off]
assert independent and all(row['value'] == 1 for row in independent)
clock = fields['ABSOLUTE TIME']
steps = [right['value']-left['value'] for left, right in zip(clock, clock[1:])]
assert min(steps) > 0 and max(steps) < 2
for name, expected in [('SIM ON GROUND', 1), ('BRAKE PARKING POSITION', 1), ('IS SLEW ACTIVE', 0), ('NUMBER OF ENGINES', 2)]:
    assert all(row['value'] == expected for row in fields[name])
assert max(row['value'] for row in fields['GROUND VELOCITY']) < .5
pause = [row for row in rows if row['kind'] == 'pause']
assert pause and all(row['flags'] == 0 for row in pause)
analysis = {
    'captureIdentity': str(raw), 'providedHashesMatch': True,
    'kindCounts': dict(collections.Counter(row['kind'] for row in rows)),
    'elapsedDurationSeconds': rows[-1]['elapsedSeconds'],
    'signalObservations': 4860, 'fields': 27, 'observationsPerField': 180,
    'aircraftTitles': sorted(set(row['value'] for row in fields['TITLE'])),
    'packageVersions': [item['manifest']['package_version'] for item in manifest['packages']],
    'perField': {name: {'count': len(values), 'min': min(r['value'] for r in values), 'max': max(r['value'] for r in values),
                       'firstReceiptUtc': values[0]['utc'], 'lastReceiptUtc': values[-1]['utc'],
                       'maxReceiptGapSeconds': max(right['elapsedSeconds']-left['elapsedSeconds'] for left, right in zip(values,values[1:])),
                       'allFinite': all(row['finite'] for row in values)} for name, values in fields.items()},
    'booleanTransitions': {name: transitions(name) for name in fields if any(word in name for word in ('MASTER', 'COMBUSTION', 'FUEL VALVE', 'FAILED', 'STARTER'))},
    'engines': engines,
    'independentMasterIntervalSeconds': first_first_off-first_second_off,
    'allMastersOffAndCombustionAbsentElapsedSeconds': all_terminal,
    'allFuelFlowAtMost1PphElapsedSeconds': all_flow,
    'extraFuelFlowWaitSeconds': all_flow-all_terminal,
    'pauseEvents': pause,
    'simTimeProgression': {'firstAbsoluteSeconds': clock[0]['value'], 'lastAbsoluteSeconds': clock[-1]['value'], 'minStepSeconds': min(steps), 'maxStepSeconds': max(steps)},
    'limits': ['One native nominal shutdown for this title/package, not all Fenix aircraft.',
               'Prompt/user action times are not independently timestamped cockpit commands. Raw transitions occur later.',
               'No startup/restart/relight, injected failure, unsupported-signal or reconnect validation.',
               'No Sierra ON/IN/PIREP state is recorded. No measured improvement in PIREP eligibility.',
               'Separate 1 Hz fields; timestamps are receipts, not atomic cockpit command timestamps.'],
}
(root / 'analysis.json').write_text(json.dumps(analysis, indent=2), encoding='utf-8')
print(json.dumps({key: analysis[key] for key in ('providedHashesMatch','kindCounts','elapsedDurationSeconds','signalObservations','aircraftTitles','engines','independentMasterIntervalSeconds','extraFuelFlowWaitSeconds')}, indent=2))
