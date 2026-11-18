#!/usr/bin/env bash
# Test harness only: stop/restart this job's disposable extracted preview.
set -euo pipefail
[[ ${GITHUB_ACTIONS:-} == true && ${PREVIEW_PID:-} =~ ^[0-9]+$ ]]
[[ $1 == interrupted || $1 == persisted ]]
[[ -f /tmp/castlumora-preview-fresh/.castlumora-preview ]]
kill -TERM "$PREVIEW_PID"
python3 - <<'PY'
import os
from pathlib import Path
import time
pid = int(os.environ['PREVIEW_PID'])
proc = Path('/proc') / str(pid) / 'stat'
for _ in range(60):
    if not proc.exists() or proc.read_text().split(') ', 1)[1].startswith('Z'):
        break
    time.sleep(0.5)
else:
    raise SystemExit('Fixture preview did not stop; refusing to modify its state')
PY
if [[ $1 == interrupted ]]; then
  python3 Castlumora.Web/browser/profile-recovery-fixture.py interrupt
fi
env -u DOTNET_ROOT -u DOTNET_ROOT_X64 CASTLUMORA_DATA_DIR=/tmp/castlumora-preview-fresh "$PREVIEW_PACKAGE/start-castlumora" > "$RUNNER_TEMP/castlumora-recovery-$1.log" 2>&1 &
echo "PREVIEW_PID=$!" >> "$GITHUB_ENV"
