import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '../../core/api/api-resource';
import { ViewScope } from '../../shared/browser/view-scope';
import { FilesApi } from './files-api';

/** Bounded server-side browsing, shared by the library and file chooser. */
@Injectable()
export class FileLibraryStore {
  private readonly api = inject(FilesApi);
  private readonly scope = inject(ViewScope);
  /** The chooser reads nothing until it is first opened. */
  private readonly active = signal(false);
  readonly search = signal('');
  private readonly query = signal('');
  private readonly typing = signal(false);
  readonly type = signal('all');
  readonly source = signal('all');
  readonly offset = signal(0);
  readonly result = apiResource({
    params: () =>
      this.active()
        ? { search: this.query(), type: this.type(), source: this.source(), offset: this.offset() }
        : undefined,
    loader: (filters, signal) => this.api.list(filters, signal),
  });
  readonly loading = computed(() => this.typing() || this.result.refreshing());
  readonly error = this.result.error;
  readonly items = computed(() => this.result.value()?.items ?? []);
  readonly total = computed(() => this.result.value()?.total ?? 0);
  readonly canNext = computed(() => this.offset() + this.items().length < this.total());
  /** Start reading, or read again after a change elsewhere. */
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
    this.offset.update((value) => Math.max(0, value + direction * 40));
  }
}
