import importlib.util
import pathlib
import struct
import unittest

spec = importlib.util.spec_from_file_location('capture', pathlib.Path(__file__).parents[1] / 'scripts/capture_fenix_shutdown.py')
capture = importlib.util.module_from_spec(spec)
spec.loader.exec_module(capture)


def packet(value=1.0, request=101, definition=101, count=1):
    return struct.pack('<10Id', 48, 0, 8, request, 0, definition, 0, 0, 1, count, value)


class CaptureTests(unittest.TestCase):
    fields = {101: ('L:S_ENG_MASTER_1', 'number', False)}

    def test_raw_zero_is_candidate_data_not_shutdown_authority(self):
        result = capture.decode(packet(0), self.fields)
        self.assertEqual(result, {'kind': 'signal', 'name': 'L:S_ENG_MASTER_1', 'value': 0.0, 'finite': True, 'request': 101})

    def test_preserves_non_boolean_signal_for_review(self):
        self.assertEqual(capture.decode(packet(0.25), self.fields)['value'], 0.25)

    def test_nonfinite_is_invalid_not_off(self):
        result = capture.decode(packet(float('nan')), self.fields)
        self.assertIsNone(result['value'])
        self.assertFalse(result['finite'])

    def test_truncated_packet_is_rejected(self):
        with self.assertRaises(ValueError): capture.decode(packet()[:-1], self.fields)

    def test_wrong_definition_or_count_is_rejected(self):
        for data in (packet(definition=102), packet(count=2)):
            with self.subTest(data=data), self.assertRaises(ValueError): capture.decode(data, self.fields)

    def test_unknown_request_is_not_attributed_to_an_engine(self):
        self.assertEqual(capture.decode(packet(request=109, definition=109), self.fields)['kind'], 'unknown-data')

    def test_exception_remains_unavailable_not_zero(self):
        data = struct.pack('<6I', 24, 0, 1, 7, 33, 1)
        self.assertEqual(capture.decode(data, self.fields), {'kind': 'exception', 'code': 7, 'sendId': 33, 'index': 1})

    def test_title_and_pause_are_separate_from_engine_evidence(self):
        data = struct.pack('<10I', 296, 0, 8, 102, 0, 102, 0, 0, 1, 1) + b'Fenix A321 IAE'.ljust(256, b'\0')
        self.assertEqual(capture.decode(data, {102: ('TITLE', None, True)})['value'], 'Fenix A321 IAE')
        pause = struct.pack('<6I', 24, 0, 4, 0, 900, 4)
        self.assertEqual(capture.decode(pause, self.fields), {'kind': 'pause', 'flags': 4})


if __name__ == '__main__': unittest.main()
