import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { NexusApi } from '@core/api/nexus-api';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { NotificationStore } from '@features/notifications/notification-store';
import { DraftRepository } from '@core/preferences/draft-repository';
import { ThemeService } from '@core/preferences/theme-service';
import { defaultSettings, UserSettingsService } from '@core/preferences/user-settings';
import { RunStream } from '@core/stream/run-stream';
import { DraftAttachments } from '@features/attachments/draft-attachments';
import { KnowledgeSelection } from '@features/knowledge/collections/knowledge-selection';
import { ProjectsApi } from '@features/projects/projects-api';
import { ConversationsApi } from '@features/conversations/conversations-api';
import { ChatStore } from './chat-store';

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => (resolve = done));
  return { promise, resolve };
}

const me = (features: string[]) => ({
  id: 'u1',
  activeRunId: null,
  preferences: { theme: 'system', reducedMotion: false, defaultModelId: 'b' },
  access: { features: features.map((id) => ({ id, name: id })) },
});
const catalog = {
  models: [
    { id: 'a', reasoningEfforts: ['auto'], defaultReasoningEffort: 'auto' },
    { id: 'b', reasoningEfforts: ['auto'], defaultReasoningEffort: 'auto' },
  ],
  policy: {
    allowModelSelection: true,
    showModelNames: true,
    defaultModelId: 'a',
    maxInputCharacters: 100,
  },
  notice: null,
};

function setup(features = ['chat']) {
  const models = deferred<typeof catalog>();
  const conversations = deferred<unknown[]>();
  const api = {
    models: vi.fn(() => models.promise),
    conversations: vi.fn(() => conversations.promise),
    webSearchStatus: vi.fn(async () => ({ available: true, notice: '' })),
    context: vi.fn(),
  };
  const account = signal<ReturnType<typeof me> | null>(null);
  const generation = signal(1);
  const drafts = { load: vi.fn(() => null), save: vi.fn(), available: signal(true) };
  const session = {
    me: account,
    load: vi.fn(async () => {
      account.set(me(features));
      return account();
    }),
    has: (id: string) => account()?.access.features.some((x) => x.id === id) ?? false,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: NexusApi, useValue: api },
      { provide: WorkspaceSession, useValue: session },
      {
        provide: AuthService,
        useValue: { generation, guard: AuthService.prototype.guard },
      },
      { provide: UserSettingsService, useValue: { value: signal(defaultSettings()) } },
      { provide: ThemeService, useValue: { apply: vi.fn() } },
      { provide: NotificationStore, useValue: {} },
      { provide: ProjectsApi, useValue: {} },
      { provide: RunStream, useValue: {} },
      { provide: ConversationsApi, useValue: { labels: vi.fn(async () => ['工作']) } },
      { provide: DraftRepository, useValue: drafts },
      {
        provide: DraftAttachments,
        useValue: {
          initialize: vi.fn(async () => undefined),
          files: signal([]),
          uploading: signal(false),
          policy: signal(null),
          error: signal(''),
          reset: vi.fn(),
        },
      },
      {
        provide: KnowledgeSelection,
        useValue: {
          initialize: vi.fn(async () => undefined),
          saving: signal(false),
          loadFailed: signal(false),
          reset: vi.fn(),
        },
      },
    ],
  });
  return {
    store: TestBed.inject(ChatStore),
    api,
    session,
    models,
    conversations,
    generation,
    drafts,
  };
}

describe('chat store bootstrap', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('requests models and history together once the account is known', async () => {
    const f = setup();
    const ready = f.store.initialize();
    await vi.waitFor(() => expect(f.api.models).toHaveBeenCalled());
    expect(f.api.conversations).toHaveBeenCalled();
    expect(f.store.ready()).toBe(false);

    f.models.resolve(catalog);
    f.conversations.resolve([]);
    await ready;

    expect(f.session.load).toHaveBeenCalledWith(true);
    expect(f.store.ready()).toBe(true);
    expect(f.store.modelId()).toBe('b');
    expect(f.store.labels()).toEqual(['工作']);
    expect(f.store.webSearchStatus().available).toBe(true);
  });

  it('saves the draft for the signed-out account although the session forgets it first', async () => {
    const f = setup();
    f.models.resolve(catalog);
    f.conversations.resolve([]);
    await f.store.initialize();
    f.store.draft.set({ text: '登出前的草稿' });
    f.drafts.save.mockClear();
    // The shared session clears the account on the same generation change, before this store.
    f.session.me.set(null);
    f.generation.set(2);
    TestBed.tick();
    expect(f.drafts.save).toHaveBeenCalledWith('u1', null, '登出前的草稿', []);
  });

  it('asks for nothing else when the role has no chat access', async () => {
    const f = setup(['knowledge']);
    await f.store.initialize();
    expect(f.store.ready()).toBe(false);
    expect(f.store.error()).toContain('沒有對話功能');
    expect(f.api.models).not.toHaveBeenCalled();
    expect(f.api.conversations).not.toHaveBeenCalled();
  });

  it('keeps sending blocked while an over-budget estimate is being refreshed', async () => {
    const f = setup();
    f.models.resolve(catalog);
    f.conversations.resolve([]);
    await f.store.initialize();
    const usage = (budgetExceeded: boolean) =>
      ({ estimatedInputTokens: 1, contextTokens: 10, budgetExceeded }) as never;
    f.store.contextUsage.set(usage(true));
    expect(f.store.canSend()).toBe(false);

    const next = deferred<unknown>();
    f.api.context.mockReturnValue(next.promise);
    f.store.contextPending.set(true);
    const preview = f.store.previewContext({} as never, new AbortController().signal);
    expect(f.store.canSend()).toBe(false);

    next.resolve(usage(false));
    await preview;
    expect(f.store.contextPending()).toBe(false);
    expect(f.store.canSend()).toBe(true);
  });
});
