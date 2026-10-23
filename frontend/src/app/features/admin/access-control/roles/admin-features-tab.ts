import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import type { AdminFeatureDto } from '@core/api/schema';
import { featureIcon } from '@core/layout/navigation-tree';
import { DataTable, DataTableRow, TableState } from '@shared/ui/data-table';
import { Icon } from '@shared/ui/icon';
import { AccessControlStore } from '../access-control-store';
import { AccessStatus } from './access-status';

/**
 * 功能來自功能目錄（網址與圖示不可改），這裡只調整名稱與開關，依目錄的宣告順序列出（與伺服器相同）。
 * 側欄的位置與順序在選單頁（`access-control.navigation`）安排，不在這裡。
 */
@Component({
  selector: 'nx-admin-features-tab',
  imports: [Icon, DataTable, DataTableRow, TableState, AccessStatus],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['../../../../shared/ui/management-table-page.scss', './admin-features-tab.scss'],
  templateUrl: './admin-features-tab.html',
})
export class AdminFeaturesTab {
  readonly store = inject(AccessControlStore);
  readonly edit = output<AdminFeatureDto>();
  readonly icon = featureIcon;
  readonly features = computed(() => this.store.catalog()?.features ?? []);
}
