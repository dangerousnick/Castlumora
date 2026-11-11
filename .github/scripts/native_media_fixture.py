"""Install only reviewed, hash-pinned CI tools; no production downloader/update API."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import tarfile
import tempfile
import time
import urllib.request
import zipfile

MANIFEST = Path(__file__).resolve().parents[1] / "fixtures/native-media.json"
MAX_BINARY = 256 * 1024 * 1024


def verify(path, asset):
    if not 0 < asset["bytes"] <= 256 * 1024 * 1024:
        raise ValueError("Native media archive exceeds its size bound")
    if path.stat().st_size != asset["bytes"]:
        raise ValueError("Native media archive size mismatch")
    with path.open("rb") as source:
        digest = hashlib.file_digest(source, "sha256").hexdigest()
    if digest != asset["sha256"]:
        raise ValueError("Native media archive checksum mismatch")


def download(path, asset):
    if not asset["url"].startswith("https://github.com/yt-dlp/FFmpeg-Builds/releases/download/autobuild-"):
        raise ValueError("Only the reviewed dated publisher release is allowed")
    if not 0 < asset["bytes"] <= 256 * 1024 * 1024:
        raise ValueError("Native media archive exceeds its size bound")
    start = time.monotonic()
    size = 0
    with urllib.request.urlopen(asset["url"], timeout=30) as response, path.open("xb") as output:
        while chunk := response.read(1024 * 1024):
            size += len(chunk)
            if size > asset["bytes"] or time.monotonic() - start > 300:
                raise ValueError("Native media download exceeded its size/time bound")
            output.write(chunk)
    verify(path, asset)


def extract(path, output, asset, platform):
    """Never extract paths from the archive; stream exact regular files to fixed names."""
    suffix = ".exe" if platform == "win-x64" else ""
    root = asset["root"]
    wanted = {f"{root}/bin/{name}{suffix}": name + suffix for name in ("ffmpeg", "ffprobe")}
    for license_name in ("LICENSE", "LICENSE.txt", "COPYING.GPLv3"):
        wanted[f"{root}/{license_name}"] = license_name
    found = set()

    def copy(name, size, regular, source):
        target_name = wanted.get(name)
        if target_name is None:
            return
        if not regular or target_name in found or not 0 < size <= MAX_BINARY:
            raise ValueError("Unsafe or duplicate native media archive member")
        if target_name not in ("ffmpeg" + suffix, "ffprobe" + suffix) and size > 1024 * 1024:
            raise ValueError("Native media license exceeds bound")
        with (output / target_name).open("xb") as target:
            remaining = size
            while remaining:
                chunk = source.read(min(65536, remaining))
                if not chunk:
                    raise ValueError("Truncated native media archive member")
                target.write(chunk)
                remaining -= len(chunk)
        if os.name != "nt":
            (output / target_name).chmod(0o700 if target_name.endswith(suffix) and target_name.startswith(("ffmpeg", "ffprobe")) else 0o600)
        found.add(target_name)

    if platform == "win-x64":
        with zipfile.ZipFile(path) as archive:
            for member in archive.infolist():
                if member.filename not in wanted:
                    continue
                regular = not member.is_dir() and not stat.S_ISLNK(member.external_attr >> 16)
                with archive.open(member) as source:
                    copy(member.filename, member.file_size, regular, source)
    else:
        with tarfile.open(path, mode="r|xz") as archive:
            for member in archive:
                if member.name not in wanted:
                    continue
                if not member.isfile():
                    raise ValueError("Native media archive member is not a regular file")
                copy(member.name, member.size, True, archive.extractfile(member))
    if not {"ffmpeg" + suffix, "ffprobe" + suffix} <= found or not found.intersection({"LICENSE", "LICENSE.txt", "COPYING.GPLv3"}):
        raise ValueError("Required binaries or license missing from reviewed archive")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform", choices=("linux-x64", "win-x64"), required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text())
    asset = manifest["assets"][args.platform]
    output = args.output.resolve()
    if output.exists():
        raise ValueError("Native fixture output already exists; do not trust or overwrite stale executables")
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="native-media-", dir=output.parent) as temporary:
        temporary = Path(temporary)
        archive = temporary / "reviewed.archive"
        download(archive, asset)
        staging = temporary / "tools"
        staging.mkdir(mode=0o700)
        extract(archive, staging, asset, args.platform)
        suffix = ".exe" if args.platform == "win-x64" else ""
        for name in ("ffmpeg", "ffprobe"):
            result = subprocess.run([str(staging / (name + suffix)), "-version"], text=True, capture_output=True, timeout=15, check=True)
            print(result.stdout.splitlines()[0])
        (staging / "PROVENANCE.json").write_text(json.dumps(manifest, indent=2) + "\n")
        staging.rename(output)
    values = {"CASTLUMORA_TEST_FFMPEG": str(output / ("ffmpeg" + suffix)), "CASTLUMORA_TEST_FFPROBE": str(output / ("ffprobe" + suffix))}
    if os.environ.get("GITHUB_ENV"):
        with open(os.environ["GITHUB_ENV"], "a", encoding="utf-8") as env:
            for key, value in values.items():
                env.write(f"{key}={value}\n")
    print(json.dumps({"release": manifest["release"], "platform": args.platform, **values}))


if __name__ == "__main__":
    main()
