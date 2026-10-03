import { describe, expect, it } from 'vitest';
import { renderMarkdown } from './markdown';
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
});
