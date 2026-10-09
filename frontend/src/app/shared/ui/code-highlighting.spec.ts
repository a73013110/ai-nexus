import { describe, expect, it, vi } from 'vitest';
import { highlightWhenVisible } from './code-highlighting';
import { renderMarkdown } from './markdown';

describe('deferred code highlighting', () => {
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
    // Blocks are highlighted in time slices, so a slow machine may reach the unknown block later.
    await vi.waitFor(() => {
      expect(known.innerHTML).toContain('hljs-keyword');
      expect(unknown.hasAttribute('data-language')).toBe(false);
    });
    expect(known.textContent).toBe('const x = "<b>";\n');
    expect(known.querySelector('b')).toBeNull();
    expect(unknown.innerHTML).toBe('plain\n');
    host.remove();
  });
});
