import { describe, expect, it, vi } from 'vitest';
import type { ConfirmDialog } from './confirm-dialog';
import { DraftEditor } from './draft-editor';

interface Draft {
  name: string;
  enabled: boolean;
}
const same = (a: Draft, b: Draft) => a.name.trim() === b.name.trim() && a.enabled === b.enabled;

function editor() {
  const value = new DraftEditor<Draft, { id: string | null }>(same);
  value.open({ id: 'u1' }, { name: '研發部', enabled: true });
  return value;
}

describe('DraftEditor', () => {
  it('is clean before opening and right after opening', () => {
    const value = new DraftEditor<Draft>(same);
    expect(value.current()).toBeNull();
    expect(value.dirty()).toBe(false);

    value.open({}, { name: '研發部', enabled: true });
    expect(value.current()).toEqual({ draft: { name: '研發部', enabled: true } });
    expect(value.dirty()).toBe(false);
  });

  it('keeps the context and compares drafts with the given rule', () => {
    const value = editor();
    value.update('name', ' 研發部 ');
    expect(value.current()?.id).toBe('u1');
    expect(value.dirty()).toBe(false);

    value.patch({ name: '業務部', enabled: false });
    expect(value.current()?.draft).toEqual({ name: '業務部', enabled: false });
    expect(value.dirty()).toBe(true);
  });

  it('reverts to the baseline and starts from a different draft when asked', () => {
    const value = editor();
    value.update('enabled', false);
    value.revert();
    expect(value.current()?.draft.enabled).toBe(true);
    expect(value.dirty()).toBe(false);

    value.open({ id: 'u2' }, { name: '總務部', enabled: true }, { name: '總務部', enabled: false });
    expect(value.baseline()).toEqual({ name: '總務部', enabled: true });
    expect(value.dirty()).toBe(true);
  });

  it('treats the current draft as saved and keeps it after saving', () => {
    const value = editor();
    value.update('name', '業務部');
    value.markSaved();
    expect(value.dirty()).toBe(false);
    expect(value.current()?.draft.name).toBe('業務部');
    value.revert();
    expect(value.current()?.draft.name).toBe('業務部');
  });

  it('asks before discarding only when something changed', async () => {
    const discard = vi.fn().mockResolvedValue(false);
    const confirmation = { discard } as unknown as ConfirmDialog;
    const value = editor();
    expect(await value.confirmDiscard(confirmation, '尚未儲存。')).toBe(true);
    expect(discard).not.toHaveBeenCalled();

    value.update('enabled', false);
    expect(await value.confirmDiscard(confirmation, '尚未儲存。')).toBe(false);
    expect(discard).toHaveBeenCalledWith('尚未儲存。');
  });
});
