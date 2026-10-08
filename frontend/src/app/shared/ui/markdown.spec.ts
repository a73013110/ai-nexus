import { describe, expect, it } from 'vitest';
import { renderMarkdown, renderMarkdownWithDiagrams } from './markdown';
describe('Markdown security', () => {
  it('never enables raw HTML, scripts or tracking images', () => {
    const html = renderMarkdown(
      '<script>alert(1)</script>\n<img src=x onerror=alert(1)>\n![追蹤](https://evil.test/pixel)',
    );
    expect(html).not.toContain('<script>');
    expect(html).not.toContain('<img');
    expect(html).toContain('［圖片：追蹤］');
  });
  it('rejects dangerous schemes and sets safe external link attributes', () => {
    const html = renderMarkdown('[點擊](javascript:alert(1))\n[文件](https://angular.dev)');
    expect(html).not.toContain('href="javascript:');
    expect(html).toContain('rel="noopener noreferrer"');
  });
  it('renders tables and escapes unrecognized code language labels', () => {
    const html = renderMarkdown(
      '|欄位|內容|\n|---|---|\n|A|B|\n\n```html\" onclick=\"alert(1)\n<script>evil()</script>\n```',
    );
    expect(html).toContain('<table>');
    expect(html).not.toContain('onclick=');
    expect(html).not.toContain('<script>');
    expect(html).toContain('複製程式碼');
  });
  it('mounts complete Mermaid fences at their original nesting without trusting diagram content as HTML', () => {
    const { html, diagrams } = renderMarkdownWithDiagrams(
      '> 圖表\n>\n> ```MERMAID\n> flowchart LR\n> A["<script>unsafe</script>"] --> B[完成]\n> ```\n\n```mermaid\nsequenceDiagram\nA->>B: 測試\n```',
    );
    expect(diagrams).toHaveLength(2);
    expect(diagrams[0]).toContain('<script>unsafe</script>');
    expect(html).toContain('<blockquote>');
    expect(html).toContain('data-diagram-index="0"');
    expect(html).toContain('data-diagram-index="1"');
    expect(html).not.toContain('<script>');
    expect(
      renderMarkdownWithDiagrams('<div class="markdown-diagram-slot" data-diagram-index="0"></div>')
        .diagrams,
    ).toHaveLength(0);
  });
  it('keeps streaming Mermaid as escaped source until the block is committed', () => {
    const source = '```mermaid\nflowchart LR\nA["<img src=x>"] -->';
    const { html, diagrams } = renderMarkdownWithDiagrams(source, true);
    expect(diagrams).toHaveLength(0);
    expect(html).toContain('複製程式碼');
    expect(html).toContain('&lt;img');
    expect(html).not.toContain('data-diagram-index');
  });
});
