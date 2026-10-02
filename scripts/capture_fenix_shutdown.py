"""Read-only parked Fenix signal capture. No Sierra state and no simulator writes.

Raw candidate observations are not a shutdown-capability declaration. A zero LVar
can mean unavailable; correlate controlled cockpit transitions before using it.
"""
import argparse
import ctypes
import datetime
import hashlib
import json
import math
import pathlib
import struct
import time


def decode(data, fields):
    if len(data) < 12:
        raise ValueError('Truncated SimConnect header')
    size, _, kind = struct.unpack_from('<3I', data)
    if size > len(data) or size < 12:
        raise ValueError('Invalid SimConnect packet size')
    data = data[:size]
    if kind == 8:
        if size < 40:
            raise ValueError('Truncated SimConnect data header')
        request, _, definition, _, _, _, count = struct.unpack_from('<7I', data, 12)
        if request not in fields:
            return {'kind': 'unknown-data', 'request': request}
        name, _, text = fields[request]
        if definition != request or count != 1 or size != (296 if text else 48):
            raise ValueError('Mismatched SimConnect data definition/count/size')
        value = data[40:296].split(b'\0', 1)[0].decode('utf-8', errors='replace') if text else struct.unpack_from('<d', data, 40)[0]
        finite = text or math.isfinite(value)
        return {'kind': 'signal', 'name': name, 'value': value if finite else None, 'finite': finite, 'request': request}
    if kind == 1:
        if size < 24:
            raise ValueError('Truncated exception')
        code, sent, index = struct.unpack_from('<3I', data, 12)
        return {'kind': 'exception', 'code': code, 'sendId': sent, 'index': index}
    if kind == 4 and size >= 24 and struct.unpack_from('<I', data, 16)[0] == 900:
        return {'kind': 'pause', 'flags': struct.unpack_from('<I', data, 20)[0]}
    return {'kind': {2: 'opened', 3: 'quit'}.get(kind, 'other'), 'receiveId': kind}


def signal_definitions():
    fields = [('ABSOLUTE TIME', 'seconds', False), ('SIM ON GROUND', 'Bool', False),
              ('GROUND VELOCITY', 'knots', False), ('BRAKE PARKING POSITION', 'Bool', False),
              ('IS SLEW ACTIVE', 'Bool', False), ('NUMBER OF ENGINES', 'number', False),
              ('PLANE LATITUDE', 'degrees', False), ('PLANE LONGITUDE', 'degrees', False),
              ('TITLE', None, True)]
    for engine in (1, 2):
        fields.append((f'L:S_ENG_MASTER_{engine}', 'number', False))
        for name, unit in [('GENERAL ENG FUEL VALVE', 'Bool'), ('GENERAL ENG COMBUSTION', 'Bool'),
                           ('TURB ENG FUEL FLOW PPH', 'pounds per hour'), ('TURB ENG N1', 'percent'),
                           ('TURB ENG N2', 'percent'), ('GENERAL ENG STARTER', 'Bool'),
                           ('GENERAL ENG OIL PRESSURE', 'psf'), ('GENERAL ENG FAILED', 'Bool')]:
            fields.append((f'{name}:{engine}', unit, False))
    return {101 + i: value for i, value in enumerate(fields)}


def capture(dll_path, directory, seconds, package_manifests=()):
    # New directory only. Never reads or writes the Sierra profile/history.
    directory.mkdir(parents=True, exist_ok=False)
    fields = signal_definitions()
    packages = []
    for path in package_manifests:
        try:
            content = path.read_bytes()
            packages.append({'path': str(path.resolve()), 'sha256': hashlib.sha256(content).hexdigest(),
                             'manifest': json.loads(content.decode('utf-8-sig'))})
        except (OSError, ValueError) as error:
            packages.append({'path': str(path), 'unavailable': str(error)})
    metadata = {'schema': 1, 'purpose': 'Unvalidated raw Fenix shutdown-intent candidates; never completion authority',
                'startedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
                'seconds': seconds, 'simconnect': str(dll_path.resolve()),
                'simconnectSha256': hashlib.sha256(dll_path.read_bytes()).hexdigest(),
                'scriptSha256': hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),
                'fields': fields, 'packages': packages, 'period': 'SECOND (1 Hz), separate definitions; preserve each signal arrival time',
                'limitations': 'No native support is inferred from zero or successful registration. Simulator command time lies between samples. No automatic reconnect or simulator writes.'}
    (directory / 'manifest.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    dll = ctypes.WinDLL(str(dll_path.resolve()))
    pointer = ctypes.c_void_p
    uint = ctypes.c_uint32
    def function(name, arguments):
        f = getattr(dll, 'SimConnect_' + name)
        f.argtypes = arguments
        f.restype = ctypes.c_int32
        return f
    open_sim = function('Open', [ctypes.POINTER(pointer), ctypes.c_char_p, pointer, uint, pointer, uint])
    close = function('Close', [pointer])
    add = function('AddToDataDefinition', [pointer, uint, ctypes.c_char_p, ctypes.c_char_p, ctypes.c_int32, ctypes.c_float, uint])
    request = function('RequestDataOnSimObject', [pointer, uint, uint, uint, ctypes.c_int32, uint, uint, uint, uint])
    subscribe = function('SubscribeToSystemEvent', [pointer, uint, ctypes.c_char_p])
    next_packet = function('GetNextDispatch', [pointer, ctypes.POINTER(pointer), ctypes.POINTER(uint)])
    last_sent = function('GetLastSentPacketID', [pointer, ctypes.POINTER(uint)])
    handle = pointer()
    packets = 0
    signals = {}
    errors = 0
    sent = {}
    completed = False
    failure = None
    started = time.monotonic()
    exit_reason = 'interrupted'
    with (directory / 'signals.jsonl').open('x', encoding='utf-8', buffering=1) as journal:
        def write(row):
            row = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'elapsedSeconds': time.monotonic() - started, **row}
            journal.write(json.dumps(row, allow_nan=False) + '\n')
        def operation(code, name, field=None):
            nonlocal errors
            packet_id = uint()
            packet_result = last_sent(handle, ctypes.byref(packet_id)) if code >= 0 else -1
            if packet_result >= 0:
                sent[packet_id.value] = {'operation': name, 'field': field}
            write({'kind': 'registration', 'operation': name, 'field': field, 'hresult': code,
                   'sendId': packet_id.value if packet_result >= 0 else None})
            if code < 0:
                errors += 1
            return code >= 0
        try:
            result = open_sim(ctypes.byref(handle), b'Sierra read-only Fenix capability capture', None, 0, None, 0xFFFFFFFF)
            if result < 0:
                raise RuntimeError(f'SimConnect open failed 0x{result & 0xFFFFFFFF:08X}; load the parked aircraft first')
            operation(subscribe(handle, 900, b'Pause_EX1'), 'subscribe-pause')
            for index, (name, unit, text) in fields.items():
                if operation(add(handle, index, name.encode('ascii'), None if unit is None else unit.encode('ascii'),
                                 9 if text else 4, 0, 0xFFFFFFFF), 'define', name):
                    operation(request(handle, index, index, 0, 4, 0, 0, 0, 0), 'request', name)
            stages = [(0, 'Keep BOTH engines running at idle, brake SET. Observe for 15 seconds.'),
                      (15, 'Set ENGINE 2 MASTER OFF. Leave Engine 1 running. Remain parked.'),
                      (35, 'Set ENGINE 1 MASTER OFF. Leave both masters OFF until capture finishes.'),
                      (125, 'Continue parked with both masters OFF. Capturing the residual spool/fuel-flow tail.')]
            print('CAPTURE STARTED. No simulator controls are written. Follow these timed prompts.', flush=True)
            while time.monotonic() - started < seconds:
                elapsed = time.monotonic() - started
                while stages and elapsed >= stages[0][0]:
                    due, instruction = stages.pop(0)
                    print(f'[{int(due):3}s] {instruction}', flush=True)
                    write({'kind': 'instruction', 'scheduledSeconds': due, 'text': instruction, 'actualActionNotObserved': True})
                for _ in range(100):
                    data, length = pointer(), uint()
                    if next_packet(handle, ctypes.byref(data), ctypes.byref(length)) < 0:
                        break
                    if not data.value or not 12 <= length.value <= 65536:
                        raise ValueError('Invalid native packet pointer/size')
                    row = decode(ctypes.string_at(data, length.value), fields)
                    packets += 1
                    if row['kind'] == 'exception':
                        errors += 1
                        row['registration'] = sent.get(row['sendId'])
                    if row['kind'] == 'signal':
                        name = row['name']
                        signals[name] = signals.get(name, 0) + 1
                    write(row)
                    if row['kind'] == 'quit':
                        raise RuntimeError('Simulator quit; capture stopped without reconnect')
                if elapsed > 15 and not signals:
                    raise RuntimeError('No signal packets received in 15 seconds; capture stopped')
                time.sleep(0.01)
            completed = True
            exit_reason = 'duration-complete'
        except KeyboardInterrupt:
            exit_reason = 'user-interrupted'
        except Exception as error:
            exit_reason = str(error)
            write({'kind': 'capture-error', 'message': exit_reason})
            failure = error
        finally:
            if handle.value:
                close(handle)
            summary = {'completed': completed, 'exitReason': exit_reason, 'packets': packets, 'signals': signals,
                       'errors': errors, 'missingSignals': [f[0] for f in fields.values() if f[0] not in signals],
                       'capability': 'UNVALIDATED — manual cockpit observations and signal transitions must be reviewed'}
            write({'kind': 'capture-end', **summary})
            (directory / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in directory.iterdir() if p.is_file()}
    (directory / 'hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
    if failure is not None:
        raise RuntimeError(f'{exit_reason}. Preserved capture: {directory.resolve()}') from failure
    print(f'Capture finished. Return this entire folder: {directory.resolve()}', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--simconnect', type=pathlib.Path, default=pathlib.Path('M:/SimConnect SDK/lib/SimConnect.dll'))
    parser.add_argument('--output', type=pathlib.Path)
    parser.add_argument('--seconds', type=int, default=180)
    parser.add_argument('--package-manifest', type=pathlib.Path, action='append', default=[])
    parser.add_argument('--describe', action='store_true', help='Print candidate definitions; do not connect or create files')
    args = parser.parse_args()
    if args.describe:
        print(json.dumps(signal_definitions(), indent=2))
        return
    if not 150 <= args.seconds <= 600:
        parser.error('--seconds must be between 150 and 600')
    if args.output is None:
        args.output = pathlib.Path.home() / 'Desktop' / ('S4-Fenix-Shutdown-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
    if not args.simconnect.is_file():
        parser.error('SimConnect DLL not found; provide --simconnect with its existing SDK path')
    capture(args.simconnect, args.output, args.seconds, args.package_manifest)


if __name__ == '__main__':
    main()
