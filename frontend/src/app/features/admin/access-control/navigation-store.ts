import { Injectable, computed, inject, signal } from '@angular/core';
import type { NavigationItemDto } from '@core/api/schema';
import { apiResource } from '@core/api/api-resource';
import { catalogFeature } from '@core/api/catalog';
import { ToastService } from '@shared/ui/toast-service';
import { AccessControlApi } from './access-control-api';
import { AccessControlStore } from './access-control-store';

/**
 * 選單頁提供（`providers`）的選單節點：伺服器的平面清單（含外部連結的可見群組），頁面的樹、
 * 編輯對話框與移動共用。功能名稱與群組名稱來自同頁的存取目錄（`AccessControlStore`）。
 */
@Injectable()
export class NavigationStore {
  private readonly api = inject(AccessControlApi);
  private readonly access = inject(AccessControlStore);
  private readonly read = apiResource({
    feature: 'access-control.navigation',
    loader: () => this.api.navigationItems(),
  });
  readonly items = computed(() => this.read.value() ?? []);
  readonly ready = computed(() => this.read.value() !== undefined && !!this.access.catalog());
  readonly loading = computed(() => this.read.loading() || this.access.loading());
  readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.read.error() || this.access.error());
  readonly toast = inject(ToastService);

  load() {
    this.actionError.set('');
    this.read.reload();
    this.access.load();
  }
  /** 先在畫面上換成新的清單（移動的預覽或失敗時還原）；伺服器的值由 `changed()` 重讀。 */
  replace(items: NavigationItemDto[]) {
    this.read.value.set(items);
  }
  /** 選單已寫入：重讀節點，以及此帳號的側欄（`/me` 的選單樹）。 */
  changed() {
    this.read.reload();
    this.access.navigationChanged();
  }
  /** 頁面節點顯示功能的有效名稱（含管理員的覆寫）；目錄外的 ID 原樣顯示。 */
  name(item: NavigationItemDto) {
    if (item.kind !== 'page') return item.name ?? '';
    const id = item.featureId ?? '';
    return (
      this.access.catalog()?.features.find((feature) => feature.id === id)?.name ||
      catalogFeature(id)?.name ||
      id
    );
  }
  /** 頁面的功能已停用時，任何人的選單都不會出現它。 */
  disabled(item: NavigationItemDto) {
    return (
      item.kind === 'page' &&
      this.access.catalog()?.features.find((feature) => feature.id === item.featureId)?.enabled ===
        false
    );
  }
}
