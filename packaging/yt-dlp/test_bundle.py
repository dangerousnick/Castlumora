import hashlib
import io
import json
import os
from contextlib import redirect_stdout
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch
from bundle import check_url, download, main, OfficialRedirects


class BundleTests(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.TemporaryDirectory(prefix='castlumora-bundle-test-')
        self.addCleanup(self.root.cleanup)
        self.target = Path(self.root.name) / 'tool'
        self.data = b'owned release bytes'
        self.asset = {'fileName': 'yt-dlp_linux', 'sha256': hashlib.sha256(self.data).hexdigest(), 'size': len(self.data)}

    def opener(self, data):
        return Mock(open=Mock(return_value=io.BytesIO(data)))

    def test_verified_download_and_cache_never_execute_content(self):
        with patch('bundle.urllib.request.build_opener', return_value=self.opener(self.data)) as factory:
            download(self.asset, self.target, '2026.10.26')
            self.assertEqual(self.target.read_bytes(), self.data)
            download(self.asset, self.target, '2026.10.26')
            self.assertEqual(factory.call_count, 1)

    def test_corrupt_or_oversized_download_cannot_become_a_verified_asset(self):
        for data in [b'x' * len(self.data), self.data + b'x']:
            with patch('bundle.urllib.request.build_opener', return_value=self.opener(data)):
                with self.assertRaises(ValueError):
                    download(self.asset, self.target, '2026.10.26')
            self.assertFalse(self.target.exists())
            self.assertFalse(self.target.with_name('tool.download').exists())

    def test_existing_different_asset_is_not_overwritten(self):
        self.target.write_bytes(b'previous asset')
        with self.assertRaises(ValueError):
            download(self.asset, self.target, '2026.10.26')
        self.assertEqual(self.target.read_bytes(), b'previous asset')

    def test_another_downloads_temporary_file_is_not_deleted(self):
        temporary = self.target.with_name('tool.download')
        temporary.write_bytes(b'other operation')
        with patch('bundle.urllib.request.build_opener', return_value=self.opener(self.data)):
            with self.assertRaises(FileExistsError):
                download(self.asset, self.target, '2026.10.26')
        self.assertEqual(temporary.read_bytes(), b'other operation')

    def test_macos_cli_packages_selected_asset_source_notices_and_private_permissions(self):
        manifest = json.loads((Path(__file__).parent / 'release.json').read_text())
        for platform in ['osx-x64', 'osx-arm64']:
            with self.subTest(platform=platform):
                root = Path(self.root.name) / platform
                def owned_download(asset, destination, version):
                    self.assertEqual(version, manifest['version'])
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    destination.write_bytes(b'owned non-executable fixture')
                with patch('sys.argv', ['bundle.py', '--platform', platform, '--system-root', str(root)]), \
                     patch('bundle.download', side_effect=owned_download) as fetch, redirect_stdout(io.StringIO()):
                    main()
                tool = root / 'tools/yt-dlp' / manifest['version']
                executable = tool / platform / 'yt-dlp_macos'
                self.assertEqual(executable.read_bytes(), b'owned non-executable fixture')
                self.assertEqual(fetch.call_args_list[0].args[0], manifest['assets'][platform])
                self.assertEqual(fetch.call_args_list[1].args[0], manifest['source'])
                for name in ['LICENSE', 'THIRD_PARTY_LICENSES.txt', 'NOTICE.md', 'release.json', 'yt-dlp-source.tar.gz']:
                    self.assertGreater((tool / 'notices' / name).stat().st_size, 0)
                if os.name != 'nt':
                    self.assertEqual(executable.stat().st_mode & 0o777, 0o700)

    def test_unsafe_hosts_schemes_ports_and_redirects_are_rejected(self):
        for url in ['http://github.com/file', 'file:///etc/passwd', 'https://github.com.attacker.test/file', 'https://user@github.com/file', 'https://github.com:444/file', 'https://127.0.0.1/file']:
            with self.assertRaises(ValueError):
                check_url(url)
            with self.assertRaises(ValueError):
                OfficialRedirects().redirect_request(None, None, 302, '', {}, url)
        check_url('https://release-assets.githubusercontent.com/file')


if __name__ == '__main__':
    unittest.main()
