import { Injectable, computed, inject, signal } from '@angular/core';
import { ViewScope } from '@shared/browser/view-scope';
import { endpointTable } from '@shared/ui/table-model';
import { FilesApi } from './files-api';

/**
 * 有上限的伺服器端瀏覽，由檔案庫與檔案選擇器共用。分頁、讀取狀態交給 `endpointTable`（ADR 0026），
 * 卡片格線與選擇器以 `<nx-table-pagination [table]>` 翻頁；換條件時模型自己回到第一頁。
 */
@Injectable()
export class FileLibraryStore {
  private readonly api = inject(FilesApi);
  private readonly scope = inject(ViewScope);
  /** 選擇器在第一次開啟前不讀取任何東西。 */
  private readonly active = signal(false);
  readonly search = signal('');
  private readonly query = signal('');
  private readonly typing = signal(false);
  readonly type = signal('all');
  readonly source = signal('all');
  /** 每頁固定 40 張（卡片格線剛好排滿）。 */
  readonly table = endpointTable({
    params: computed(() =>
      this.active()
        ? { search: this.query(), type: this.type(), source: this.source() }
        : undefined,
    ),
    // 後端以 offset／limit 分頁、不走 `TableRequest`（沒有欄位排序與篩選），所以永遠是逐頁讀。
    load: async (filters, page, signal) => ({
      ...(await this.api.files({ ...filters, offset: page.offset, limit: page.limit }, signal)),
      offset: page.offset,
      complete: false,
    }),
    columns: [],
    pageSize: 40,
    pageSizes: [40],
    unit: '份',
  });
  /** 讀取中：搜尋的防抖等待與重讀。 */
  readonly loading = computed(() => this.typing() || this.table.refreshing());
  readonly error = this.table.error;
  readonly items = this.table.rows;
  /** 超過一頁才顯示分頁；已翻到後面的頁時即使總數變少也保留，才能回到前面。 */
  readonly paged = computed(
    () => this.table.total() > this.table.pageSize() || this.table.pageIndex() > 0,
  );
  /** 開始讀取，或在其他地方變更後重新讀取。 */
  load() {
    if (this.active()) this.table.reload();
    else this.active.set(true);
  }
  find(value: string) {
    this.search.set(value);
    this.typing.set(true);
    this.scope.later(
      () => {
        this.typing.set(false);
        this.query.set(value);
      },
      220,
      'files-search',
    );
  }
  filter(type: string, source = this.source()) {
    this.type.set(type);
    this.source.set(source);
  }
}
