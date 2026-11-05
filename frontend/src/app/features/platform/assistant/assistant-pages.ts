import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import type { CatalogId } from '@core/api/feature-catalog';

/** 頁面接上全站助理時登記的內容。 */
export interface AssistantPage {
  /** 頁面的功能 ID；後端描述這一頁的工具以它對應（`IAssistantTool.DescribesPage`）。 */
  feature: CatalogId;
  /** 畫面目前的查詢條件，欄位與該頁查詢端點的參數同名；還沒有條件時回傳 null。 */
  view: () => object | null;
  /** 還沒提問時列出的建議問題。 */
  suggestions: () => readonly string[];
}

/**
 * 目前頁面登記給全站助理的內容。助理只有一個，由外框放置（`AssistantHost`）；
 * 頁面在建構時呼叫 `provide`，離開頁面自動取消，沒有頁面登記時助理不出現。
 */
@Injectable({ providedIn: 'root' })
export class AssistantPages {
  readonly current = signal<AssistantPage | null>(null);

  /** 在呼叫頁面的生命週期內登記（在注入脈絡呼叫）。 */
  provide(page: AssistantPage) {
    this.current.set(page);
    inject(DestroyRef).onDestroy(() => {
      if (this.current() === page) this.current.set(null);
    });
  }
}
