"""Exercise the real hook against a private bare remote; fake compilers only test blocking."""
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

SOURCE = Path(__file__).resolve().parents[2]


class PrePushTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="castlumora-hook-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.repo = self.root / "repo"
        self.remote = self.root / "remote.git"
        self.tools = self.root / "tools"
        self.tools.mkdir()
        self.env = dict(os.environ, PATH=str(self.tools) + os.pathsep + os.environ["PATH"])
        self.env["CASTLUMORA_GATE_TEST_LOG"] = str(self.root / "commands")
        self.git("init", "--bare", str(self.remote), cwd=self.root)
        self.git("init", str(self.repo), cwd=self.root)
        self.git("config", "user.name", "Fixture")
        self.git("config", "user.email", "fixture@example.invalid")
        for name in ["scripts/validate-before-push.sh", "scripts/install-git-hooks.sh", ".githooks/pre-push", ".github/scripts/change_scope.py"]:
            target = self.repo / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(SOURCE / name, target)
        (self.repo / "Castlumora.Web").mkdir()
        (self.repo / "Castlumora.Web/package.json").write_text("{}")
        self.git("add", ".")
        self.git("commit", "-m", "owned fixture")
        self.git("config", "core.hooksPath", ".githooks")
        self.git("remote", "add", "origin", str(self.remote))
        self.tool("dotnet", '''
if [ "$1" = --version ]; then echo 10.0.403; exit 0; fi
if [ "$1" = format ]; then
  if [ "${CASTLUMORA_GATE_TEST_FAILURE:-}" = mutate ]; then echo dirty >> Castlumora.Web/package.json; fi
  if [ "${CASTLUMORA_GATE_TEST_FAILURE:-}" = workspace ]; then
    echo 'Warnings were encountered while loading the workspace.'; exit 0
  fi
  if [ "${CASTLUMORA_GATE_TEST_FAILURE:-}" = format ]; then exit 1; fi
fi
if [ "$1" = build ] && [ "$2" = Jellyfin.Server ] && [ "${CASTLUMORA_GATE_TEST_FAILURE:-}" = build ]; then exit 1; fi
if [ "$1" = build ] && [ "${CASTLUMORA_GATE_TEST_FAILURE:-}" = disk ]; then echo "No space left on device" >&2; exit 1; fi
if [ "$1" = test ] && [ "${CASTLUMORA_GATE_TEST_FAILURE:-}" = tests ]; then exit 1; fi
''')
        self.tool("node", 'if [ "$1" = --version ]; then echo v24.22.1; fi\n')
        self.tool("ffmpeg", "echo native-hook-fixture")
        self.env["CASTLUMORA_TEST_FFMPEG"] = str(self.tools / "ffmpeg")
        self.tool("ffprobe", "echo native-hook-fixture")
        self.env["CASTLUMORA_TEST_FFPROBE"] = str(self.tools / "ffprobe")
        self.tool("npm", 'if [ "${CASTLUMORA_GATE_TEST_FAILURE:-}" = web-tests ] && [ "$1" = test ]; then exit 1; fi')
        self.tool("python3", 'if [ "${CASTLUMORA_GATE_TEST_FAILURE:-}" = policy ] && [ "$1" = -m ]; then exit 1; fi\nif [ "$1" = .github/scripts/change_scope.py ]; then exec '+sys.executable+' "$@"; fi\nif [ "$1" = -c ]; then echo "Free bytes: 1024; informational only"; fi')

    def test_documentation_only_push_does_not_rebuild_application(self):
        self.git("push", "origin", "HEAD:refs/heads/development")
        (self.root / "commands").write_text("")
        (self.repo / "README.md").write_text("# Owned documentation\n")
        self.git("add", ".")
        self.git("commit", "-m", "documentation")
        self.git("push", "origin", "HEAD:refs/heads/development")
        commands = (self.root / "commands").read_text()
        self.assertIn("--check-docs", commands)
        self.assertNotIn("dotnet", commands)
        self.assertNotIn("npm", commands)

    def test_final_docs_commit_cannot_hide_unpublished_application_changes(self):
        self.git("push", "origin", "HEAD:refs/heads/development")
        (self.repo / "backend.cs").write_text("unpublished backend change")
        self.git("add", ".")
        self.git("commit", "-m", "application")
        (self.repo / "README.md").write_text("# Final docs\n")
        self.git("add", ".")
        self.git("commit", "-m", "docs")
        self.env["CASTLUMORA_GATE_TEST_FAILURE"] = "tests"
        result = self.git("push", "origin", "HEAD:refs/heads/development", check=False)
        self.assertNotEqual(result.returncode, 0)

    def docs_blocked(self, text):
        self.git("push", "origin", "HEAD:refs/heads/development")
        base = self.git("rev-parse", "HEAD").stdout.strip()
        (self.repo / "README.md").write_text(text)
        self.git("add", ".")
        self.git("commit", "-m", "bad docs")
        result = self.git("push", "origin", "HEAD:refs/heads/development", check=False)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.git("--git-dir="+str(self.remote), "rev-parse", "refs/heads/development").stdout.strip(), base)

    def test_documentation_conflict_blocks_push(self):
        self.docs_blocked("<<<<<<< unfinished\n")

    def test_documentation_whitespace_error_blocks_push(self):
        self.docs_blocked("unfinished trailing space \n")

    def test_policy_changes_require_application_validation(self):
        self.git("push", "origin", "HEAD:refs/heads/development")
        (self.repo / "AGENTS.md").write_text("policy change\n")
        self.git("add", ".")
        self.git("commit", "-m", "policy")
        self.env["CASTLUMORA_GATE_TEST_FAILURE"] = "policy"
        self.assertNotEqual(self.git("push", "origin", "HEAD:refs/heads/development", check=False).returncode, 0)

    def test_symlink_cannot_masquerade_as_documentation(self):
        self.git("push", "origin", "HEAD:refs/heads/development")
        outside = self.root / "outside"
        outside.write_text("owned fixture")
        (self.repo / "README.md").symlink_to(outside)
        self.git("add", ".")
        self.git("commit", "-m", "symlink")
        self.env["CASTLUMORA_GATE_TEST_FAILURE"] = "tests"
        self.assertNotEqual(self.git("push", "origin", "HEAD:refs/heads/development", check=False).returncode, 0)

    def test_untrusted_base_option_defaults_to_application(self):
        result = subprocess.run([sys.executable, ".github/scripts/change_scope.py", "--base=--output=untrusted"], cwd=self.repo, text=True, capture_output=True, check=True)
        self.assertEqual(result.stdout.strip(), "application")
        self.assertFalse((self.repo / "untrusted").exists())

    def test_changed_merge_target_cannot_reuse_docs_only_validation(self):
        base = self.git("rev-parse", "HEAD").stdout.strip()
        branch = self.git("branch", "--show-current").stdout.strip()
        self.git("checkout", "-b", "changed-target")
        (self.repo / "server.cs").write_text("target branch application change")
        self.git("add", ".")
        self.git("commit", "-m", "target change")
        target = self.git("rev-parse", "HEAD").stdout.strip()
        self.git("checkout", branch)
        (self.repo / "README.md").write_text("# Docs\n")
        self.git("add", ".")
        self.git("commit", "-m", "docs")
        result = subprocess.run([sys.executable, ".github/scripts/change_scope.py", "--base", base, "--target-base", target], cwd=self.repo, text=True, capture_output=True, check=True)
        self.assertEqual(result.stdout.strip(), "application")

    def test_frontend_only_runs_client_checks_without_rebuilding_backend(self):
        self.git("push", "origin", "HEAD:refs/heads/development")
        (self.root / "commands").write_text("")
        (self.repo / "Castlumora.Web/package.json").write_text('{"client":true}')
        self.git("add", ".")
        self.git("commit", "-m", "client")
        self.git("push", "origin", "HEAD:refs/heads/development")
        commands = (self.root / "commands").read_text()
        self.assertNotIn("dotnet", commands)
        self.assertIn("npm run typecheck", commands)
        self.assertIn("npm test", commands)

    def test_frontend_failure_blocks_client_only_push(self):
        self.git("push", "origin", "HEAD:refs/heads/development")
        (self.repo / "Castlumora.Web/package.json").write_text('{"client":false}')
        self.git("add", ".")
        self.git("commit", "-m", "client")
        self.env["CASTLUMORA_GATE_TEST_FAILURE"] = "web-tests"
        self.assertNotEqual(self.git("push", "origin", "HEAD:refs/heads/development", check=False).returncode, 0)

    def test_executable_validation_script_uses_policy_checks(self):
        self.git("push", "origin", "HEAD:refs/heads/development")
        (self.root / "commands").write_text("")
        script = self.repo / "scripts/validate-before-push.sh"
        script.write_text(script.read_text()+"\n# Owned policy-only edit\n")
        self.git("add", ".")
        self.git("commit", "-m", "validation policy")
        self.git("push", "origin", "HEAD:refs/heads/development")
        commands = (self.root / "commands").read_text()
        self.assertNotIn("dotnet", commands)
        self.assertIn("python3 -m unittest", commands)
        self.assertIn("npm test", commands)

    def test_missing_native_inspection_fixture_blocks_push(self):
        self.env["CASTLUMORA_TEST_FFPROBE"] = str(self.root / "missing-native-probe")
        self.blocked("inspectionFixture")

    def git(self, *args, cwd=None, check=True):
        return subprocess.run(["git", *args], cwd=cwd or self.repo, env=self.env,
                              text=True, capture_output=True, check=check)

    def tool(self, name, body):
        path = self.tools / name
        path.write_text('#!/bin/sh\nprintf "%s\\n" "' + name + ' $*" >> "$CASTLUMORA_GATE_TEST_LOG"\n' + body + '\nexit 0\n')
        path.chmod(0o755)

    def blocked(self, mode):
        self.env["CASTLUMORA_GATE_TEST_FAILURE"] = mode
        result = self.git("push", "origin", "HEAD:refs/heads/development", check=False)
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(self.git("--git-dir=" + str(self.remote), "show-ref", check=False).returncode, 1)

    def test_format_failure_prevents_remote_update(self):
        self.blocked("format")

    def test_workspace_warning_prevents_remote_update_even_with_zero_exit(self):
        self.blocked("workspace")

    def test_source_mutation_during_checks_prevents_remote_update(self):
        self.blocked("mutate")

    def test_actual_build_disk_failure_prevents_remote_update(self):
        self.blocked("disk")

    def test_installer_preserves_existing_default_hook(self):
        self.git("config", "--unset", "core.hooksPath")
        hook = self.repo / ".git/hooks/pre-push"
        hook.write_text("owned existing hook")
        result = subprocess.run(["bash", "scripts/install-git-hooks.sh"], cwd=self.repo,
                                env=self.env, capture_output=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(hook.read_text(), "owned existing hook")
        self.assertNotEqual(self.git("config", "--get", "core.hooksPath", check=False).returncode, 0)

    def test_installer_preserves_existing_hooks_path(self):
        self.git("config", "core.hooksPath", "existing-hooks")
        result = subprocess.run(["bash", "scripts/install-git-hooks.sh"], cwd=self.repo,
                                env=self.env, capture_output=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.git("config", "--get", "core.hooksPath").stdout.strip(), "existing-hooks")

    def test_debug_build_failure_prevents_remote_update(self):
        self.blocked("build")

    def test_test_failure_prevents_remote_update(self):
        self.blocked("tests")

    def test_success_runs_every_stage_and_publishes_checked_head(self):
        self.git("push", "origin", "HEAD:refs/heads/development")
        expected = self.git("rev-parse", "HEAD").stdout.strip()
        actual = self.git("--git-dir=" + str(self.remote), "rev-parse", "refs/heads/development").stdout.strip()
        self.assertEqual(actual, expected)
        commands = (self.root / "commands").read_text()
        # Disk observations must not preempt real builds/tests using an unrelated backup threshold.
        self.assertNotIn("5368709120", commands)
        self.assertIn("informational only", commands)
        for command in ["dotnet format --verify-no-changes --verbosity diagnostic",
                        "dotnet build Jellyfin.Server", "dotnet build Jellyfin.sln --configuration Release",
                        "dotnet test Jellyfin.sln --configuration Release --collect:XPlat Code Coverage",
                        "npm run typecheck", "npm run lint", "npm test", "npm run build"]:
            self.assertIn(command, commands)


if __name__ == "__main__":
    unittest.main()
