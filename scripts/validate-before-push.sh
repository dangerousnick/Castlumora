#!/usr/bin/env bash
# Fail-closed local counterpart of Format, ABI HEAD build, Tests and frontend CI.
set -Eeuo pipefail
root=$(git rev-parse --show-toplevel)
cd "$root"
if ! git diff --quiet HEAD -- || test -n "$(git ls-files --others --exclude-standard)"; then
    echo 'BLOCKED: commit the intended changes before validating; uncommitted/untracked files can mask an incomplete commit.' >&2
    exit 1
fi
head=$(git rev-parse HEAD)
logdir=$(mktemp -d "${TMPDIR:-/tmp}/castlumora-prepush.XXXXXXXX")
export DOTNET_PROCESSOR_COUNT=${DOTNET_PROCESSOR_COUNT:-2}
export MSBUILDDISABLENODEREUSE=1
# SDK-supported I/O optimization only: identical dependency bytes, no omitted assets/tests.
# Do not hard-link source/content fixtures, which tests may edit.
export CreateHardLinksForCopyLocalIfPossible=true
trap 'echo "VALIDATION FAILED: push blocked. Logs: $logdir" >&2' ERR
assert_snapshot() {
    if [[ $(git -C "$root" rev-parse HEAD) != "$head" ]] \
        || ! git -C "$root" diff --quiet HEAD -- \
        || [[ -n $(git -C "$root" ls-files --others --exclude-standard) ]]; then
        echo 'BLOCKED: source changed during validation; commit the final source and rerun the entire gate.' >&2
        return 1
    fi
}
run() {
    assert_snapshot
    local name=$1
    shift
    echo "Checking $name (log: $logdir/$name.log)"
    if ! "$@" > "$logdir/$name.log" 2>&1; then
        tail -n 60 "$logdir/$name.log" >&2
        return 1
    fi
}
command -v dotnet >/dev/null
command -v node >/dev/null
command -v npm >/dev/null
command -v python3 >/dev/null
sdk=$(dotnet --version)
[[ "$sdk" == 10.* ]] || { echo 'BLOCKED: .NET SDK10 required.' >&2; exit 1; }
[[ $(node --version) == v24.* ]] || { echo 'BLOCKED: Node24 required.' >&2; exit 1; }
run hook-tests python3 -m unittest discover -s scripts/tests -p 'test_*.py'
run disk-space python3 -c 'import shutil,sys; free=shutil.disk_usage(sys.argv[1]).free; print(f"Free bytes: {free}; informational only; build and test I/O failures remain fatal")' "$root"
run restore dotnet restore Jellyfin.sln
# No include/exclude list: Debug analyzers must load across the whole solution.
run format dotnet format --verify-no-changes --verbosity diagnostic
if grep -Eq 'Warnings were encountered while loading the workspace|Failed to load project|could not be loaded' "$logdir/format.log"; then
    echo "BLOCKED: formatter workspace did not load completely. Inspect $logdir/format.log" >&2
    exit 1
fi
# Same Debug/server build as ci-compat.yml; Release-only misses Debug analyzers.
run debug-build dotnet build Jellyfin.Server -o "$logdir/abi-head" -m:1
# Output is disposable; retain logs while avoiding duplicate large dependency copies.
rm -rf -- "$logdir/abi-head"
run release-build dotnet build Jellyfin.sln --configuration Release -m:1
case "$(uname -s):$(uname -m)" in
    Linux:x86_64) platform=linux-x64 ;;
    MINGW*:x86_64|MSYS*:x86_64) platform=win-x64 ;;
    Darwin:x86_64|Darwin:arm64) platform= ;;
    *) echo 'BLOCKED: no CI-equivalent validation setup is defined for this host.' >&2; exit 1 ;;
esac
# Never silently skip the real platform executable tests.
run bundle-tests python3 -m unittest discover -s packaging/yt-dlp -p 'test_*.py'
if [[ -n "$platform" ]]; then
    export CASTLUMORA_TEST_YTDLP_BUNDLE=${CASTLUMORA_TEST_YTDLP_BUNDLE:-"$logdir/tool-fixture"}
    run bundle python3 packaging/yt-dlp/bundle.py --platform "$platform" --system-root "$CASTLUMORA_TEST_YTDLP_BUNDLE"
else
    # Match existing macOS CI: run all tests; unsupported real-tool cases retain explicit skips.
    unset CASTLUMORA_TEST_YTDLP_BUNDLE
    echo 'macOS native managed-tool asset is not yet pinned; full managed suites still run.'
fi
run test-disk-space python3 -c 'import shutil,sys; free=shutil.disk_usage(sys.argv[1]).free; print(f"Free bytes: {free}; informational only; build and test I/O failures remain fatal")' "$root"
run tests dotnet test Jellyfin.sln --configuration Release --collect:XPlat\ Code\ Coverage --settings tests/coverletArgs.runsettings --verbosity minimal -m:1
run workflow-tests node --test .github/scripts/publish-openapi-report.test.cjs
(
    cd Castlumora.Web
    run web-install npm ci
    run web-typecheck npm run typecheck
    run web-lint npm run lint
    run web-tests npm test
    run web-build npm run build
)
assert_snapshot
printf 'PASS: %s, SDK%s, %s\nLogs: %s\n' "$head" "$sdk" "$(node --version)" "$logdir"
