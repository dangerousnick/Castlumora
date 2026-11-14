from pathlib import Path
import subprocess
import argparse
parser = argparse.ArgumentParser(description='Generate only synthetic repeated-audio test episodes.')
parser.add_argument('--fixture-root', type=Path, required=True)
args = parser.parse_args()
base = args.fixture_root.resolve()
if not (base / '.castlumora-disposable').is_file():
 raise SystemExit('Missing explicit disposable fixture marker.')
root = base / 'series' / 'Castlumora Skipper' / 'Season 01'
root.mkdir(parents=True, exist_ok=True)
(root.parent/'tvshow.nfo').write_text('<tvshow><title>Castlumora Skipper</title></tvshow>')
for n in range(1,4):
 file=root/f'Castlumora Skipper S01E0{n}.mp4'
 expression=f'if(lt(t,10),0.15*sin(2*PI*{180+90*n}*t),if(lt(t,40),0.3*sin(2*PI*(350*t+20*t*t))+0.15*sin(2*PI*970*t),if(lt(t,145),0.15*sin(2*PI*{100+13*n}*t),if(lt(t,170),0.3*sin(2*PI*(720*t+7*t*t))+0.15*sin(2*PI*1300*t),0.2*sin(2*PI*{450+30*n}*t)))))'
 if not file.exists():
  subprocess.run(['ffmpeg','-nostdin','-hide_banner','-v','error','-f','lavfi','-i','color=c=purple:s=320x180:r=10','-f','lavfi','-i','aevalsrc='+expression.replace(',',r'\,')+':s=8000','-t','180','-c:v','libx264','-preset','ultrafast','-pix_fmt','yuv420p','-c:a','aac','-b:a','64k','-movflags','+faststart',str(file)],check=True)
 file.with_suffix('.nfo').write_text(f'<episodedetails><title>Synthetic Skipper Episode {n}</title><season>1</season><episode>{n}</episode></episodedetails>')
print('Three local synthetic episodes generated; repeated intro/outro and unique main/post-credit audio')
