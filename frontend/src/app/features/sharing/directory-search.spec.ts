import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth-service';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { directorySearch } from './directory-search';
import { ResourceApi } from './resource-api';

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
      { provide: ResourceApi, useValue: { directory } },
      {
        provide: WorkspaceSession,
        useValue: {
          auth: { generation },
          load: async () => true,
          has: () => true,
          me: () => ({ id: 'me' }),
        },
      },
      { provide: AuthService, useValue: { generation } },
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
    expect(context.search.results()).toEqual([]);
  });

  it('leaves out the signed-in user and people already chosen', async () => {
    const context = setup();
    await type(context, '王小');
    expect(context.directory).toHaveBeenCalledWith('王小');
    expect(context.search.results().map((x) => x.id)).toEqual(['a', 'b']);
    context.chosen.set(['a']);
    expect(context.search.results().map((x) => x.id)).toEqual(['b']);
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
