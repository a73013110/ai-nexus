import { afterRenderEffect, DestroyRef, Directive, ElementRef, inject, input } from '@angular/core';
import { highlightWhenVisible } from './code-highlighting';

/** 超過此大小，上色的成本會大於效益；原始碼維持純文字。 */
const HIGHLIGHT_LIMIT = 160_000;

/**
 * 唯讀原始碼的文字與語法上色：`<code [nxHighlightedCode]="text" language="java">`。
 * 上色會改寫 code 的內容，所以文字由這裡設定，不能再用插值綁定；上色延到接近視口才做（`highlightWhenVisible`），
 * 沒有登記文法、`language` 為 null 或內容太大時維持純文字。
 */
@Directive({ selector: 'code[nxHighlightedCode]' })
export class HighlightedCode {
  readonly source = input.required<string>({ alias: 'nxHighlightedCode' });
  /** highlight.js 的語言名稱或別名（`java`、`cs`…）。 */
  readonly language = input<string | null>(null);

  constructor() {
    const code = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    let clear: () => void = () => undefined;
    afterRenderEffect(() => {
      const source = this.source();
      const language = this.language();
      clear();
      clear = () => undefined;
      // 結尾的換行會在 pre 底部多出一行空白。
      code.textContent = source.endsWith('\n') ? source.slice(0, -1) : source;
      if (!language || source.length > HIGHLIGHT_LIMIT) {
        code.removeAttribute('data-language');
        return;
      }
      code.dataset['language'] = language;
      clear = highlightWhenVisible(code.parentElement ?? code);
    });
    inject(DestroyRef).onDestroy(() => clear());
  }
}
