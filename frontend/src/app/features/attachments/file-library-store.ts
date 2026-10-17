import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import { ViewScope } from '@shared/browser/view-scope';
import { AttachmentsApi } from './attachments-api';

/** 有上限的伺服器端瀏覽，由檔案庫與檔案選擇器共用。 */
@Injectable()
export class FileLibraryStore {
  private readonly api = inject(AttachmentsApi);
  private readonly scope = inject(ViewScope);
  /** 選擇器在第一次開啟前不讀取任何東西。 */
  private readonly active = signal(false);
  readonly search = signal('');
  private readonly query = signal('');
  private readonly typing = signal(false);
  readonly type = signal('all');
  readonly source = signal('all');
  readonly offset = signal(0);
  readonly pageSize = 40;
  readonly result = apiResource({
    params: () =>
      this.active()
        ? {
            search: this.query(),
            type: this.type(),
            source: this.source(),
            offset: this.offset(),
            limit: this.pageSize,
          }
        : undefined,
    loader: (filters, signal) => this.api.files(filters, signal),
  });
  readonly loading = computed(() => this.typing() || this.result.refreshing());
  readonly error = this.result.error;
  readonly items = computed(() => this.result.value()?.items ?? []);
  readonly total = computed(() => this.result.value()?.total ?? 0);
  readonly canNext = computed(() => this.offset() + this.items().length < this.total());
  readonly pageIndex = computed(() => Math.floor(this.offset() / this.pageSize));
  /** 超過一頁才顯示分頁；已翻到後面的頁時即使總數變少也保留，才能回到前面。 */
  readonly paged = computed(() => this.total() > this.pageSize || this.offset() > 0);
  /** 開始讀取，或在其他地方變更後重新讀取。 */
  load() {
    if (this.active()) this.result.reload();
    else this.active.set(true);
  }
  find(value: string) {
    this.search.set(value);
    this.typing.set(true);
    this.scope.later(
      () => {
        this.typing.set(false);
        this.offset.set(0);
        this.query.set(value);
      },
      220,
      'files-search',
    );
  }
  filter(type: string, source = this.source()) {
    this.type.set(type);
    this.source.set(source);
    this.offset.set(0);
  }
  page(direction: number) {
    this.select(this.pageIndex() + direction);
  }
  select(index: number) {
    this.offset.set(Math.max(0, index) * this.pageSize);
  }
}
