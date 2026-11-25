"""Generate synthetic photos and two-page PDF/EPUB books on a marked disposable loopback server."""
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
header = 'MediaBrowser Client="Castlumora fixture", Device="Disposable test", DeviceId="castlumora-shelf-fixture", Version="1.0"'
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

import zipfile
photos = root / 'photo-shelf' / 'Castle Colors'
books = root / 'books'
photos.mkdir(parents=True, exist_ok=True)
books.mkdir(parents=True, exist_ok=True)
for color in ['purple', 'blue']:
    target = photos / (color.title() + ' Castle.jpg')
    if not target.exists():
        subprocess.run(['ffmpeg', '-nostdin', '-v', 'error', '-f', 'lavfi', '-i',
                        f'color=c={color}:s=640x480', '-frames:v', '1', str(target)], check=True)
pdf = books / 'Castle PDF.pdf'
if not pdf.exists():
    objects = [b'<< /Type /Catalog /Pages 2 0 R >>', b'<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>']
    for content in [6, 7]:
        objects.append(f'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 500] /Resources << /Font << /F1 5 0 R >> >> /Contents {content} 0 R >>'.encode())
    objects.append(b'<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>')
    for page in [1, 2]:
        text = f'BT /F1 20 Tf 40 400 Td (Castle PDF page {page}) Tj ET'.encode()
        objects.append(b'<< /Length ' + str(len(text)).encode() + b' >>\nstream\n' + text + b'\nendstream')
    output = b'%PDF-1.4\n'
    offsets = [0]
    for i, obj in enumerate(objects, 1):
        offsets.append(len(output))
        output += f'{i} 0 obj\n'.encode() + obj + b'\nendobj\n'
    xref = len(output)
    output += f'xref\n0 {len(offsets)}\n0000000000 65535 f \n'.encode()
    for offset in offsets[1:]:
        output += f'{offset:010} 00000 n \n'.encode()
    output += f'trailer\n<< /Size {len(offsets)} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF'.encode()
    pdf.write_bytes(output)
epub = books / 'Castle EPUB.epub'
if not epub.exists():
    with zipfile.ZipFile(epub, 'w') as archive:
        archive.writestr('mimetype', 'application/epub+zip')
        archive.writestr('META-INF/container.xml', '<container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container"><rootfiles><rootfile full-path="book.opf" media-type="application/oebps-package+xml"/></rootfiles></container>')
        archive.writestr('book.opf', '<package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="id"><metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="id">castle-fixture</dc:identifier><dc:title>Castle EPUB</dc:title><dc:language>en</dc:language></metadata><manifest><item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/><item id="one" href="one.xhtml" media-type="application/xhtml+xml"/><item id="two" href="two.xhtml" media-type="application/xhtml+xml"/></manifest><spine><itemref idref="one"/><itemref idref="two"/></spine></package>')
        archive.writestr('nav.xhtml', '<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"><head><title>Contents</title></head><body><nav epub:type="toc"><ol><li><a href="one.xhtml">First castle</a></li><li><a href="two.xhtml">Second castle</a></li></ol></nav></body></html>')
        for name in ['one', 'two']:
            archive.writestr(name + '.xhtml', '<html xmlns="http://www.w3.org/1999/xhtml"><head><title>Castle chapter</title></head><body><h1>Castle chapter ' + name + '</h1><p>A quiet violet castle beneath the moon.</p></body></html>')
folders = request('/Library/VirtualFolders')
for name, path, kind, types in [('Fixture Native Photos', root / 'photo-shelf', 'homevideos', ['Photo', 'PhotoAlbum']), ('Fixture Books', books, 'books', ['Book'])]:
    existing = next((folder for folder in folders if folder['Name'] == name), None)
    if existing and str(path) not in existing['Locations']:
        raise SystemExit('Existing fixture library points elsewhere; not modified.')
    if not existing:
        options = {'PathInfos': [{'Path': str(path)}], 'EnableRealtimeMonitor': False, 'EnablePhotos': True,
                   'TypeOptions': [{'Type': item, 'MetadataFetchers': [], 'ImageFetchers': []} for item in types]}
        request('/Library/VirtualFolders?' + urllib.parse.urlencode({'name': name, 'collectionType': kind, 'refreshLibrary': 'true'}), {'LibraryOptions': options}, 'POST')
request('/Library/Refresh', {}, 'POST')
for attempt in range(60):
    items = request('/Items?' + urllib.parse.urlencode({'UserId': auth['User']['Id'], 'Recursive': 'true', 'IncludeItemTypes': 'Book,Photo', 'Fields': 'Path'}))['Items']
    found = [item for item in items if item.get('Path', '').startswith((str(books) + '/', str(root / 'photo-shelf') + '/'))]
    if len(found) >= 4:
        print('Synthetic photos, PDF and EPUB are indexed.')
        break
    time.sleep(1)
else:
    raise SystemExit('Synthetic shelf items did not finish indexing.')
