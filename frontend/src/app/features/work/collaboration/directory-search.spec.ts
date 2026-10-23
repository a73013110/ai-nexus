import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { directorySearch } from './directory-search';
import { CollaborationApi } from './collaboration-api';

const people = [
  { id: 'me', displayName: '我', account: 'me' },
  { id: 'a', displayName: '王小明', account: 'a' },
  { id: 'b', displayName: '王大同', account: 'b' },
];

const pause = (ms: number) => new Promise((done) => setTimeout(done, ms));

function setup() {
  const generation = signal(1);
  const directory = vi.fn(async () => people);
  TestBed.configureTestingModule({
    providers: [
      ViewScope,
      { provide: CollaborationApi, useValue: { directory } },
      {
        provide: WorkspaceSession,
        useValue: {
          auth: { generation },
          load: async () => true,
          has: () => true,
          me: () => ({ id: 'me' }),
        },
      },
      { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
    ],
  });
  const chosen = signal<string[]>([]);
  const search = TestBed.runInInjectionContext(() => directorySearch(chosen));
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { search, chosen, directory, settle };
}

describe('directory search', () => {
  afterEach(() => TestBed.resetTestingModule());

  async function type(context: ReturnType<typeof setup>, value: string) {
    context.search.find(value);
    await pause(280);
    await context.settle();
  }

  it('waits for two characters before searching', async () => {
    const context = setup();
    await type(context, '王');
    expect(context.directory).not.toHaveBeenCalled();
    expect(context.search.searchable()).toBe(false);
    expect(context.search.results()).toEqual([]);
  });

  it('leaves out the signed-in user and people already chosen', async () => {
    const context = setup();
    await type(context, '王小');
    expect(context.directory).toHaveBeenCalledWith('王小');
    expect(context.search.searchable()).toBe(true);
    expect(context.search.results().map((item) => item.id)).toEqual(['a', 'b']);
    context.chosen.set(['a']);
    expect(context.search.results().map((item) => item.id)).toEqual(['b']);
  });

  it('counts the debounce wait and the request as loading', async () => {
    const context = setup();
    let answer!: (value: typeof people) => void;
    context.directory.mockImplementationOnce(
      () => new Promise<typeof people>((done) => (answer = done)),
    );
    context.search.find('王');
    expect(context.search.loading()).toBe(false);
    context.search.find('王小');
    expect(context.search.loading()).toBe(true);
    expect(context.search.results()).toEqual([]);
    await pause(280);
    await vi.waitFor(() => expect(answer).toBeDefined());
    expect(context.search.loading()).toBe(true);
    answer(people);
    await context.settle();
    expect(context.search.loading()).toBe(false);
    expect(context.search.results().map((item) => item.id)).toEqual(['a', 'b']);
    context.search.clear();
    expect(context.search.loading()).toBe(false);
  });

  it('hides results for text that has not been searched yet and clears everything', async () => {
    const context = setup();
    await type(context, '王小');
    context.search.find('王小明');
    expect(context.search.results()).toEqual([]);
    context.search.clear();
    expect(context.search.text()).toBe('');
    expect(context.search.results()).toEqual([]);
  });
});
