"""Configure synthetic M3U/XMLTV only on an explicitly marked disposable test server.

Use the same CASTLUMORA_TEST_* credentials as real-server.spec.ts. Never production.
Serve only <fixture-root>/media on loopback port 18097 before running playback tests.
"""
import argparse
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import urllib.parse
import urllib.request

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--fixture-root', type=Path, required=True)
args = parser.parse_args()
root = args.fixture_root.resolve()
server = os.environ['CASTLUMORA_TEST_SERVER'].rstrip('/')
if urllib.parse.urlparse(server).hostname not in ('127.0.0.1', 'localhost', '::1'):
    raise SystemExit('Only an isolated loopback server is supported.')
if not (root / '.castlumora-disposable').is_file():
    raise SystemExit('Missing explicit .castlumora-disposable marker in fixture root.')
movie = root / 'media' / 'Castlumora Test (2026).mp4'
if not movie.is_file():
    raise SystemExit('Generate the documented synthetic movie fixture first.')
header = 'MediaBrowser Client="Castlumora fixture", Device="Disposable test", DeviceId="castlumora-live-fixture", Version="1.0"'
token = None


def request(path, data=None, method=None):
    authorization = header + (f', Token="{token}"' if token else '')
    req = urllib.request.Request(server + path, data=json.dumps(data).encode() if data is not None else None,
                                 headers={'Content-Type': 'application/json', 'Authorization': authorization}, method=method)
    with urllib.request.urlopen(req, timeout=30) as response:
        body = response.read()
        return json.loads(body) if body else None


auth = request('/Users/AuthenticateByName', {'Username': os.environ['CASTLUMORA_TEST_USER'], 'Pw': os.environ['CASTLUMORA_TEST_PASSWORD']}, 'POST')
token = auth['AccessToken']
config = request('/System/Configuration/livetv')
if config.get('TunerHosts') or config.get('ListingProviders'):
    raise SystemExit('Fixture setup requires an empty tuner/provider configuration; existing sources were not modified.')
lines = ['#EXTM3U']
xml = ['<?xml version="1.0"?><tv>']
now = datetime.now(timezone.utc)
for channel in (1, 2):
    identity = f'castle-{channel}'
    name = 'Castle One' if channel == 1 else 'Castle Two'
    lines += [f'#EXTINF:-1 tvg-id="{identity}" tvg-name="{name}" group-title="Synthetic",{name}',
              f'http://127.0.0.1:18097/{urllib.parse.quote(movie.name)}?channel={channel}']
    xml += [f'<channel id="{identity}"><display-name>{name}</display-name></channel>']
    for episode in range(6):
        start = now - timedelta(minutes=10) + timedelta(minutes=episode * 30)
        stop = start + timedelta(minutes=30)
        xml += [f'<programme start="{start:%Y%m%d%H%M%S} +0000" stop="{stop:%Y%m%d%H%M%S} +0000" channel="{identity}"><title>Synthetic Castle Show {episode}</title><desc>Disposable local guide fixture.</desc><category>Series</category><episode-num system="xmltv_ns">0.{episode}.0</episode-num></programme>']
xml += ['</tv>']
(root / 'channels.m3u').write_text('\n'.join(lines) + '\n')
(root / 'guide.xml').write_text(''.join(xml))
request('/LiveTv/TunerHosts', {'Type': 'm3u', 'Url': str(root / 'channels.m3u'), 'FriendlyName': 'Synthetic Castle', 'EnableStreamLooping': True, 'AllowStreamSharing': True}, 'POST')
request('/LiveTv/ListingProviders?validateListings=true', {'Type': 'xmltv', 'Path': str(root / 'guide.xml'), 'EnableAllTuners': True}, 'POST')
for task in request('/ScheduledTasks'):
    if 'RefreshGuide' in task.get('Key', '') and task['State'] != 'Running':
        request('/ScheduledTasks/Running/' + task['Id'], {}, 'POST')
print('Synthetic guide configured on the marked disposable loopback server. Wait for Refresh Guide to finish.')
