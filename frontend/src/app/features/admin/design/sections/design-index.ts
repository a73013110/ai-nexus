import { Injectable, signal } from '@angular/core';

/** 索引的一筆：區段的 id、標題與宿主元素（捲動與判斷目前位置用）。 */
export interface DesignIndexEntry {
  readonly id: string;
  readonly title: string;
  readonly element: HTMLElement;
}

/**
 * 側欄索引的來源：畫面上的 `DesignSection` 渲染後自行登記、銷毀時移除，
 * 所以新增範例不必另外維護清單，切換分類時索引也跟著換。由頁面提供。
 */
@Injectable()
export class DesignIndex {
  private readonly entries = signal<readonly DesignIndexEntry[]>([]);
  /** 依文件位置排序：巢狀的範例元件（例：對話框尺寸）比外層後面的區段晚建立，不能用登記順序。 */
  readonly sections = this.entries.asReadonly();

  add(entry: DesignIndexEntry) {
    this.entries.update((entries) => [...entries, entry].sort(byDocumentOrder));
  }
  remove(element: HTMLElement) {
    this.entries.update((entries) => entries.filter((entry) => entry.element !== element));
  }
}

const byDocumentOrder = (a: DesignIndexEntry, b: DesignIndexEntry) =>
  a.element.compareDocumentPosition(b.element) & Node.DOCUMENT_POSITION_FOLLOWING ? -1 : 1;
