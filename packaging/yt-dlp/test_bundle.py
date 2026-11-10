import hashlib
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch
from bundle import check_url, download, OfficialRedirects


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

    def test_unsafe_hosts_schemes_ports_and_redirects_are_rejected(self):
        for url in ['http://github.com/file', 'file:///etc/passwd', 'https://github.com.attacker.test/file', 'https://user@github.com/file', 'https://github.com:444/file', 'https://127.0.0.1/file']:
            with self.assertRaises(ValueError):
                check_url(url)
            with self.assertRaises(ValueError):
                OfficialRedirects().redirect_request(None, None, 302, '', {}, url)
        check_url('https://release-assets.githubusercontent.com/file')


if __name__ == '__main__':
    unittest.main()
