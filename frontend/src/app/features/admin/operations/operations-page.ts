import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { apiResource } from '../../../core/api/api-resource';
import { FeaturePage } from '../../../core/layout/feature-page';
import { WorkspaceSession } from '../../../core/auth/workspace-session';
import { Notice } from '../../../shared/ui/notice';
import { ViewSwitch } from '../../../shared/ui/view-switch';
import { PriceBook } from '../../billing/price-book';
import { PlatformUsage } from './platform-usage';
import { RetrievalAdmin } from './retrieval-admin';

/** Platform operations: usage, retrieval indexes and model and tool prices, one tab each. */
@Component({
  selector: 'nx-operations-page',
  imports: [FeaturePage, Notice, ViewSwitch, PlatformUsage, RetrievalAdmin, PriceBook],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './operations-page.html',
})
export class OperationsPage {
  readonly session = inject(WorkspaceSession);
  /** The tabs read their own data; the page reads the account to know whether to show them. */
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
