import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { apiResource } from '@core/api/api-resource';
import { FeatureAccessNotice } from '@core/layout/feature-access-notice';
import { FeaturePage } from '@core/layout/feature-page';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { Notice } from '@shared/ui/notice';
import { ViewMotion } from '@shared/ui/view-motion';
import { ViewSwitch } from '@shared/ui/view-switch';
import { PriceBook } from '@features/admin/billing/price-book';
import { PlatformUsage } from './platform-usage';
import { RetrievalAdmin } from '@features/work/knowledge/retrieval/retrieval-admin';

/** 平台營運：用量、檢索索引，以及模型與工具價格，各一個分頁。 */
@Component({
  selector: 'nx-operations-page',
  imports: [
    FeaturePage,
    FeatureAccessNotice,
    Notice,
    ViewMotion,
    ViewSwitch,
    PlatformUsage,
    RetrievalAdmin,
    PriceBook,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './operations-page.html',
})
export class OperationsPage {
  readonly session = inject(WorkspaceSession);
  /** 各分頁讀取自己的資料；頁面讀取帳號以決定是否顯示它們。 */
  readonly account = apiResource({ loader: () => this.session.load() });
  private readonly router = inject(Router);
  readonly tab = signal('usage');
  readonly tabs = [
    { value: 'usage', label: '平台用量' },
    { value: 'retrieval', label: '知識檢索' },
    { value: 'prices', label: '模型與工具價格' },
  ];
  constructor() {
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        const tab = params.get('tab') || 'usage';
        if (this.tabs.some((item) => item.value === tab)) this.tab.set(tab);
      });
  }
  selectTab(id: string) {
    this.tab.set(id);
    void this.router.navigate([], { queryParams: { tab: id }, queryParamsHandling: 'merge' });
  }
}
