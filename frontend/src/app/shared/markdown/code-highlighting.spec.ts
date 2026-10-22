import { beforeAll, describe, expect, it, vi } from 'vitest';
import { highlightWhenVisible } from './code-highlighting';
import { renderMarkdownWithDiagrams } from './markdown';

const renderMarkdown = (content: string) => renderMarkdownWithDiagrams(content).html;

describe('deferred code highlighting', () => {
  // 上色器第一次使用時才動態載入 highlight.js 與十多種文法；全套平行執行時載入可能超過
  // vi.waitFor 預設的 1 秒而偶發失敗。先載入好，等待就只涵蓋要驗證的分段上色。
  beforeAll(() => import('./code-highlight'));

  it('renders escaped code first, then colours known languages only', async () => {
    const host = document.createElement('div');
    document.body.append(host);
    host.innerHTML = renderMarkdown(
      '```ts\nconst x = "<b>";\n```\n\n```unknown\nplain\n```\n\n```\ntext\n```',
    );
    const [known, unknown, plain] = [...host.querySelectorAll('code')];
    expect(known.dataset['language']).toBe('ts');
    expect(known.innerHTML).not.toContain('hljs');
    expect(plain.hasAttribute('data-language')).toBe(false);

    highlightWhenVisible(host);
    // 區塊是以時間片段上色的，因此較慢的機器可能會較晚才處理未知的區塊。
    await vi.waitFor(() => {
      expect(known.innerHTML).toContain('hljs-keyword');
      expect(unknown.hasAttribute('data-language')).toBe(false);
    });
    expect(known.textContent).toBe('const x = "<b>";\n');
    expect(known.querySelector('b')).toBeNull();
    expect(unknown.innerHTML).toBe('plain\n');
    host.remove();
  });

  it('colours code highlighted before immediately when it is mounted again', async () => {
    const markdown = renderMarkdown('```ts\nconst again = 1;\n```');
    const first = document.createElement('div');
    document.body.append(first);
    first.innerHTML = markdown;
    highlightWhenVisible(first);
    await vi.waitFor(() => expect(first.querySelector('.hljs-keyword')).not.toBeNull());

    const remount = document.createElement('div');
    document.body.append(remount);
    remount.innerHTML = markdown;
    highlightWhenVisible(remount);
    const [before, after] = [first, remount].map((host) => host.querySelector('code')!);
    expect(after.innerHTML).toBe(before.innerHTML);
    expect(after.hasAttribute('data-language')).toBe(false);
    first.remove();
    remount.remove();
  });
});
