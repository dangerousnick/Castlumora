"""Protect synthetic Live TV setup from pointing at an unrelated loopback server."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

SCRIPT = Path(__file__).with_name('setup-live-fixture.py')


class LiveFixtureGuardTests(unittest.TestCase):
    def test_missing_marker_rejects_before_connecting(self):
        with tempfile.TemporaryDirectory() as temp:
            result = self.run_setup(Path(temp), 'http://127.0.0.1:1')
            self.assertNotEqual(0, result.returncode)
            self.assertIn('Missing explicit .castlumora-disposable marker', result.stderr)

    def test_remote_address_rejects_before_connecting(self):
        with tempfile.TemporaryDirectory() as temp:
            result = self.run_setup(Path(temp), 'https://example.invalid')
            self.assertNotEqual(0, result.returncode)
            self.assertIn('Only an isolated loopback server', result.stderr)

    def test_mismatched_actual_data_root_prevents_configuration_access(self):
        self.check_server(False)

    def test_matching_data_root_reaches_existing_source_protection(self):
        self.check_server(True)

    @staticmethod
    def run_setup(root, server):
        environment = dict(os.environ, CASTLUMORA_TEST_SERVER=server,
                           CASTLUMORA_TEST_USER='Owned fixture',
                           CASTLUMORA_TEST_PASSWORD='Synthetic test password')
        return subprocess.run([sys.executable, str(SCRIPT), '--fixture-root', str(root)],
                              env=environment, capture_output=True, text=True, timeout=10)

    def check_server(self, matches):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            (root / '.castlumora-disposable').touch()
            (root / 'media').mkdir()
            (root / 'media' / 'Castlumora Test (2026).mp4').write_bytes(b'Owned fixture marker')
            requests = []

            class Handler(BaseHTTPRequestHandler):
                def log_message(self, *_args):
                    pass

                def reply(self, value):
                    body = json.dumps(value).encode()
                    self.send_response(200)
                    self.send_header('Content-Type', 'application/json')
                    self.send_header('Content-Length', str(len(body)))
                    self.end_headers()
                    self.wfile.write(body)

                def do_POST(self):
                    requests.append(('POST', self.path))
                    self.rfile.read(int(self.headers.get('Content-Length', '0')))
                    self.reply({'AccessToken': 'Synthetic-fixture-token'})

                def do_GET(self):
                    requests.append(('GET', self.path))
                    if self.path == '/System/Info':
                        self.reply({'ProgramDataPath': str(root / 'data') if matches else '/unrelated/server/data'})
                    else:
                        self.reply({'TunerHosts': [{'Url': 'Existing source must be preserved'}]})

            server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            try:
                result = self.run_setup(root, 'http://127.0.0.1:' + str(server.server_port))
            finally:
                server.shutdown()
                server.server_close()
                thread.join(timeout=2)
            self.assertNotEqual(0, result.returncode)
            self.assertFalse((root / 'guide.xml').exists())
            self.assertFalse((root / 'channels.m3u').exists())
            expected = [('POST', '/Users/AuthenticateByName'), ('GET', '/System/Info')]
            if matches:
                expected.append(('GET', '/System/Configuration/livetv'))
                self.assertIn('existing sources were not modified', result.stderr)
            else:
                self.assertIn('Actual server data root differs', result.stderr)
            self.assertEqual(expected, requests)


if __name__ == '__main__':
    unittest.main()
