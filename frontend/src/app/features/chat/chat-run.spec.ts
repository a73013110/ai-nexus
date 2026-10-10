import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../core/api/nexus-api';
import type { RunDto } from '../../core/api/schema';
import { RunStream, type StreamObserver } from '../../core/stream/run-stream';
import { ChatRun } from './chat-run';

const run = (id: string, status = 'running') => ({ id, status, content: '' }) as RunDto;

function setup(
  follow: (run: RunDto, signal: AbortSignal, observer: StreamObserver) => Promise<RunDto>,
) {
  TestBed.configureTestingModule({
    providers: [{ provide: RunStream, useValue: { follow: vi.fn(follow) } }],
  });
  return TestBed.inject(ChatRun);
}

describe('chat run', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('publishes streamed text and settles the finished run', async () => {
    const chat = setup(async (current, _signal, observer) => {
      observer.connection('connected');
      observer.content('部分');
      observer.status({ ...current, status: 'running' });
      return { ...current, status: 'completed', content: '完整' };
    });
    const settled = vi.fn(async (terminal: RunDto) => chat.settle(terminal));
    await chat.follow(run('r1'), settled);
    expect(chat.text()).toBe('部分');
    expect(settled).toHaveBeenCalledWith(expect.objectContaining({ status: 'completed' }));
    expect(chat.live()).toBeNull();
    expect(chat.connection()).toBe('connected');
  });

  it('lets a newer run take over without settling the older one', async () => {
    let release!: () => void;
    const chat = setup(
      (current, signal) =>
        new Promise((resolve) => {
          if (current.id === 'old') release = () => resolve(current);
          else resolve({ ...current, status: 'completed' });
          signal.addEventListener('abort', () => resolve(current));
        }),
    );
    const settled = vi.fn(async () => undefined);
    const older = chat.follow(run('old'), settled);
    await chat.follow(run('new'), settled);
    release();
    await older;
    expect(settled).toHaveBeenCalledOnce();
    expect(settled.mock.calls[0]).toEqual([expect.objectContaining({ id: 'new' })]);
  });

  it('marks the connection lost and forgets a run the server no longer has', async () => {
    const chat = setup(async () => {
      throw new ApiError(404, 'run_not_found');
    });
    await expect(chat.follow(run('r1'), vi.fn())).rejects.toBeInstanceOf(ApiError);
    expect(chat.connection()).toBe('disconnected');
    expect(chat.live()).toBeNull();
  });
});
