import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, NexusApi } from '../api/nexus-api';
import type { Run } from '../api/types';
import { RunStream } from './run-stream';

const run = (patch: Partial<Run> = {}) =>
  ({ id: 'r1', status: 'running', content: '', lastSequence: 0, errorCode: null, ...patch }) as Run;

const event = (sequence: number, type: string, patch: Record<string, unknown> = {}) => ({
  version: 1,
  runId: 'r1',
  sequence,
  type,
  status: 'running',
  errorCode: null,
  ...patch,
});

function sse(...events: object[]) {
  const body = events.map((data) => `event: run\ndata: ${JSON.stringify(data)}\n\n`).join('');
  return new Response(
    new ReadableStream({
      start(controller) {
        controller.enqueue(new TextEncoder().encode(body));
        controller.close();
      },
    }),
  );
}

function setup(api: Partial<Record<'events' | 'run', ReturnType<typeof vi.fn>>>) {
  TestBed.configureTestingModule({ providers: [{ provide: NexusApi, useValue: api }] });
  const stream = TestBed.inject(RunStream);
  const content: string[] = [];
  const connection: string[] = [];
  const observer = {
    content: (value: string) => content.push(value),
    status: () => undefined,
    connection: (value: string) => connection.push(value),
  };
  return { stream, content, connection, observer };
}

describe('run stream', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('applies snapshots and deltas once, in sequence, and returns the settled run', async () => {
    const events = vi
      .fn()
      .mockResolvedValue(
        sse(
          event(1, 'snapshot', { delta: 'Hel' }),
          event(2, 'delta', { delta: 'lo' }),
          event(2, 'delta', { delta: '重複' }),
          event(1, 'delta', { delta: '過期' }),
          event(3, 'status', { status: 'completed' }),
        ),
      );
    const settled = run({ status: 'completed', content: 'Hello', lastSequence: 3 });
    const fetchRun = vi.fn().mockResolvedValue(settled);
    const f = setup({ events, run: fetchRun });

    const result = await f.stream.follow(run(), new AbortController().signal, f.observer);

    expect(result).toBe(settled);
    expect(events).toHaveBeenCalledOnce();
    expect(events.mock.calls[0].slice(0, 2)).toEqual(['r1', 0]);
    expect(f.content.at(-1)).toBe('Hello');
    expect(f.content.join('|')).not.toContain('重複');
  });

  it('resumes from the server state after the stream ends early', async () => {
    const events = vi
      .fn()
      .mockResolvedValueOnce(sse(event(1, 'delta', { delta: 'A' })))
      .mockResolvedValueOnce(
        sse(event(2, 'delta', { delta: 'B' }), event(3, 'status', { status: 'completed' })),
      );
    const fetchRun = vi
      .fn()
      .mockResolvedValueOnce(run({ content: 'A', lastSequence: 1 }))
      .mockResolvedValueOnce(run({ status: 'completed', content: 'AB', lastSequence: 3 }));
    const f = setup({ events, run: fetchRun });

    const result = await f.stream.follow(run(), new AbortController().signal, f.observer);

    expect(result.content).toBe('AB');
    expect(events.mock.calls.map((call) => call[1])).toEqual([0, 1]);
    expect(f.connection).toEqual(['connected', 'reconnecting', 'connected']);
    expect(f.content.at(-1)).toBe('AB');
  });

  it('stops on a permanent client error without polling the run again', async () => {
    const events = vi.fn().mockRejectedValue(new ApiError(404, 'run_not_found'));
    const fetchRun = vi.fn();
    const f = setup({ events, run: fetchRun });

    await expect(
      f.stream.follow(run(), new AbortController().signal, f.observer),
    ).rejects.toBeInstanceOf(ApiError);
    expect(fetchRun).not.toHaveBeenCalled();
  });

  it('returns a run that is already settled without opening the event stream', async () => {
    const events = vi.fn();
    const f = setup({ events, run: vi.fn() });
    const done = run({ status: 'completed', content: '完成' });

    expect(await f.stream.follow(done, new AbortController().signal, f.observer)).toBe(done);
    expect(events).not.toHaveBeenCalled();
    expect(f.content).toEqual(['完成']);
  });
});
