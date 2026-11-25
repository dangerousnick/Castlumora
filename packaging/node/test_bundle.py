"""Owned archive fixtures exercise the actual constrained extraction path."""
import hashlib
import io
import tempfile
import tarfile
import unittest
from pathlib import Path
from bundle import extract, verify_existing, OfficialOnly


class NodeBundleTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.destination = self.root / 'bundle'
        self.archive = self.root / 'runtime.tar.xz'
        self.binary = b'\x7fELF\x02\x01' + b'\0' * 12 + b'\x3e\0' + b'owned executable fixture'
        self.license = b'owned license fixture'

    def build(self, link=False, duplicate=False):
        with tarfile.open(self.archive, 'w:xz') as archive:
            for name, data in [('bin/node', self.binary), ('LICENSE', self.license), ('../../escape', b'ignore')]:
                item = tarfile.TarInfo('node-v24.22.1-linux-x64/' + name)
                if link and name == 'bin/node':
                    item.type = tarfile.SYMTYPE
                    item.linkname = '/bin/sh'
                    archive.addfile(item)
                else:
                    item.size = len(data)
                    archive.addfile(item, io.BytesIO(data))
                    if duplicate and name == 'bin/node':
                        archive.addfile(item, io.BytesIO(data))
        return {
            'version': '24.22.1', 'size': self.archive.stat().st_size,
            'sha256': hashlib.sha256(self.archive.read_bytes()).hexdigest(),
            'binarySize': len(self.binary), 'binarySha256': hashlib.sha256(self.binary).hexdigest(),
            'licenseSha256': hashlib.sha256(self.license).hexdigest()
        }

    def test_extracts_only_reviewed_regular_files_and_rechecks_existing(self):
        manifest = self.build()
        extract(self.archive, self.destination, manifest)
        verify_existing(self.destination, manifest)
        self.assertEqual({p.name for p in self.destination.iterdir()}, {'node', 'LICENSE'})
        self.assertFalse((self.root / 'escape').exists())
        self.assertEqual((self.destination / 'node').stat().st_mode & 0o777, 0o700)
        self.assertEqual((self.destination / 'LICENSE').read_bytes(), self.license)

    def test_rejects_archive_corruption(self):
        manifest = self.build()
        self.archive.write_bytes(self.archive.read_bytes() + b'corrupt')
        with self.assertRaises(ValueError):
            extract(self.archive, self.destination, manifest)
        self.assertFalse(self.destination.exists())

    def test_rejects_selected_links_and_duplicates_before_extracting(self):
        for link, duplicate in [(True, False), (False, True)]:
            with self.subTest(link=link, duplicate=duplicate):
                manifest = self.build(link, duplicate)
                with self.assertRaises(ValueError):
                    extract(self.archive, self.destination, manifest)
                self.assertFalse((self.destination / 'node').exists())

    def test_never_overwrites_existing_files(self):
        manifest = self.build()
        self.destination.mkdir()
        (self.destination / 'node').write_bytes(b'preserved')
        with self.assertRaises(ValueError):
            extract(self.archive, self.destination, manifest)
        self.assertEqual((self.destination / 'node').read_bytes(), b'preserved')

    def test_cached_runtime_rejects_modified_license_and_links(self):
        manifest = self.build()
        extract(self.archive, self.destination, manifest)
        (self.destination / 'LICENSE').write_bytes(b'modified')
        with self.assertRaises(ValueError):
            verify_existing(self.destination, manifest)
        (self.destination / 'LICENSE').unlink()
        (self.destination / 'LICENSE').symlink_to(self.archive)
        with self.assertRaises(ValueError):
            verify_existing(self.destination, manifest)

    def test_official_dependency_download_refuses_redirects(self):
        with self.assertRaises(ValueError):
            OfficialOnly().redirect_request(None, None, 302, 'redirect', {}, 'https://unreviewed.example/node')


if __name__ == '__main__':
    unittest.main()
