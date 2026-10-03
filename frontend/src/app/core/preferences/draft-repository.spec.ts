import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DraftRepository } from './draft-repository';

describe('Personal draft persistence', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => vi.restoreAllMocks());

  it('isolates accounts, conversations and the new conversation draft', () => {
    const drafts = new DraftRepository();
    drafts.save('alice', null, '未送出的提問', ['file-1']);
    drafts.save('alice', 'project', '工作專案', []);
    expect(drafts.load('alice', null)?.text).toBe('未送出的提問');
    expect(drafts.load('alice', 'project')?.text).toBe('工作專案');
    expect(drafts.load('bob', null)).toBeNull();
    expect(drafts.load('bob', 'project')).toBeNull();
    drafts.clear('alice', null);
    expect(drafts.load('alice', null)).toBeNull();
    expect(drafts.load('alice', 'project')?.text).toBe('工作專案');
  });

  it('keeps empty text when a draft still has attachments', () => {
    const drafts = new DraftRepository();
    drafts.save('alice', null, '', ['image']);
    expect(drafts.load('alice', null)?.attachmentIds).toEqual(['image']);
    drafts.save('alice', null, '  ', []);
    expect(drafts.load('alice', null)).toBeNull();
  });

  it('ignores corrupt storage without mistaking it for unavailable storage', () => {
    const drafts = new DraftRepository();
    localStorage.setItem('nexus.draft.alice.new', '{broken');
    expect(drafts.load('alice', null)).toBeNull();
    expect(drafts.available()).toBe(true);
    localStorage.setItem(
      'nexus.draft.alice.new',
      JSON.stringify({ text: 'x', attachmentIds: [42], updatedAt: 1 }),
    );
    expect(drafts.load('alice', null)).toBeNull();
  });

  it('evicts older drafts only within the current account, including corrupt entries', () => {
    const drafts = new DraftRepository();
    let time = 0;
    vi.spyOn(Date, 'now').mockImplementation(() => ++time);
    drafts.save('bob', 'keep', '其他人的草稿', []);
    localStorage.setItem('nexus.draft.alice.broken', '{broken');
    for (let i = 0; i < 42; i++) drafts.save('alice', String(i), `提問 ${i}`, []);
    expect(drafts.load('alice', '0')).toBeNull();
    expect(drafts.load('alice', '1')).toBeNull();
    expect(drafts.load('alice', '2')?.text).toBe('提問 2');
    expect(drafts.load('alice', '41')?.text).toBe('提問 41');
    expect(drafts.load('bob', 'keep')?.text).toBe('其他人的草稿');
    expect(
      Object.keys(localStorage).filter((key) => key.startsWith('nexus.draft.alice.')),
    ).toHaveLength(40);
  });

  it('survives browser storage restrictions and recovers when writes work again', () => {
    const drafts = new DraftRepository();
    const blocked = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('Blocked', 'QuotaExceededError');
    });
    expect(() => drafts.save('alice', null, '保留在記憶體中的文字', [])).not.toThrow();
    expect(drafts.available()).toBe(false);
    blocked.mockRestore();
    drafts.save('alice', null, '再次保存', []);
    expect(drafts.available()).toBe(true);
    expect(drafts.load('alice', null)?.text).toBe('再次保存');
  });
});
