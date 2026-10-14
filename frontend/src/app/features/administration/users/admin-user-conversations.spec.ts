import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AdminUserConversations } from './admin-user-conversations';
import { UserConversationReader } from './user-conversation-reader';

const item = (id: string, title: string) => ({
  id,
  title,
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: '2026-10-01T00:00:00Z',
  isArchived: false,
  isDeleted: id === 'b',
  messages: 0,
});

/** The tab's view of the reader; the reader's own requests and audit are not under test here. */
function fakeReader() {
  const selected = signal<string | null>(null);
  return {
    search: signal(''),
    includeDeleted: signal(true),
    page: signal({ items: [item('a', '週報'), item('b', '公文')], total: 2, offset: 0 }),
    loading: signal(false),
    error: signal<string | null>(null),
    detail: signal(null),
    reading: signal(false),
    readError: signal<string | null>(null),
    selected,
    pageSize: 50,
    start: vi.fn(),
    searchChanged: vi.fn(),
    deletedChanged: vi.fn(),
    loadList: vi.fn(),
    read: vi.fn((id: string) => selected.set(id)),
    close: vi.fn(() => selected.set(null)),
  };
}

function setup(narrow: boolean) {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: narrow,
    media: query,
    addEventListener() {},
    removeEventListener() {},
  }));
  const reader = fakeReader();
  TestBed.configureTestingModule({
    providers: [{ provide: UserConversationReader, useValue: reader }],
  });
  const fixture = TestBed.createComponent(AdminUserConversations);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  return { fixture, reader, root };
}

describe('AdminUserConversations', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('lists conversations beside an empty reading pane and starts the audited listing', () => {
    const { reader, root } = setup(false);
    expect(reader.start).toHaveBeenCalledOnce();
    expect(root.querySelectorAll('.conversation-item')).toHaveLength(2);
    expect(root.querySelector('.conversation-filter')?.textContent).toContain('2 段');
    expect(root.querySelector('.conversation-body')?.classList).not.toContain('is-reading');
    expect(root.querySelector('.reading-empty')?.textContent).toContain('選取左側對話');
    expect(root.querySelector('.conversation-item[data-conversation="b"]')?.textContent).toContain(
      '已刪除',
    );
  });

  it('marks the chosen conversation and switches the narrow layout to reading', async () => {
    const { fixture, reader, root } = setup(true);
    root.querySelector<HTMLButtonElement>('.conversation-item[data-conversation="b"]')!.click();
    await fixture.whenStable();
    expect(reader.read).toHaveBeenCalledWith('b');
    expect(root.querySelector('.conversation-body')?.classList).toContain('is-reading');
    expect(
      root.querySelector('.conversation-item[data-conversation="b"]')?.getAttribute('aria-current'),
    ).toBe('true');
    expect(document.activeElement).toBe(root.querySelector('.reading-back'));
  });

  it('returns from the reading to the same row', async () => {
    const { fixture, reader, root } = setup(true);
    root.querySelector<HTMLButtonElement>('.conversation-item[data-conversation="a"]')!.click();
    await fixture.whenStable();
    root.querySelector<HTMLButtonElement>('.reading-back')!.click();
    await fixture.whenStable();
    expect(reader.close).toHaveBeenCalledOnce();
    expect(root.querySelector('.conversation-body')?.classList).not.toContain('is-reading');
    expect(document.activeElement).toBe(
      root.querySelector('.conversation-item[data-conversation="a"]'),
    );
  });
});
