import { describe, expect, it } from 'vitest';
import { releaseNoteGroups } from './about-dialog';

describe('releaseNoteGroups', () => {
  it('lists features before fixes and leaves out changes users do not notice', () => {
    const groups = releaseNoteGroups([
      { type: 'fix', scope: 'chat', subject: '登入逾時後保留草稿' },
      { type: 'docs', scope: '', subject: '補上部署步驟' },
      { type: 'feat', scope: 'releases', subject: '版本頁' },
    ]);
    expect(groups.map((group) => [group.label, group.items.map((item) => item.subject)])).toEqual([
      ['新功能', ['版本頁']],
      ['修正', ['登入逾時後保留草稿']],
    ]);
  });
});
