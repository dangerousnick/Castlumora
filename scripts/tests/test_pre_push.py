"""Exercise the real hook against a private bare remote; fake compilers only test blocking."""
import os
from pathlib import Path
import shutil
import subprocess
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
        for name in ["scripts/validate-before-push.sh", "scripts/install-git-hooks.sh", ".githooks/pre-push"]:
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
if [ "$1" = test ] && [ "${CASTLUMORA_GATE_TEST_FAILURE:-}" = tests ]; then exit 1; fi
''')
        self.tool("node", 'if [ "$1" = --version ]; then echo v24.22.1; fi\n')
        self.tool("npm", "")
        self.tool("python3", 'if [ "$1" = -c ] && [ "${CASTLUMORA_GATE_TEST_FAILURE:-}" = disk ]; then exit 1; fi')

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

    def test_insufficient_disk_prevents_remote_update(self):
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
        for command in ["dotnet format --verify-no-changes --verbosity diagnostic",
                        "dotnet build Jellyfin.Server", "dotnet build Jellyfin.sln --configuration Release",
                        "dotnet test Jellyfin.sln --configuration Release --collect:XPlat Code Coverage",
                        "npm run typecheck", "npm run lint", "npm test", "npm run build"]:
            self.assertIn(command, commands)


if __name__ == "__main__":
    unittest.main()
