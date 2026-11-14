const { test } = require('node:test');
const assert = require('node:assert/strict');
const publish = require('./publish-openapi-report.cjs');

function fixture(overrides = {}) {
  const calls = [];
  const comments = overrides.comments ?? [];
  const args = {
    env: { PR_NUMBER: '3', REPORT_SHA: 'current-sha', REPORT_PATH: '/report.md' },
    context: { repo: { owner: 'dangerousnick', repo: 'Castlumora' } },
    core: { notice: message => calls.push(['notice', message]) },
    fs: {
      statSync: () => ({ size: overrides.size ?? 100 }),
      readFileSync: () => overrides.report ?? '## API changes\nA report, not executable code.',
    },
    github: {
      rest: {
        pulls: { get: async () => ({ data: overrides.pr ?? { state: 'open', head: { sha: 'current-sha' } } }) },
        issues: {
          listComments: () => {},
          updateComment: async payload => calls.push(['update', payload]),
          createComment: async payload => calls.push(['create', payload]),
        },
      },
      paginate: async () => comments,
    },
  };
  return { args, calls };
}

test('publishes report text to the verified PR without executing artifact contents', async () => {
  const report = '${process.exit(99)}\n<script>untrusted</script>';
  const { args, calls } = fixture({ report });
  await publish(args);
  assert.equal(calls[0][0], 'create');
  assert.equal(calls[0][1].issue_number, 3);
  assert.equal(calls[0][1].body, '<!-- castlumora-openapi-report -->\n' + report);
});

test('updates only the marked GitHub Actions bot comment', async () => {
  const marker = '<!-- castlumora-openapi-report -->';
  const { args, calls } = fixture({ comments: [
    { id: 1, user: { login: 'owner' }, body: marker },
    { id: 2, user: { login: 'github-actions[bot]' }, body: 'Unrelated report' },
    { id: 3, user: { login: 'github-actions[bot]' }, body: marker },
  ] });
  await publish(args);
  assert.equal(calls[0][0], 'update');
  assert.equal(calls[0][1].comment_id, 3);
});

test('skips closed and superseded PRs without consuming artifact contents or publishing', async () => {
  for (const pr of [{ state: 'closed', head: { sha: 'current-sha' } }, { state: 'open', head: { sha: 'newer-sha' } }]) {
    const { args, calls } = fixture({ pr });
    args.fs.statSync = () => { throw new Error('Must not read stale report'); };
    await publish(args);
    assert.deepEqual(calls.map(c => c[0]), ['notice']);
  }
});

test('rejects invalid PR identifiers and oversized artifacts', async () => {
  for (const value of ['', 'null', '-3', '3; do something', '1.5']) {
    const { args, calls } = fixture();
    args.env.PR_NUMBER = value;
    await assert.rejects(publish(args), /valid pull request/);
    assert.equal(calls.length, 0);
  }
  const { args, calls } = fixture({ size: 1048577 });
  await assert.rejects(publish(args), /exceeds the supported size/);
  assert.equal(calls.length, 0);
});

test('bounds large comments and points readers to the complete artifact', async () => {
  const { args, calls } = fixture({ report: 'x'.repeat(70000), size: 70000 });
  await publish(args);
  assert.ok(calls[0][1].body.length < 65536);
  assert.match(calls[0][1].body, /Report truncated/);
});

// Exercise the actual workflow script, including intentionally absent artifacts.
const fs = require('node:fs');
const vm = require('node:vm');
const workflow = fs.readFileSync('.github/workflows/openapi-workflow-run.yml', 'utf8');
const generationScript = workflow.split('        script: |\n')[1].split('    - name: Get Pull Request Number')[0]
  .split('\n').map(line => line.replace(/^          /, '')).join('\n');

async function reportExpected(jobs) {
  const notices = [];
  const run = vm.runInNewContext(`(async () => { ${generationScript} })`, {
    github: { rest: { actions: { listJobsForWorkflowRun: {} } },
      paginate: async (_method, args) => {
        assert.equal(args.run_id, 123);
        assert.equal(args.filter, 'latest');
        return jobs;
      } },
    context: { repo: { owner: 'dangerousnick', repo: 'Castlumora' }, payload: { workflow_run: { id: 123 } } },
    core: { notice: message => notices.push(message) },
  });
  return { expected: await run(), notices };
}

test('skips artifact consumption only when report generation was intentionally skipped', async () => {
  const skipped = await reportExpected([{ name: 'Generate Report', conclusion: 'skipped' }]);
  assert.equal(skipped.expected, false);
  assert.equal(skipped.notices.length, 1);
  assert.equal((await reportExpected([{ name: 'Generate Report', conclusion: 'success' }])).expected, true);
  for (const jobs of [[], [{ name: 'Generate Report', conclusion: 'failure' }],
    [{ name: 'Generate Report', conclusion: 'cancelled' }],
    [{ name: 'Generate Report', conclusion: 'success' }, { name: 'Generate Report', conclusion: 'skipped' }]]) {
    await assert.rejects(reportExpected(jobs));
  }
});

test('artifact producer and cross-run consumer retain strict matching contracts', () => {
  const producer = fs.readFileSync('.github/workflows/openapi-pull-request.yml', 'utf8');
  assert.match(producer, /needs:\n    - head\n    - base/);
  assert.match(producer, /uses: actions\/upload-artifact@v4\n      with:\n        name: openapi-report\n        path: \/tmp\/openapi-report\/openapi-report.md\n        if-no-files-found: error/);
  assert.match(workflow, /needs.metadata.outputs.report_expected == 'true'/);
  assert.match(workflow, /uses: actions\/download-artifact@v4\n      with:\n        name: openapi-report/);
  assert.match(workflow, /run-id: \$\{\{ github.event.workflow_run.id }}/);
  assert.match(workflow, /github-token: \$\{\{ secrets.GITHUB_TOKEN }}/);
  assert.doesNotMatch(workflow, /continue-on-error/);
});
