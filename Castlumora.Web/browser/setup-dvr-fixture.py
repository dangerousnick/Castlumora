"""Prepare short DVR lifecycle programmes only on the marked disposable loopback fixture."""
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
header = 'MediaBrowser Client="Castlumora fixture", Device="Disposable test", DeviceId="castlumora-dvr-fixture", Version="1.0"'
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

from datetime import datetime, timedelta, timezone
import xml.etree.ElementTree as ET
state_file = root / 'dvr-fixture-state.json'
if state_file.exists():
    raise SystemExit('An earlier DVR fixture must be cleaned up before creating another.')
config = request('/System/Configuration/livetv')
if not config.get('ListingProviders') or any(source.get('Path') != str(root / 'guide.xml') for source in config['ListingProviders']):
    raise SystemExit('DVR fixture requires only its own local XMLTV guide.')
if any(source.get('Url') != str(root / 'channels.m3u') for source in config.get('TunerHosts', [])):
    raise SystemExit('DVR fixture requires only its own synthetic M3U source.')
active = [timer for timer in request('/LiveTv/Timers')['Items'] if timer.get('Status') in ['New', 'InProgress']]
if active:
    raise SystemExit('Finish/cancel existing synthetic schedules before changing fixture recording options.')
folder = root / 'dvr-lifecycle'
folder.mkdir(exist_ok=True)
original_guide = (root / 'guide.xml').read_text()
guide = ET.fromstring(original_guide)
channel = guide.find('channel').attrib['id']
now = datetime.now(timezone.utc)
stamp = now.strftime('%Y%m%d%H%M%S')
programs = []
for kind, delay, length in [('Complete', 90, 25), ('Cancel', 150, 45)]:
    name = 'Synthetic DVR ' + kind + ' ' + stamp
    start = now + timedelta(seconds=delay)
    end = start + timedelta(seconds=length)
    program = ET.SubElement(guide, 'programme', channel=channel, start=start.strftime('%Y%m%d%H%M%S +0000'), stop=end.strftime('%Y%m%d%H%M%S +0000'))
    ET.SubElement(program, 'title').text = name
    ET.SubElement(program, 'desc').text = 'Owned synthetic DVR lifecycle fixture, not external content.'
    ET.SubElement(program, 'category').text = 'Movie'
    programs.append({'kind': kind, 'name': name, 'start': start.isoformat(), 'end': end.isoformat()})
state = {'originalConfig': config, 'originalGuide': original_guide, 'recordingFolder': str(folder), 'programs': programs, 'userId': auth['User']['Id'], 'originalMovie': str(root / 'media' / 'Castlumora Test (2026).mp4')}
state['originalPolicy'] = auth['User']['Policy']
state_file.write_text(json.dumps(state));state_file.chmod(0o600)
request('/Users/' + auth['User']['Id'] + '/Policy', dict(auth['User']['Policy'], EnableContentDeletion=True), 'POST')
changed = dict(config, TunerHosts=[dict(tuner, ReadAtNativeFramerate=True) for tuner in config['TunerHosts']], RecordingPath=str(folder), MovieRecordingPath=str(folder), SeriesRecordingPath=str(folder), PrePaddingSeconds=0, PostPaddingSeconds=0, SaveRecordingImages=False)
state['expectedTunerHosts'] = changed['TunerHosts']
state_file.write_text(json.dumps(state))
request('/System/Configuration/livetv', changed, 'POST')
# Ensure the newly registered recording root is indexed before scheduling.
request('/Library/Refresh', {}, 'POST')
(root / 'guide.xml').write_text(ET.tostring(guide, encoding='unicode'))
# Re-saving this owned local provider is the supported explicit cache invalidation.
for provider in changed['ListingProviders']:
    request('/LiveTv/ListingProviders?validateListings=true', provider, 'POST')
task = next(item for item in request('/ScheduledTasks') if 'RefreshGuide' in item['Key'])
if task['State'] == 'Idle': request('/ScheduledTasks/Running/' + task['Id'], {}, 'POST')
for attempt in range(60):
    found = request('/LiveTv/Programs?' + urllib.parse.urlencode({'UserId': auth['User']['Id'], 'MinEndDate': now.isoformat(), 'MaxStartDate': (now + timedelta(minutes=10)).isoformat()}))['Items']
    selected = [item for item in found if any(item['Name'] == program['name'] for program in programs)]
    if len(selected) == 2:
        state['programIds'] = {item['Name']: item['Id'] for item in selected}
        state_file.write_text(json.dumps(state))
        print('Short synthetic DVR programmes are indexed. Run dvr-server.spec.ts immediately.')
        break
    time.sleep(1)
else:
    raise SystemExit('DVR guide did not finish indexing. Original guide/options retained in fixture state for recovery.')
