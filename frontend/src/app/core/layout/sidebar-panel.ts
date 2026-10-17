import { DestroyRef, Directive, Injectable, TemplateRef, inject, signal } from '@angular/core';

/** 頁面專屬的內容，顯示在共用導覽之下，例如聊天歷史。 */
@Injectable({ providedIn: 'root' })
export class SidebarPanel {
  readonly template = signal<TemplateRef<unknown> | null>(null);
}

/**
 * 宣告目前頁面的側邊欄內容。範本保留其頁面脈絡，因此其中的綁定與輸出仍與頁面對話，
 * 而由殼層決定它在哪裡渲染。
 */
@Directive({ selector: 'ng-template[nxSidebarPanel]' })
export class SidebarPanelContent {
  constructor() {
    const panel = inject(SidebarPanel);
    const template = inject(TemplateRef);
    panel.template.set(template);
    inject(DestroyRef).onDestroy(() => {
      if (panel.template() === template) panel.template.set(null);
    });
  }
}
