#!/usr/bin/env bash
set -Eeuo pipefail
cd "$(git rev-parse --show-toplevel)"
existing=$(git config --get core.hooksPath || true)
if [[ -n "$existing" && "$existing" != .githooks ]]; then
    echo "Refusing to replace existing hooksPath: $existing; integrate the required pre-push gate first." >&2
    exit 1
fi
git config --local core.hooksPath .githooks
echo 'Castlumora pre-push validation installed for this checkout.'
