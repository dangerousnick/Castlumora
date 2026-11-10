"""Verify retained creator metadata and locks only on an explicitly owned loopback fixture."""
import argparse
import json
import os
from pathlib import Path
import shutil
import time
import urllib.error
import urllib.parse
import urllib.request

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--fixture-root', type=Path, required=True)
parser.add_argument('--resume', action='store_true', help='Resume only a previously marked owned creator fixture.')
args = parser.parse_args()
root = args.fixture_root.resolve()
server = os.environ['CASTLUMORA_TEST_SERVER'].rstrip('/')
if urllib.parse.urlparse(server).hostname != '127.0.0.1' or not (root / '.castlumora-disposable').is_file():
    raise SystemExit('Marked disposable loopback fixture required.')
header = 'MediaBrowser Client="Creator metadata verification", Device="Disposable", DeviceId="creator-sidecar-fixture", Version="1"'
token = None


def request(path, data=None, method=None):
    headers = {'Content-Type': 'application/json', 'Authorization': header + (f', Token="{token}"' if token else '')}
    req = urllib.request.Request(server + path, data=json.dumps(data).encode() if data is not None else None, headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=30) as response:
        body = response.read()
        return json.loads(body) if body else None


auth = request('/Users/AuthenticateByName', {'Username': os.environ['CASTLUMORA_TEST_USER'], 'Pw': os.environ['CASTLUMORA_TEST_PASSWORD']}, 'POST')
token = auth['AccessToken']
if request('/System/Info')['ProgramDataPath'] != str(root / 'data'):
    raise SystemExit('Actual data path differs; no fixture changes made.')
folder = root / 'creator-sidecar'
marker = folder / '.castlumora-creator-fixture'
if folder.exists() and not (args.resume and marker.is_file()):
    raise SystemExit('Existing creator fixture requires its ownership marker and explicit --resume.')
folder.mkdir(exist_ok=True)
marker.touch()
source = root / 'media' / 'Castlumora Test (2026).mp4'
media = folder / 'Unspeakable [AbCdEf123_-].mp4'
if not media.exists():
    shutil.copyfile(source, media)
options = {'PathInfos': [{'Path': str(folder)}], 'EnableRealtimeMonitor': False, 'EnableInternetProviders': False,
           'MetadataSavers': [], 'TypeOptions': [{'Type': 'Video', 'MetadataFetchers': [], 'ImageFetchers': []}]}
existing = next((f for f in request('/Library/VirtualFolders') if f['Name'] == 'Fixture Creator Sidecar'), None)
if existing and existing['Locations'] != [str(folder)]:
    raise SystemExit('Existing library points elsewhere; stopped.')
if not existing:
    request('/Library/VirtualFolders?name=Fixture%20Creator%20Sidecar&collectionType=homevideos&refreshLibrary=true', {'LibraryOptions': options}, 'POST')


def wait_for(get, accept, message):
    for _ in range(90):
        value = get()
        if accept(value):
            return value
        time.sleep(1)
    raise AssertionError(message)


def items():
    return request('/Items?' + urllib.parse.urlencode({'UserId': auth['User']['Id'], 'Recursive': 'true', 'Fields': 'Path,ProviderIds,Overview,Settings'}))['Items']


item = wait_for(items, lambda values: any(v.get('Path') == str(media) and v.get('RunTimeTicks', 0) > 0 for v in values), 'Existing synthetic video was not indexed.')
item = next(v for v in item if v.get('Path') == str(media))
assert item['Type'] == 'Video'
if not args.resume:
    assert not item.get('ProviderIds', {}).get('YouTube')
item_id = item['Id']


def get_item():
    return request('/Users/' + auth['User']['Id'] + '/Items/' + item_id + '?Fields=Path,ProviderIds,Overview,Settings')


def refresh():
    request('/Items/' + item_id + '/Refresh?metadataRefreshMode=FullRefresh&imageRefreshMode=None&replaceAllMetadata=true&replaceAllImages=false', {}, 'POST')


sidecar = media.with_suffix('.info.json')
metadata = {'id': 'AbCdEf123_-', 'extractor_key': 'Youtube', 'webpage_url': 'https://www.youtube.com/watch?v=AbCdEf123_-',
            'title': 'Synthetic creator: Unspeakable', 'description': 'Owned fixture <script>must not execute</script>',
            'channel_id': 'UC' + 'a' * 22, 'channel': 'Synthetic Castle Creator', 'upload_date': '20240312',
            'thumbnail': 'http://127.0.0.1:1/must-not-be-requested', '_filename': '../../must-not-be-used'}
sidecar.write_text(json.dumps(metadata))
refresh()
item = wait_for(get_item, lambda value: value.get('ProviderIds', {}).get('YouTube') == metadata['id'] and value['Name'] == metadata['title'], 'Retained sidecar was not applied to the existing file.')
assert item['Type'] == 'Video' and item['Name'] == metadata['title']
assert item['ProviderIds']['YouTubeChannel'] == metadata['channel_id']
assert '&lt;script&gt;' in item['Overview'] and item['Path'] == str(media)
assert abs(item['RunTimeTicks'] / 10000000 - 90) < 2

# Use the inherited metadata editor/lock API, then change retained source metadata.
item.update(Name='Administrator corrected creator title', LockData=True)
request('/Items/' + item_id, item, 'POST')
metadata['title'] = 'Source changed after manual lock'
sidecar.write_text(json.dumps(metadata))
refresh()
time.sleep(3)
locked = get_item()
assert locked['Name'] == 'Administrator corrected creator title' and locked['LockData']
locked.update(LockData=False)
request('/Items/' + item_id, locked, 'POST')
refresh()
item = wait_for(get_item, lambda value: value['Name'] == metadata['title'], 'Unlocked metadata did not refresh.')
# Per-field correction remains locked while other source fields refresh.
item.update(Name='Field-locked creator title', LockData=False, LockedFields=['Name'])
request('/Items/' + item_id, item, 'POST')
metadata.update(title='Final synthetic creator title', description='Refreshed description with a locked title')
sidecar.write_text(json.dumps(metadata))
refresh()
partial = wait_for(get_item, lambda value: value.get('Overview') == metadata['description'], 'Unlocked description did not refresh.')
assert partial['Name'] == 'Field-locked creator title'
partial.update(LockedFields=[])
request('/Items/' + item_id, partial, 'POST')
metadata['description'] = 'Owned fixture <script>must not execute</script>'
sidecar.write_text(json.dumps(metadata))
refresh()
wait_for(get_item, lambda value: value['Name'] == metadata['title'] and '&lt;script&gt;' in value.get('Overview', ''), 'Removed field lock did not allow repair.')

# A conflicting stable filename identifier must not be replaced by sidecar identity.
conflict = folder / 'MrBeast [ZYXWV987654].mp4'
shutil.copyfile(source, conflict)
conflict.with_suffix('.info.json').write_text(json.dumps(metadata))
request('/Library/Refresh', {}, 'POST')
conflicts = wait_for(items, lambda values: any(v.get('Path') == str(conflict) and v.get('RunTimeTicks', 0) > 0 for v in values), 'Conflicting fixture did not scan.')
other = next(v for v in conflicts if v.get('Path') == str(conflict))
assert other['Type'] == 'Video' and not other.get('ProviderIds', {}).get('YouTube')
assert other['Name'] != metadata['title']
assert source.exists() and media.exists()
(root / 'creator-sidecar-result.json').write_text(json.dumps({'itemId': item_id, 'path': str(media), 'conflictingItemId': other['Id'],
    'existingFileIdentification': True, 'manualLock': True, 'fieldLock': True, 'unlockRefresh': True, 'conflictRejected': True, 'networkLookupRequired': False}))
print('PASS: existing-file source identity, generic type, local refresh, manual lock/unlock and conflicting filename; original synthetic media retained.')
