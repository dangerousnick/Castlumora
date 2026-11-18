// @vitest-environment node
import { afterEach, expect, it } from 'vitest';
import { createServer, type Server, type RequestListener } from 'node:http';
import type { AddressInfo } from 'node:net';
import { request, type APIRequestContext } from '@playwright/test';
import { readPreview } from '../../browser/preview-read';

let server: Server | undefined;
let client: APIRequestContext | undefined;
const secret = 'a'.repeat(32);
async function fixture(handler: RequestListener) {
  server = createServer(handler);
  await new Promise<void>((resolve) => server!.listen(0, '127.0.0.1', resolve));
  client = await request.newContext({
    extraHTTPHeaders: { Authorization: `MediaBrowser Token="${secret}"` },
  });
  return `http://127.0.0.1:${(server.address() as AddressInfo).port}/owned-fixture`;
}
afterEach(async () => {
  await client?.dispose();
  server?.closeAllConnections();
  if (server) await new Promise<void>((resolve) => server!.close(() => resolve()));
  server = undefined;
  client = undefined;
});

it('recovers one reset and still returns the real expected 404', async () => {
  let calls = 0;
  const url = await fixture((incoming, response) => {
    if (++calls === 1) incoming.socket.destroy();
    else {
      response.writeHead(404);
      response.end('User not found');
    }
  });
  const response = await readPreview(client!, url);
  expect(response.status()).toBe(404);
  expect(await response.text()).toBe('User not found');
  expect(calls).toBe(2);
});

it('fails after the single reset retry and redacts the fixture credential', async () => {
  let calls = 0;
  const url = await fixture((incoming) => {
    calls++;
    incoming.socket.destroy();
  });
  let failure: unknown;
  try {
    await readPreview(client!, url);
  } catch (error) {
    failure = error;
  }
  expect(failure).toBeInstanceOf(Error);
  expect((failure as Error).message).toMatch(/ECONNRESET/);
  expect((failure as Error).message).not.toContain(secret);
  expect(calls).toBe(2);
});

it.each([401, 404, 500])('never retries or conceals an HTTP %i response', async (status) => {
  let calls = 0;
  const url = await fixture((_incoming, response) => {
    calls++;
    response.writeHead(status);
    response.end('unchanged');
  });
  const response = await readPreview(client!, url);
  expect(response.status()).toBe(status);
  expect(await response.text()).toBe('unchanged');
  expect(calls).toBe(1);
});
