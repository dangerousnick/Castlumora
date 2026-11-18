import type { APIRequestContext } from '@playwright/test';

// One transport-only retry for idempotent preview reads. Playwright retries only
// ECONNRESET, never HTTP errors; mutations continue to use their original requests.
export async function readPreview(
  request: APIRequestContext,
  url: string,
  options?: Parameters<APIRequestContext['get']>[1],
) {
  try {
    return await request.get(url, { ...options, maxRetries: 1 });
  } catch (error) {
    // Playwright's call log can include the fixture's Authorization header.
    const message = error instanceof Error ? error.message : String(error);
    throw new Error(
      message
        .replace(/authorization\s*:[^\r\n]*/gi, 'Authorization: [redacted]')
        .replace(/[a-f0-9]{32,}/gi, '[redacted-id-or-secret]'),
    );
  }
}
