"""Acceptance evidence, never application runtime. Historical results stay immutable."""
import json
import pathlib
import sys
import math

REQUIRED = ('input', 'map', 'telemetry', 'integrity', 'restoration', 'liveness', 'dashboard')
REQUIRED_SUITES = ('direct','operations','presentation','delivery','restoration','interaction-control','scheduling','terminal-recovery','regression','replay')

def handoff_within_existing_control(milliseconds):
    """Existing MapV2Checks.Delivery 250 ms guard within 750 ms Render pressure.

    Diagnostic regression discriminator, not a new universal native/pixel SLA.
    Calibration on archived traces tests the queue-delay component only.
    """
    return isinstance(milliseconds,(int,float)) and math.isfinite(milliseconds) and 0<=milliseconds<250

def evaluate(evidence):
    failures=[]
    contracts=evidence.get('contracts',{})
    for name in REQUIRED:
        c=contracts.get(name,{})
        if c.get('passed') is not True or not isinstance(c.get('checks'),int) or c['checks']<=0 or not c.get('boundary'):
            failures.append(name+': failed or incomplete direct evidence')
    for name in ('knownBadDetected','knownGoodAccepted'):
        if evidence.get(name) is not True: failures.append(name+': missing calibration')
    for name in REQUIRED_SUITES:
        suite=evidence.get('suites',{}).get(name,{})
        if suite.get('passed') is not True or type(suite.get('checks')) is not int or suite['checks']<=0:
            failures.append(name+': missing/failed/empty required suite')
    heartbeat=evidence.get('heartbeat',{})
    values=[heartbeat.get('baselineMaxMs'),heartbeat.get('stressedMaxMs')]
    if not all(isinstance(v,(int,float)) and math.isfinite(v) and v>=0 for v in values):
        failures.append('heartbeat: missing/invalid diagnostic measurement')
    elif heartbeat.get('passed') != (values[1]<500 and values[1]<=max(150,values[0]+100)):
        failures.append('heartbeat: recorded historical result is inconsistent')
    if heartbeat.get('thresholdsChanged') is not False or heartbeat.get('role')!='diagnostic':
        failures.append('heartbeat: role or thresholds do not match approved policy')
    return {'passed':not failures,'failures':failures,'contracts':contracts,'suites':evidence.get('suites',{}),'heartbeat':heartbeat,
            'policy':'7.24C direct responsiveness; heartbeat diagnostic retained',
            'physicalCertification':False}

if __name__=='__main__':
    result=evaluate(json.loads(pathlib.Path(sys.argv[1]).read_text(encoding='utf-8-sig')))
    pathlib.Path(sys.argv[2]).write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result,indent=2))
    sys.exit(0 if result['passed'] else 1)
