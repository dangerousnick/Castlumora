#!/usr/bin/env python3
"""Bundle a reviewed yt-dlp platform asset/source with verified hashes; never run downloaded code."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import urllib.parse
import urllib.request


class OfficialRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        check_url(newurl)
        return super().redirect_request(req, fp, code, msg, headers, newurl)


def check_url(value):
    url = urllib.parse.urlsplit(value)
    if url.scheme != 'https' or url.hostname not in {'github.com', 'release-assets.githubusercontent.com', 'objects.githubusercontent.com'} or url.username or url.password or url.port not in (None, 443):
        raise ValueError('Dependency downloads must stay on official HTTPS release hosts.')


def download(asset, destination, version):
    url = f'https://github.com/yt-dlp/yt-dlp/releases/download/{version}/{asset["fileName"]}'
    check_url(url)
    if destination.exists():
        if destination.stat().st_size == asset['size'] and hashlib.sha256(destination.read_bytes()).hexdigest() == asset['sha256']:
            return
        raise ValueError('Existing dependency file differs from the reviewed release; not overwritten.')
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(destination.name + '.download')
    digest = hashlib.sha256()
    size = 0
    owned = False
    try:
        opener = urllib.request.build_opener(OfficialRedirects())
        with temporary.open('xb') as output:
            owned = True
            with opener.open(url, timeout=60) as response:
                while chunk := response.read(65536):
                    size += len(chunk)
                    if size > asset['size']:
                        raise ValueError('Dependency exceeded pinned size.')
                    digest.update(chunk)
                    output.write(chunk)
        if size != asset['size'] or digest.hexdigest() != asset['sha256']:
            raise ValueError('Dependency checksum/size does not match the reviewed release.')
        temporary.replace(destination)
    finally:
        if owned:
            temporary.unlink(missing_ok=True)


def main():
    here = Path(__file__).resolve().parent
    manifest = json.loads((here / 'release.json').read_text())
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--platform', choices=sorted(manifest['assets']), required=True)
    parser.add_argument('--system-root', type=Path, required=True)
    args = parser.parse_args()
    version = manifest['version']
    asset = manifest['assets'][args.platform]
    root = args.system_root.resolve()
    destination = root / 'tools' / 'yt-dlp' / version / args.platform / asset['fileName']
    download(asset, destination, version)
    if args.platform != 'win-x64':
        destination.chmod(0o700)
    notices = root / 'tools' / 'yt-dlp' / version / 'notices'
    notices.mkdir(parents=True, exist_ok=True)
    for name in ['LICENSE', 'THIRD_PARTY_LICENSES.txt', 'NOTICE.md', 'release.json']:
        shutil.copyfile(here / name, notices / name)
    download(manifest['source'], notices / 'yt-dlp-source.tar.gz', version)
    print(f'Verified yt-dlp {version} ({args.platform}); binary, source archive and license notices bundled.')


if __name__ == '__main__':
    main()
