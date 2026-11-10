// Trusted workflow code only; downloaded report bytes are never executed.
module.exports = async function publishOpenApiReport({ github, context, core, env = process.env, fs = require('node:fs') }) {
  const issue_number = Number(env.PR_NUMBER);
  if (!Number.isSafeInteger(issue_number) || issue_number <= 0) {
    throw new Error('A valid pull request number is required');
  }
  const { owner, repo } = context.repo;
  const { data: pr } = await github.rest.pulls.get({ owner, repo, pull_number: issue_number });
  if (pr.state !== 'open' || pr.head.sha !== env.REPORT_SHA) {
    core.notice('Skipping an outdated or closed pull request report');
    return;
  }
  const marker = '<!-- castlumora-openapi-report -->';
  // Treat the untrusted report artifact only as bounded comment text, never code.
  if (fs.statSync(env.REPORT_PATH).size > 1024 * 1024) {
    throw new Error('OpenAPI report exceeds the supported size');
  }
  const report = fs.readFileSync(env.REPORT_PATH, 'utf8');
  const body = marker + '\n' + report.slice(0, 60000)
    + (report.length > 60000 ? '\nReport truncated; see the OpenAPI Check artifact.' : '');
  const comments = await github.paginate(github.rest.issues.listComments, { owner, repo, issue_number, per_page: 100 });
  const previous = comments.find(c => c.user?.login === 'github-actions[bot]' && c.body?.includes(marker));
  if (previous) {
    await github.rest.issues.updateComment({ owner, repo, comment_id: previous.id, body });
  } else {
    await github.rest.issues.createComment({ owner, repo, issue_number, body });
  }
};
