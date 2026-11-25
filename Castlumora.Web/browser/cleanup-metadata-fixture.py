"""Recover only a marked loopback metadata-administration fixture after interruption."""
import argparse
import json
import os
from pathlib import Path
import urllib.parse
import urllib.request

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--fixture-root', type=Path, required=True)
root = parser.parse_args().fixture_root.resolve()
server = os.environ['CASTLUMORA_TEST_SERVER'].rstrip('/')
if urllib.parse.urlparse(server).hostname != '127.0.0.1' or not (root / '.castlumora-disposable').is_file():
    raise SystemExit('Marked owned loopback fixture required.')
state_path = root / 'metadata-admin-state.json'
state = json.loads(state_path.read_text())
if state['library']['Locations'] != [str(root / 'creator-sidecar')] or not state['original']['Path'].startswith(str(root / 'creator-sidecar') + '/'):
    raise SystemExit('Saved metadata does not belong to the creator fixture.')
header = 'MediaBrowser Client="Metadata recovery", Device="Disposable", DeviceId="metadata-recovery", Version="1"'
token = None


def request(path, data=None, method=None):
    headers = {'Content-Type': 'application/json', 'Authorization': header + (f', Token="{token}"' if token else '')}
    with urllib.request.urlopen(urllib.request.Request(server + path, data=json.dumps(data).encode() if data is not None else None, headers=headers, method=method), timeout=30) as response:
        body = response.read()
        return json.loads(body) if body else None


auth = request('/Users/AuthenticateByName', {'Username': os.environ['CASTLUMORA_TEST_USER'], 'Pw': os.environ['CASTLUMORA_TEST_PASSWORD']}, 'POST')
token = auth['AccessToken']
if request('/System/Info')['ProgramDataPath'] != str(root / 'data'):
    raise SystemExit('Actual server data path differs.')
library = next(v for v in request('/Library/VirtualFolders') if v['ItemId'] == state['library']['ItemId'])
if library['Locations'] != state['library']['Locations']:
    raise SystemExit('Library moved; no changes made.')
request('/Library/VirtualFolders/LibraryOptions', {'Id': library['ItemId'], 'LibraryOptions': state['library']['LibraryOptions']}, 'POST')
request('/Items/' + state['original']['Id'], state['original'], 'POST')
if state.get('viewerId'):
    viewer = request('/Users/' + state['viewerId'])
    if not viewer['Name'].startswith('SyntheticMetadataViewer'):
        raise SystemExit('Saved viewer identity is not the synthetic viewer; not deleted.')
    request('/Users/' + state['viewerId'], method='DELETE')
state_path.rename(root / 'metadata-admin-last-result.json')
print('Restored only owned creator library options/metadata and removed its synthetic viewer, if present.')
