import { describe, expect, it } from 'vitest';
import { ApiError } from '../api/api-transport';
import { jsonEvents } from './json-events';

describe('typed JSON event stream', () => {
  it('decodes snapshots split across chunks and honors a revocation status without displaying raw errors', async () => {
    const encoder = new TextEncoder();
    let cancelled = false;
    const response = new Response(
      new ReadableStream({
        start(controller) {
          controller.enqueue(encoder.encode('event: snapshot\ndata: {"online'));
          controller.enqueue(
            encoder.encode(
              'Users":2}\n\nevent: error\ndata: {"status":403,"code":"login_method_disabled","message":"private-password"}\n\n',
            ),
          );
        },
        cancel() {
          cancelled = true;
        },
      }),
    );
    const events = jsonEvents<{ onlineUsers: number }>(
      response,
      'snapshot',
      new AbortController().signal,
    );
    expect((await events.next()).value).toEqual({ onlineUsers: 2 });
    try {
      await events.next();
      expect.unreachable('Revocation must end the event stream');
    } catch (error) {
      expect(error).toBeInstanceOf(ApiError);
      expect((error as ApiError).status).toBe(403);
      expect((error as Error).message).not.toContain('private-password');
    }
    expect(cancelled).toBe(true);
  });
});
