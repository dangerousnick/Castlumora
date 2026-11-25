"""Generate two synthetic tagged MP3 tracks on a marked disposable loopback server."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import time
import urllib.parse
import urllib.request

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--fixture-root', type=Path, required=True)
args = parser.parse_args()
root = args.fixture_root.resolve()
server = os.environ['CASTLUMORA_TEST_SERVER'].rstrip('/')
if urllib.parse.urlparse(server).hostname not in ('127.0.0.1', 'localhost', '::1') or not (root / '.castlumora-disposable').is_file():
    raise SystemExit('An explicitly marked disposable loopback server is required.')
header = 'MediaBrowser Client="Castlumora fixture", Device="Disposable test", DeviceId="castlumora-music-fixture", Version="1.0"'
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
if request('/System/Info')['ProgramDataPath'] != str(root / 'data'):
    raise SystemExit('Server data path does not match the disposable fixture.')
album = root / 'music' / 'Castle Artist' / 'Castle Album'
album.mkdir(parents=True, exist_ok=True)
for number, title in [(1, 'Violet Overture'), (2, 'Moonlit Reprise')]:
    target = album / f'{number:02} {title}.mp3'
    if not target.exists():
        subprocess.run(['ffmpeg', '-nostdin', '-hide_banner', '-v', 'error', '-f', 'lavfi', '-i',
                        f'sine=frequency={440 + number * 110}:duration=30', '-c:a', 'libmp3lame', '-b:a', '128k',
                        '-metadata', 'title=' + title, '-metadata', 'artist=Castle Artist', '-metadata', 'album_artist=Castle Artist',
                        '-metadata', 'album=Castle Album', '-metadata', 'date=2026', '-metadata', 'track=' + str(number), str(target)], check=True)
folders = request('/Library/VirtualFolders')
existing = next((folder for folder in folders if folder['Name'] == 'Fixture Music'), None)
if existing and str(root / 'music') not in existing['Locations']:
    raise SystemExit('An existing music fixture points elsewhere; it was not modified.')
if not existing:
    options = {'PathInfos': [{'Path': str(root / 'music')}], 'EnableRealtimeMonitor': False, 'EnableLUFSScan': False,
               'TypeOptions': [{'Type': kind, 'MetadataFetchers': [], 'ImageFetchers': []} for kind in ['MusicArtist', 'MusicAlbum', 'Audio']]}
    request('/Library/VirtualFolders?name=Fixture%20Music&collectionType=music&refreshLibrary=true', {'LibraryOptions': options}, 'POST')
for attempt in range(60):
    tracks = request('/Items?' + urllib.parse.urlencode({'UserId': auth['User']['Id'], 'Recursive': 'true', 'IncludeItemTypes': 'Audio', 'Fields': 'Path'}))['Items']
    tracks = [track for track in tracks if str(root / 'music') + '/' in track.get('Path', '')]
    if len(tracks) == 2 and all(track.get('RunTimeTicks', 0) > 200000000 for track in tracks):
        print('Synthetic MP3 album/artist/tracks are indexed. Run native music browser tests with CASTLUMORA_TEST_MUSIC=1.')
        break
    time.sleep(1)
else:
    raise SystemExit('Synthetic music did not finish indexing.')
