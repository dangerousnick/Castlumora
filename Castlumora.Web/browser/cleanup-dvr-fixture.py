"""Restore only guide/options/policy owned by a marked DVR lifecycle fixture."""
import argparse,json,os
from pathlib import Path
import urllib.parse,urllib.request
parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--fixture-root',type=Path,required=True);args=parser.parse_args();root=args.fixture_root.resolve()
server=os.environ['CASTLUMORA_TEST_SERVER'].rstrip('/')
if urllib.parse.urlparse(server).hostname not in ('127.0.0.1','localhost','::1') or not (root/'.castlumora-disposable').is_file():raise SystemExit('Marked loopback fixture required.')
state_path=root/'dvr-fixture-state.json'
if not state_path.exists():raise SystemExit('No DVR fixture state to restore.')
state=json.loads(state_path.read_text());header='MediaBrowser Client="DVR cleanup", Device="Disposable", DeviceId="dvr-cleanup", Version="1"';token=None
def req(path,data=None,method=None):
 h={'Content-Type':'application/json','Authorization':header+(f', Token="{token}"' if token else '')}
 with urllib.request.urlopen(urllib.request.Request(server+path,data=json.dumps(data).encode() if data is not None else None,headers=h,method=method),timeout=30) as r:
  b=r.read();return json.loads(b) if b else None
a=req('/Users/AuthenticateByName',{'Username':os.environ['CASTLUMORA_TEST_USER'],'Pw':os.environ['CASTLUMORA_TEST_PASSWORD']},'POST');token=a['AccessToken']
if req('/System/Info')['ProgramDataPath']!=str(root/'data'):raise SystemExit('Actual server data root differs.')
current=req('/System/Configuration/livetv')
if current.get('RecordingPath')!=str(root/'dvr-lifecycle') or current.get('TunerHosts')!=state.get('expectedTunerHosts',state['originalConfig'].get('TunerHosts')) or current.get('ListingProviders')!=state['originalConfig'].get('ListingProviders'):raise SystemExit('Fixture config changed unexpectedly; restore manually from saved state.')
names={p['name'] for p in state['programs']}
for timer in req('/LiveTv/Timers')['Items']:
 if timer['Name'] in names and timer.get('Status') in ['New','InProgress']:req('/LiveTv/Timers/'+timer['Id'],method='DELETE')
(root/'guide.xml').write_text(state['originalGuide'])
req('/System/Configuration/livetv',state['originalConfig'],'POST')
for provider in state['originalConfig']['ListingProviders']:req('/LiveTv/ListingProviders?validateListings=true',provider,'POST')
if state.get('originalPolicy'):req('/Users/'+state['userId']+'/Policy',state['originalPolicy'],'POST')
if state.get('viewerId'):req('/Users/'+state['viewerId'],method='DELETE')
state_path.rename(root/'dvr-fixture-last-result.json')
print('Restored own guide/recording options/policy and removed only the synthetic viewer. Recording files, if any, retained for inspection.')
