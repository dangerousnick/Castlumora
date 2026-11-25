"""Verify native repeated-audio detection only with the marked disposable fixture.

Requires CASTLUMORA_TEST_SERVER/USER/PASSWORD, the three synthetic episodes,
and the isolated server configured as documented. No credentials are printed.
"""
import argparse
import json
import os
from pathlib import Path
import time
import urllib.parse
import urllib.request

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--fixture-root', type=Path, required=True)
args = parser.parse_args()
root = args.fixture_root.resolve()
server = os.environ['CASTLUMORA_TEST_SERVER'].rstrip('/')
if urllib.parse.urlparse(server).hostname not in ('127.0.0.1', 'localhost', '::1') or not (root / '.castlumora-disposable').is_file():
    raise SystemExit('A marked disposable loopback fixture is required.')
header = 'MediaBrowser Client="Castlumora fixture", Device="Disposable test", DeviceId="castlumora-skipper-fixture", Version="1.0"'
token = None


def request(path, data=None, method=None):
    authorization = header + (f', Token="{token}"' if token else '')
    req = urllib.request.Request(server + path, data=json.dumps(data).encode() if data is not None else None,
                                 headers={'Content-Type': 'application/json', 'Authorization': authorization}, method=method)
    with urllib.request.urlopen(req, timeout=120) as response:
        body = response.read()
        return json.loads(body) if body else None


auth = request('/Users/AuthenticateByName', {'Username': os.environ['CASTLUMORA_TEST_USER'], 'Pw': os.environ['CASTLUMORA_TEST_PASSWORD']}, 'POST')
token = auth['AccessToken']
folders = request('/Library/VirtualFolders')
if not any(f['Name'] == 'Fixture Movies' and str(root / 'media') in f['Locations'] for f in folders):
    raise SystemExit('The documented disposable movie library must already exist at this fixture root.')
if not any(f['Name'] == 'Fixture Skipper' for f in folders):
    options = {'PathInfos': [{'Path': str(root / 'series')}], 'EnableRealtimeMonitor': False,
               'EnableChapterImageExtraction': False, 'EnableTrickplayImageExtraction': False, 'MetadataSavers': [],
               'TypeOptions': [{'Type': t, 'MetadataFetchers': [], 'ImageFetchers': []} for t in ['Series', 'Season', 'Episode']]}
    request('/Library/VirtualFolders?name=Fixture%20Skipper&collectionType=tvshows&refreshLibrary=true', {'LibraryOptions': options}, 'POST')
for attempt in range(60):
    episodes = request('/Items?' + urllib.parse.urlencode({'UserId': auth['User']['Id'], 'Recursive': 'true', 'IncludeItemTypes': 'Episode', 'Fields': 'Path'}))['Items']
    episodes = [e for e in episodes if str(root / 'series') + '/' in e.get('Path', '') and e['Name'].startswith('Synthetic Skipper Episode')]
    if len(episodes) == 3 and all(e.get('RunTimeTicks', 0) > 1700000000 for e in episodes):
        break
    time.sleep(1)
else:
    raise SystemExit('Synthetic episodes were not indexed with duration.')
identity = None
for episode in episodes:
    state = request('/Castlumora/Skipper/Items/' + episode['Id'])
    if state['revision'] == 0:
        identity = episode['Id']
        break
if identity is None:
    raise SystemExit('All fixture episodes already have review data. Use a fresh disposable configuration; existing reviews were not changed.')
original = request('/Castlumora/Skipper/Settings')
try:
    request('/Castlumora/Skipper/Settings', {'analysisEnabled': True, 'defaultMode': 'manual'}, 'PUT')
    state = request('/Castlumora/Skipper/Items/' + identity + '/Analyze', {}, 'POST')
    assert {s['type'] for s in state['segments']} == {'Intro', 'Outro'}, state
    assert all(s['status'] == 'suggested' for s in state['segments'])
    assert not request('/MediaSegments/' + identity)['Items'], 'Unreviewed intervals must not be published'
    state['protectedFromTicks'] = 1700000000
    for segment in state['segments']:
        segment['status'] = 'approved'
        if segment['type'] == 'Outro':
            segment['endTicks'] = min(segment['endTicks'], state['protectedFromTicks'])
    request('/Castlumora/Skipper/Items/' + identity, state, 'PUT')
    approved = request('/MediaSegments/' + identity)['Items']
    assert len(approved) == 2
    assert all(s['EndTicks'] <= 1700000000 for s in approved)
    print(json.dumps({'episodeId': identity, 'segments': approved}))
    print('PASS real repeated-audio detection, review boundary and central approved intervals. Use episodeId for CASTLUMORA_TEST_EPISODE.')
finally:
    request('/Castlumora/Skipper/Settings', original, 'PUT')
