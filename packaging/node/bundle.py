#!/usr/bin/env python3
"""Bundle the pinned Node runtime/license; extract only reviewed regular files."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import tarfile
import urllib.request


def verify_existing(destination, manifest):
    for path in [destination, *destination.parents]:
        if path.is_symlink():
            raise ValueError('Node dependency paths must not be symbolic links')
    for name, digest in [('node', manifest['binarySha256']), ('LICENSE', manifest['licenseSha256'])]:
        path = destination / name
        if path.is_symlink() or not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            raise ValueError('Existing Node dependency differs from reviewed bytes')
    if (destination / 'node').stat().st_size != manifest['binarySize']:
        raise ValueError('Existing Node binary size differs')


def extract(archive, destination, manifest):
    if archive.stat().st_size != manifest['size'] or hashlib.sha256(archive.read_bytes()).hexdigest() != manifest['sha256']:
        raise ValueError('Node archive checksum/size differs from the reviewed release')
    prefix = f'node-v{manifest["version"]}-linux-x64/'
    expected = {prefix+'bin/node': 'node', prefix+'LICENSE': 'LICENSE'}
    for path in [destination, *destination.parents]:
        if path.is_symlink():
            raise ValueError('Node dependency paths must not be symbolic links')
    destination.mkdir(parents=True, exist_ok=True)
    with tarfile.open(archive, 'r:xz') as source:
        selected = {}
        for item in source:
            if item.name in expected:
                if item.name in selected or not item.isfile() or item.size <= 0 or item.size > 150_000_000:
                    raise ValueError('Unsafe or duplicate Node runtime member')
                selected[item.name] = item
        if set(selected) != set(expected):
            raise ValueError('Incomplete Node archive')
        for name in expected.values():
            target = destination / name
            if target.exists() or target.is_symlink():
                raise ValueError('Node output already exists; never overwrite unverified files')
        for name, item in selected.items():
            target = destination / expected[name]
            with source.extractfile(item) as content, target.open('xb') as output:
                shutil.copyfileobj(content, output)
            target.chmod(0o700 if target.name == 'node' else 0o600)
    binary = destination / 'node'
    data = binary.read_bytes()
    if data[:5] != b'\x7fELF\x02' or data[5] != 1 or int.from_bytes(data[18:20], 'little') != 62:
        raise ValueError('Expected a Linux x64 ELF runtime')
    verify_existing(destination, manifest)
    return {'version': manifest['version'], 'size': len(data), 'sha256': hashlib.sha256(data).hexdigest()}


class OfficialOnly(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, url):
        raise ValueError('Unexpected Node dependency redirect')


def main():
    manifest = json.loads(Path(__file__).with_name('release.json').read_text())
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--system-root', type=Path, required=True)
    args = parser.parse_args()
    root = args.system_root.resolve() / 'tools' / 'node' / manifest['version'] / manifest['platform']
    if root.exists() or root.is_symlink():
        verify_existing(root, manifest)
        print('Existing pinned Node runtime and license verified')
        return
    archive = root.parent / (manifest['fileName']+'.download')
    root.parent.mkdir(parents=True, exist_ok=True)
    owned = False
    try:
        opener = urllib.request.build_opener(OfficialOnly())
        with opener.open(manifest['url'], timeout=60) as response, archive.open('xb') as output:
            owned = True
            total = 0
            while chunk := response.read(65536):
                total += len(chunk)
                if total > manifest['size']:
                    raise ValueError('Node archive exceeded its pinned size')
                output.write(chunk)
        receipt = extract(archive, root, manifest)
        (root / 'runtime.json').write_text(json.dumps(receipt, indent=2)+'\n')
        print('Verified Node '+manifest['version']+' Linux x64 runtime and license bundled')
    finally:
        if owned:
            archive.unlink(missing_ok=True)


if __name__ == '__main__':
    main()
