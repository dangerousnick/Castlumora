"""Prepare only the dedicated, disposable GitHub package-recovery fixture."""
import hashlib
import json
import os
from pathlib import Path
import sys
import urllib.request

ROOT = Path("/tmp/castlumora-preview-fresh")
assert os.environ.get("GITHUB_ACTIONS") == "true"
assert (ROOT / ".castlumora-preview").is_file()
assert not ROOT.is_symlink()
metadata = Path(os.environ["RUNNER_TEMP"]) / "castlumora-profile-recovery.json"
registry = ROOT / "config/castlumora/profiles/profiles.json"
key = registry.parent / "credentials.key"
for candidate in (registry, key):
    for ancestor in (candidate, *candidate.parents):
        assert not ancestor.is_symlink()

def request(path, token=None, value=None):
    authorization = ('MediaBrowser Token="' + token + '"') if token else 'MediaBrowser Client="Castlumora fixture", Device="CI", DeviceId="recovery-fixture", Version="1.0"'
    headers = {"Authorization": authorization, "Content-Type": "application/json"}
    data = None if value is None else json.dumps(value).encode()
    req = urllib.request.Request("http://127.0.0.1:28096/" + path, data=data, headers=headers)
    with urllib.request.urlopen(req, timeout=20) as response:
        payload = response.read()
        return json.loads(payload) if payload else None

def save(path, value):
    temporary = path.with_suffix(".tmp")
    with os.fdopen(os.open(temporary, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), "w") as handle:
        json.dump(value, handle)
        handle.flush()
        os.fsync(handle.fileno())
    os.replace(temporary, path)

if sys.argv[1] == "prepare":
    login = request("Users/AuthenticateByName", value={"Username": "PreviewOwner", "Pw": os.environ["CASTLUMORA_PREVIEW_PASSWORD"]})
    token = login["AccessToken"]
    owner = login["User"]["Id"]
    folders = request("Library/VirtualFolders", token)
    library = next(folder["ItemId"] for folder in folders if folder["Name"] == "Packaged Fixture Movies")
    child = request("Castlumora/Profiles", token, {"accountId": owner, "name": "Recovery Fixture", "preset": "kids", "libraries": [library]})["id"]
    items = request("Items?recursive=true&includeItemTypes=Movie&parentId=" + library, token)["Items"]
    assert len(items) == 1, "Expected the single owned movie in the dedicated fixture library"
    item = items[0]["Id"]
    request("Users/" + child + "/FavoriteItems/" + item, token, {})
    info = request("Users/" + child, token)
    data = request("Items/" + item + "?userId=" + child, token)["UserData"]
    assert data["IsFavorite"]
    save(metadata, {"id": child, "ownerId": owner, "libraryId": library, "itemId": item,
        "configuration": info["Configuration"], "userData": data, "coreName": info["Name"],
        "keySha256": hashlib.sha256(key.read_bytes()).hexdigest()})
    print("Prepared original managed identity and retained viewing data; no credentials persisted.")
elif sys.argv[1] == "interrupt":
    pid = int(os.environ["PREVIEW_PID"])
    proc = Path("/proc") / str(pid) / "stat"
    assert not proc.exists() or proc.read_text().split(") ", 1)[1].startswith("Z"), "Stop fixture server before editing private intent"
    saved = json.loads(metadata.read_text())
    state = json.loads(registry.read_text())
    assert state["Version"] == 2
    profile = next(profile for profile in state["Profiles"] if profile["User"].replace("-", "") == saved["id"].replace("-", ""))
    assert profile["Ready"] and profile["Lifecycle"] == "active"
    assert hashlib.sha256(key.read_bytes()).hexdigest() == saved["keySha256"]
    profile.update(Ready=False, Lifecycle="provisioning", PendingLibraries=[saved["libraryId"]])
    save(registry, state)
    print("Recorded interrupted intent only while disposable preview was stopped; database/key preserved.")
else:
    raise SystemExit("Expected prepare or interrupt")
