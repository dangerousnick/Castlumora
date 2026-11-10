# Required local publishing checks

Install with `./scripts/install-git-hooks.sh` in every checkout. This sets only
repository-local core.hooksPath=.githooks and refuses to overwrite another hooks
configuration. Git does not install hooks when cloning; AGENTS.md also requires the
process for every future Codex session. Client hooks are not remote branch protection;
maintainers must still require GitHub CI/review. Do not bypass the hook.

Prerequisites: Bash, Git, .NET10 SDK selected by global.json, Node24 LTS, npm,
Python3, Linux/Windows x64, outbound package access and enough disk/memory to build
and start disposable test servers. On this workspace put /tmp/castlumora-dotnet on
PATH. Commit the intended working tree locally before validation; the gate refuses
tracked changes or untracked source files and validates only checked-out HEAD.

Run `./scripts/validate-before-push.sh`. A normal git push invokes it again. Every
step is required and failures stop the command before network publication:

- Restore Jellyfin.sln; full `dotnet format --verify-no-changes --verbosity diagnostic`
  (same check as Format CI, with additional diagnostic output). No project/file filter.
  Workspace-load warnings are a failure, not evidence that all projects were checked.
- `dotnet build Jellyfin.Server -o <temporary>/abi-head` in Debug, matching ABI HEAD CI.
  Its temporary output is removed after success; logs remain.
- Release build of Jellyfin.sln, then full Release solution tests with
  --collect:"XPlat Code Coverage" and tests/coverletArgs.runsettings, matching Tests CI.
- Verified OS-specific runtime bundle and bundler tests, so real-executable cases do
  not silently skip. Optional CASTLUMORA_TEST_YTDLP_BUNDLE reuses verified cached bytes;
  its digest/size are still checked.
- Node workflow comment-handler tests; npm ci/typecheck/lint/test/build for Web.

Logs are retained in a printed /tmp/castlumora-prepush.* directory. Parallelism is
bounded for resource use; analyzers, tests and warnings-as-errors are not disabled.
Missing tools, network/restore failure, unsupported platform or incomplete workspace
loading block publication. There is no skip flag or successful-check cache.

Linux execution cannot certify Windows/macOS, physical Mint, GPU/tuner hardware,
remote ABI base/head comparison, OpenAPI Docker report or packaged browser workflows.
Those remain required applicable CI/preview checks; never claim they ran locally.

## Incident corrected

GitHub Format42855077268/job129965793560 and ABI42855076911/HEAD job129965792607
both failed with SA1508 at Jellyfin.Api/Castlumora/YtDlpRuntime.cs392: a blank line
before a closing brace. Removed the blank line. Release-only builds and scoped
formatter invocations were insufficient evidence; the gate now covers full Debug
analyzers and full-solution formatting. No GitHub check has been disabled or weakened.

## Additional Windows test investigation

Windows Tests42855077283/job129965793527 also failed four TLS fixture cases;
Linux/macOS passed. Both trusted and untrusted fixtures failed before meaningful
certificate validation with unexpected TLS EOF. The tests had used private keys
straight from CreateSelfSigned. A shared fixture now exports/reimports PKCS12 with
DefaultKeySet (no PersistKeySet), server-auth EKU and key usage, making the key
usable by Schannel as well as OpenSSL. Server task failures are observed so a
future native handshake error is not hidden behind client EOF. All success,
hostname-rejection and untrusted-certificate assertions remain; no tests are skipped.
Windows confirmation requires CI because this executor is Linux; do not claim it
passed locally. Six isolated hook tests include source mutation during validation.
The gate checks its committed snapshot before every stage and at completion.

## Storage failure and recovery

A full local run passed full-solution formatting and Debug server build, then
failed the Release solution build with ENOSPC on this8GB filesystem. It did not
run subsequent required stages and did not authorize a push. Logs are retained
at /tmp/castlumora-prepush.v1RcuxL8. Stopped old generated extractions and failed
bin outputs were removed; source, test data, logs, installed packages and all saved
archives were preserved. Only the redundant NuGet HTTP download cache was cleared.

The gate now uses MSBuild's supported CreateHardLinksForCopyLocalIfPossible=true
storage optimization for identical dependency copies. No source fixture content
is hard-linked, no runtime assets are excluded, and checks/assertions are unchanged.
The filesystem was probed for hard-link support. Disk headroom is checked before
builds and tests (minimum2GiB); failure still blocks publication. Nine isolated
bare-remote tests include disk failure and preservation of existing hook settings.
Final frozen-source validation must still pass in full; earlier partial runs are
not a substitute. Windows TLS fixture confirmation still requires Windows CI.
