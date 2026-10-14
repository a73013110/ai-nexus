import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { NexusApi } from '@core/api/nexus-api';
import type { ConversationDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { ConversationsApi } from '@features/conversations/conversations-api';
import { ChatHistory } from './chat-history';

const rows = (prefix: string, count: number) =>
  Array.from({ length: count }, (_, index) => ({ id: `${prefix}${index}` }));

function setup(api: object, workspace: object = {}) {
  const generation = signal(1);
  TestBed.configureTestingModule({
    providers: [
      { provide: NexusApi, useValue: api },
      { provide: ConversationsApi, useValue: workspace },
      { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
    ],
  });
  return { history: TestBed.inject(ChatHistory), generation };
}

describe('chat history', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('applies only the latest search when responses arrive out of order', async () => {
    const pending: ((value: unknown) => void)[] = [];
    const conversations = vi.fn(
      (_search: string) => new Promise((resolve) => pending.push(resolve)),
    );
    const { history } = setup({ conversations });
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
    const { history } = setup({ conversations });
    await history.refresh();
    expect(history.hasMore()).toBe(true);
    await history.refresh(true);
    expect(conversations.mock.calls[1][1]).toBe(100);
    expect(history.conversations()).toHaveLength(103);
    expect(history.hasMore()).toBe(false);
  });

  it('places a changed conversation where the server list would, or drops it from the view', () => {
    const { history } = setup({});
    const row = (id: string, updatedAt: string, changes: Partial<ConversationDto> = {}) =>
      ({
        id,
        updatedAt,
        isFavorite: false,
        isArchived: false,
        labels: [],
        ...changes,
      }) as ConversationDto;
    history.conversations.set([
      row('a', '2026-01-03'),
      row('b', '2026-01-02'),
      row('c', '2026-01-01'),
    ]);

    history.place(row('c', '2026-01-01', { isFavorite: true }));
    expect(history.conversations().map((x) => x.id)).toEqual(['c', 'a', 'b']);

    history.place(row('a', '2026-01-03', { isArchived: true }));
    expect(history.conversations().map((x) => x.id)).toEqual(['c', 'b']);

    history.label.set('工作');
    history.place(row('b', '2026-01-02', { title: '改名' }));
    expect(history.conversations().map((x) => x.id)).toEqual(['c']);
  });

  it('drops labels that arrive after the account changed', async () => {
    let resolve!: (value: string[]) => void;
    const labels = () => new Promise<string[]>((done) => (resolve = done));
    const { history, generation } = setup({}, { labels });
    const reload = history.reloadLabels();
    generation.set(2);
    resolve(['工作']);
    await reload;
    expect(history.labels()).toEqual([]);
  });
});
