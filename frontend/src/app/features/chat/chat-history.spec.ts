import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { NexusApi } from '../../core/api/nexus-api';
import { ChatHistory } from './chat-history';

const rows = (prefix: string, count: number) =>
  Array.from({ length: count }, (_, index) => ({ id: `${prefix}${index}` }));

describe('chat history', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('applies only the latest search when responses arrive out of order', async () => {
    const pending: ((value: unknown) => void)[] = [];
    const conversations = vi.fn(
      (_search: string) => new Promise((resolve) => pending.push(resolve)),
    );
    TestBed.configureTestingModule({
      providers: [{ provide: NexusApi, useValue: { conversations } }],
    });
    const history = TestBed.inject(ChatHistory);
    history.search.set('舊');
    const older = history.refresh();
    history.search.set('新');
    const newer = history.refresh();
    pending[1](rows('new', 1));
    pending[0](rows('old', 1));
    await Promise.all([older, newer]);
    expect(history.conversations().map((x) => x.id)).toEqual(['new0']);
    expect(conversations.mock.calls.map((call) => call[0])).toEqual(['舊', '新']);
  });

  it('appends further pages from the current offset and reports when more remain', async () => {
    const conversations = vi
      .fn()
      .mockResolvedValueOnce(rows('a', 100))
      .mockResolvedValueOnce(rows('b', 3));
    TestBed.configureTestingModule({
      providers: [{ provide: NexusApi, useValue: { conversations } }],
    });
    const history = TestBed.inject(ChatHistory);
    await history.refresh();
    expect(history.hasMore()).toBe(true);
    await history.refresh(true);
    expect(conversations.mock.calls[1][1]).toBe(100);
    expect(history.conversations()).toHaveLength(103);
    expect(history.hasMore()).toBe(false);
  });
});
