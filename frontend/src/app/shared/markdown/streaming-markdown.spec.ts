import { describe, expect, it } from 'vitest';
import { StreamingMarkdown, completeStreamingInline } from './streaming-markdown';
import { renderMarkdown } from './markdown';

describe('progressive Markdown', () => {
  it('formats unfinished emphasis and code while keeping unsafe links inactive', () => {
    expect(completeStreamingInline('重點：**')).toBe('重點：');
    expect(completeStreamingInline('使用 `')).toBe('使用 ');
    expect(renderMarkdown(completeStreamingInline('重點：**正在生成'), true)).toContain(
      '<strong>正在生成</strong>',
    );
    expect(renderMarkdown(completeStreamingInline('使用 `const x'), true)).toContain(
      '<code>const x</code>',
    );
    expect(
      renderMarkdown(completeStreamingInline('[連結](javascript:alert(1'), true),
    ).not.toContain('href=');
    expect(
      renderMarkdown(completeStreamingInline('<img src=x onerror=alert(1)>'), true),
    ).not.toContain('<img');
  });
  it('commits stable blocks without splitting nested lists, tables or open fences', () => {
    const stream = new StreamingMarkdown();
    const prefix = '# 標題\n\n';
    const first = stream.update(prefix + '- 清單\n  - 子項\n');
    expect(first.blocks[0].content).toBe(prefix);
    const nested = stream.update(prefix + '- 清單\n  - 子項\n\n  段落\n');
    expect(nested.blocks).toHaveLength(1);
    expect(nested.blocks[0]).toBe(first.blocks[0]);
    const code = stream.update(prefix + '- 清單\n  - 子項\n\n  段落\n\n```js\n**raw');
    expect(code.blocks).toHaveLength(2);
    expect(code.tail).toBe('');
    expect(code.fence).toEqual({ language: 'js', chunks: [], tail: '**raw' });
    expect(completeStreamingInline('```js\n**raw')).toBe('```js\n**raw');
  });
  it('follows an open fence in line chunks and hands it back to Markdown once closed', () => {
    const stream = new StreamingMarkdown();
    const lines = Array.from({ length: 60 }, (_, index) => `const x${index} = ${index};\n`);
    let text = '說明\n\n````ts\n';
    let result = stream.update(text);
    for (const line of lines) result = stream.update((text += line));
    expect(result.blocks.map((block) => block.content)).toEqual(['說明\n\n']);
    expect(result.fence!.chunks.length).toBe(2);
    expect(result.fence!.chunks.map((chunk) => chunk.text).join('') + result.fence!.tail).toBe(
      lines.join(''),
    );
    // A shorter marker or one with an info string does not close a four-backtick fence.
    result = stream.update((text += '```\n```js\n'));
    expect(result.fence).not.toBeNull();
    result = stream.update((text += '````'));
    expect(result.fence).toBeNull();
    expect(result.tail).toContain('const x59');
    result = stream.update(text + '\n\n結尾');
    expect(result.blocks.at(-1)!.content).toContain('````ts');
    expect(result.tail).toBe('結尾');
  });
  it('commits finished items of a long list and keeps every character', () => {
    const stream = new StreamingMarkdown();
    let text = '';
    let result = stream.update(text);
    for (let index = 1; index <= 80; index++)
      result = stream.update((text += `${index}. 第 ${index} 項說明，內容足夠長以累積字數。\n`));
    expect(result.blocks.length).toBeGreaterThan(0);
    expect(result.blocks.every((block) => block.continues)).toBe(true);
    expect(result.blocks.map((block) => block.content).join('') + result.tail).toBe(text);
    expect(result.tail.length).toBeLessThan(1400);
    expect(renderMarkdown(result.tail)).toMatch(/<ol start="\d+">/);
  });
  it('resets after a replaced snapshot and preserves every source character', () => {
    const stream = new StreamingMarkdown();
    const text = '\n\n# 標題\n\n|A|B|\n|---|---|\n|1|2|\n\n尾段 👨‍👩‍👧‍👦';
    let result = stream.update('');
    for (let index = 1; index <= text.length; index++) result = stream.update(text.slice(0, index));
    expect(result.blocks.map((block) => block.content).join('') + result.tail).toBe(text);
    result = stream.update('新的答案');
    expect(result.blocks).toEqual([]);
    expect(result.tail).toBe('新的答案');
  });
});
