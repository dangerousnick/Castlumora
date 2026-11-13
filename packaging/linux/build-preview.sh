#!/usr/bin/env bash
set -euo pipefail
repo=$(git rev-parse --show-toplevel)
cd "$repo"
if [[ -n $(git status --porcelain) ]]; then echo 'Commit source changes first; preview provenance requires a clean Git tree.' >&2; exit 2; fi
sha=$(git rev-parse HEAD)
epoch=$(git show -s --format=%ct "$sha")
output=${CASTLUMORA_BUILD_DIR:-$repo/dist/preview}
output=$(realpath -m -- "$output")
if [[ -e $output ]]; then echo "Build output already exists: $output. Choose a fresh CASTLUMORA_BUILD_DIR." >&2; exit 2; fi
name="Castlumora-1.0-dev-${sha:0:12}-linux-x64"
stage="$output/$name"
mkdir -p "$stage/server" "$stage/web" "$stage/licenses"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
node --test .github/scripts/*.test.cjs
python3 -m unittest discover -s packaging/yt-dlp -p 'test_*.py'
(cd Castlumora.Web; npm ci; npm run typecheck; npm run lint; npm test; VITE_LEGACY_WEB_PATH=web/legacy/ npm run build)
dotnet publish Jellyfin.Server/Jellyfin.Server.csproj --configuration Release --runtime linux-x64 --self-contained true --nologo -m:1 -p:PublishSingleFile=false -p:ContinuousIntegrationBuild=true -o "$stage/server"
python3 packaging/yt-dlp/bundle.py --platform linux-x64 --system-root "$stage/server"
cp -a Castlumora.Web/dist/. "$stage/web/"
mkdir "$stage/web/legacy"
cp packaging/linux/legacy-help.html "$stage/web/legacy/index.html"
cp packaging/linux/start-castlumora packaging/linux/Castlumora.desktop "$stage/"
cp INSTALL_TEST_BUILD.md "$stage/"
cp docs/TEST_BUILD_CHECKLIST.md "$stage/"
cp LICENSE CONTRIBUTORS.md "$stage/licenses/"
cp -a packaging/licenses/. "$stage/licenses/"
python3 packaging/linux/collect-licenses.py Jellyfin.Server/obj/project.assets.json "$stage/licenses/nuget"
cp FEATURE_PARITY.md PRODUCT_ROADMAP.md DEVELOPMENT_PROGRESS.md "$stage/"
cp Castlumora.Web/public/THIRD_PARTY_NOTICES.txt "$stage/licenses/WEB_DEPENDENCIES.txt"
cp -a branding "$stage/branding"
mkdir -p "$stage/docs"
cp docs/CASTLUMORA_IMPORT_FAILURES.md docs/verification-single-video-import-1.4.md docs/CASTLUMORA_SINGLE_VIDEO_IMPORT.md docs/CASTLUMORA_IMPORT_QUEUE.md docs/CASTLUMORA_IMPORT_MEDIA_INSPECTION.md "$stage/docs/"
cp docs/CASTLUMORA_IMPORT_FAILURES.md docs/verification-single-video-import-1.4.md docs/CASTLUMORA_SINGLE_VIDEO_IMPORT.md docs/CASTLUMORA_IMPORT_QUEUE.md docs/CASTLUMORA_IMPORT_MEDIA_INSPECTION.md docs/verification-platform-runtime-1.4.md docs/CASTLUMORA_PLATFORM_SUPPORT.md docs/CASTLUMORA_READER_AND_SHELVES.md docs/CASTLUMORA_SKIPPER.md docs/CASTLUMORA_REGISTRATION.md docs/CASTLUMORA_CREATOR_SIDECAR.md docs/CASTLUMORA_METADATA_MANAGEMENT.md docs/CASTLUMORA_IMPORTER_RUNTIME.md docs/CASTLUMORA_DVR.md "$stage/"
printf '{"product":"Castlumora","version":"1.0 development preview","commit":"%s","platform":"linux-x64","selfContainedDotnet":true,"legacyWebIncluded":false,"importerImplemented":false,"singleVideoImportAvailable":true,"importerStage":"authorized-single-direct-mp4","managedYtDlpRuntimeBundled":true,"metadataProviderImplemented":false,"creatorSidecarImplemented":true,"metadataManagementStage":"selected-item-local"}\n' "$sha" > "$stage/BUILD_INFO.json"
cp "$stage/BUILD_INFO.json" "$stage/web/BUILD_INFO.json"
(cd "$output"; tar --sort=name --mtime="@$epoch" --owner=0 --group=0 --numeric-owner -czf "$name.tar.gz" "$name"; sha256sum "$name.tar.gz" > "$name.tar.gz.sha256")
printf 'Package: %s/%s.tar.gz\nSource: %s\n' "$output" "$name" "$sha"
