import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth-service';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { FileLibraryStore } from './file-library-store';
import { FilesApi } from './files-api';

const pause = (ms: number) => new Promise((done) => setTimeout(done, ms));

function setup(total = 100) {
  const generation = signal(1);
  const list = vi.fn(async (filters: { offset: number }) => ({
    items: Array.from({ length: 40 }, (_, index) => ({ id: `${filters.offset + index}` })),
    total,
  }));
  TestBed.configureTestingModule({
    providers: [
      FileLibraryStore,
      ViewScope,
      { provide: FilesApi, useValue: { list } },
      {
        provide: WorkspaceSession,
        useValue: { auth: { generation }, load: async () => true, has: () => true },
      },
      { provide: AuthService, useValue: { generation } },
    ],
  });
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { store: TestBed.inject(FileLibraryStore), list, settle };
}

describe('file library store', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('reads nothing until it is first loaded', async () => {
    const { store, list, settle } = setup();
    await settle();
    expect(list).not.toHaveBeenCalled();
    store.load();
    await settle();
    expect(list).toHaveBeenCalledTimes(1);
    expect(store.items()).toHaveLength(40);
    expect(store.canNext()).toBe(true);
  });

  it('pages within the total and returns to the first page on a new filter', async () => {
    const { store, list, settle } = setup(60);
    store.load();
    await settle();
    store.page(1);
    await settle();
    expect(store.offset()).toBe(40);
    expect(store.canNext()).toBe(false);
    store.filter('image');
    await settle();
    expect(store.offset()).toBe(0);
    expect(list).toHaveBeenLastCalledWith(
      { search: '', type: 'image', source: 'all', offset: 0 },
      expect.anything(),
    );
  });

  it('searches after typing pauses and reports the pause as loading', async () => {
    const { store, list, settle } = setup();
    store.load();
    await settle();
    store.find('報告');
    expect(store.loading()).toBe(true);
    await pause(250);
    await settle();
    expect(list).toHaveBeenLastCalledWith(
      { search: '報告', type: 'all', source: 'all', offset: 0 },
      expect.anything(),
    );
    expect(store.loading()).toBe(false);
  });
});
