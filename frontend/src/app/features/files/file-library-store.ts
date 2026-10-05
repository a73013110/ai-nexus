import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import type { FileLibraryPage } from '../../core/api/types';
import { ViewScope } from '../../shared/browser/view-scope';
import { FilesApi } from './files-api';

/** Bounded server-side browsing, shared by the library and file chooser. */
@Injectable()
export class FileLibraryStore {
  private readonly api = inject(FilesApi);
  private readonly scope = inject(ViewScope);
  readonly result = signal<FileLibraryPage | null>(null);
  readonly search = signal('');
  readonly type = signal('all');
  readonly source = signal('all');
  readonly offset = signal(0);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly items = computed(() => this.result()?.items ?? []);
  readonly total = computed(() => this.result()?.total ?? 0);
  readonly canNext = computed(() => this.offset() + this.items().length < this.total());
  private controller?: AbortController;
  private version = 0;
  constructor() {
    inject(DestroyRef).onDestroy(() => this.controller?.abort());
  }
  async load() {
    const version = ++this.version,
      guard = this.scope.guard(),
      valid = () => guard() && version === this.version;
    this.controller?.abort();
    const controller = (this.controller = new AbortController());
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.api.list(
        { search: this.search(), type: this.type(), source: this.source(), offset: this.offset() },
        controller.signal,
      );
      if (valid()) this.result.set(result);
    } catch (error) {
      if (valid() && !controller.signal.aborted) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  find(value: string) {
    this.search.set(value);
    this.offset.set(0);
    ++this.version;
    this.controller?.abort();
    this.loading.set(true);
    this.scope.later(() => void this.load(), 220, 'files-search');
  }
  filter(type: string, source = this.source()) {
    this.type.set(type);
    this.source.set(source);
    this.offset.set(0);
    void this.load();
  }
  page(direction: number) {
    this.offset.update((value) => Math.max(0, value + direction * 40));
    void this.load();
  }
}
