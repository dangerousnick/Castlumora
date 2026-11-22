// @vitest-environment node
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { it } from 'vitest';

it('rejects unsafe Live TV fixtures before configuration writes', () => {
  execFileSync('python3', ['browser/test_live_fixture_guard.py'], {
    cwd: fileURLToPath(new URL('../', import.meta.url)),
    timeout: 25000,
    encoding: 'utf8',
  });
}, 30000);
