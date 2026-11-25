"""Security tests for the CI-only native archive boundary; no fake application tests."""
import importlib.util
import io
from pathlib import Path
import tarfile
import tempfile
import unittest
import zipfile

SPEC = importlib.util.spec_from_file_location("native_media_fixture", Path(__file__).with_name("native_media_fixture.py"))
fixture = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(fixture)


class NativeFixtureTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="native-fixture-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.output = self.root / "tools"
        self.output.mkdir()
        self.asset = {"root": "reviewed", "bytes": 1, "sha256": "0" * 64}

    def archive(self, extra=None):
        path = self.root / "fixture.tar.xz"
        with tarfile.open(path, "w:xz") as archive:
            for name in ("ffmpeg", "ffprobe", "LICENSE"):
                member = tarfile.TarInfo("reviewed/" + ("bin/" if name != "LICENSE" else "") + name)
                member.size = 4
                archive.addfile(member, io.BytesIO(b"test"))
            if extra:
                extra(archive)
        return path

    def test_only_exact_regular_files_and_license_are_written(self):
        def extra(archive):
            member = tarfile.TarInfo("../../escape")
            member.size = 4
            archive.addfile(member, io.BytesIO(b"evil"))
        fixture.extract(self.archive(extra), self.output, self.asset, "linux-x64")
        self.assertEqual({"ffmpeg", "ffprobe", "LICENSE"}, {p.name for p in self.output.iterdir()})
        self.assertFalse((self.root / "escape").exists())

    def test_duplicate_selected_binary_is_rejected(self):
        def extra(archive):
            member = tarfile.TarInfo("reviewed/bin/ffmpeg")
            member.size = 4
            archive.addfile(member, io.BytesIO(b"evil"))
        with self.assertRaisesRegex(ValueError, "duplicate"):
            fixture.extract(self.archive(extra), self.output, self.asset, "linux-x64")

    def test_selected_symbolic_link_is_rejected(self):
        path = self.root / "linked.tar.xz"
        with tarfile.open(path, "w:xz") as archive:
            member = tarfile.TarInfo("reviewed/bin/ffmpeg")
            member.type = tarfile.SYMTYPE
            member.linkname = "/untrusted/command"
            archive.addfile(member)
        with self.assertRaisesRegex(ValueError, "regular"):
            fixture.extract(path, self.output, self.asset, "linux-x64")

    def test_archive_hash_and_size_must_match(self):
        path = self.archive()
        with self.assertRaisesRegex(ValueError, "size"):
            fixture.verify(path, self.asset)
        self.asset["bytes"] = path.stat().st_size
        with self.assertRaisesRegex(ValueError, "checksum"):
            fixture.verify(path, self.asset)

    def test_missing_required_binary_cannot_be_silently_accepted(self):
        path = self.root / "empty.zip"
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr("reviewed/bin/ffmpeg.exe", "owned")
        with self.assertRaisesRegex(ValueError, "missing"):
            fixture.extract(path, self.output, self.asset, "win-x64")

    def test_windows_fixed_names_do_not_extract_traversal(self):
        path = self.root / "windows.zip"
        with zipfile.ZipFile(path, "w") as archive:
            for name in ("bin/ffmpeg.exe", "bin/ffprobe.exe", "LICENSE"):
                archive.writestr("reviewed/" + name, "owned")
            archive.writestr("../../escape.exe", "untrusted")
        fixture.extract(path, self.output, self.asset, "win-x64")
        self.assertEqual({"ffmpeg.exe", "ffprobe.exe", "LICENSE"}, {p.name for p in self.output.iterdir()})
        self.assertFalse((self.root / "escape.exe").exists())


if __name__ == "__main__":
    unittest.main()
