"""Wait only for the dedicated local packaged-preview API, never an HTML startup page."""
import json
import time
import urllib.error
import urllib.request
for attempt in range(90):
    try:
        with urllib.request.urlopen('http://127.0.0.1:28096/System/Info/Public', timeout=2) as response:
            data = json.load(response)
        if 'StartupWizardCompleted' in data:
            break
    except (OSError, ValueError, urllib.error.URLError):
        pass
    time.sleep(1)
else:
    raise SystemExit('Packaged preview API did not become ready within 90 attempts.')
