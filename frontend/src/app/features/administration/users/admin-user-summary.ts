import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { formatBytes, formatDate, formatNumber } from '@shared/browser/format';
import { Notice } from '@shared/ui/notice';
import { NumberTicker } from '@shared/ui/number-ticker';
import { AdministrationStore } from '../administration-store';
import { AdminUserStore } from './admin-user-store';
import { signInMethods } from './user-account-fields';

/**
 * How the user signs in, their roles and their last 30 days, between the inspector's title bar
 * (name, status, account) and its tabs.
 */
@Component({
  selector: 'nx-admin-user-summary',
  imports: [Notice, NumberTicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './admin-user-summary.scss',
  template: `
    @if (store.current(); as user) {
      <div class="summary-identity">
        <p class="summary-meta">
          <span>登入：{{ methods(user) }}</span
          ><span>最近活動 {{ date(user.lastSeenAt) }}</span>
        </p>
        <div class="resource-badges summary-roles" aria-label="角色">
          @for (role of roles(); track role) {
            <span>{{ role }}</span>
          } @empty {
            <small class="form-note">尚未分派角色</small>
          }
        </div>
      </div>
      <div class="inspector-stats" role="group" aria-label="最近三十天 AI 用量">
        <div class="summary-stat">
          <span>30 天生成</span>
          <strong><nx-number-ticker [value]="usage()?.requests" /></strong>
          <small>{{
            usage() ? usage()!.completed + ' 完成 · ' + usage()!.failed + ' 失敗' : '讀取中…'
          }}</small>
        </div>
        <div class="summary-stat">
          <span>回報 tokens</span>
          <strong><nx-number-ticker [value]="tokens()" /></strong>
          <small>{{
            usage()
              ? '輸入 ' + format(usage()!.inputTokens) + ' · 輸出 ' + format(usage()!.outputTokens)
              : '只計模型回報'
          }}</small>
        </div>
        <div class="summary-stat">
          <span>對話與附件</span>
          <strong><nx-number-ticker [value]="store.overview()?.conversations" /><em>段</em></strong>
          <small>{{
            usage() ? '附件 ' + bytes(usage()!.storage.usedBytes) : '含封存與已刪除'
          }}</small>
        </div>
      </div>
    }
    @if (store.overviewError()) {
      <nx-notice tone="danger" [message]="store.overviewError()" />
    }
  `,
})
export class AdminUserSummary {
  readonly store = inject(AdminUserStore);
  private readonly admin = inject(AdministrationStore);
  readonly usage = computed(() => this.store.overview()?.usage ?? null);
  readonly tokens = computed(() => {
    const usage = this.usage();
    return usage ? usage.inputTokens + usage.outputTokens : null;
  });
  readonly roles = computed(() => this.admin.roleNames(this.store.current()?.roleIds ?? []));
  readonly methods = signInMethods;
  readonly date = formatDate;
  readonly format = formatNumber;
  readonly bytes = formatBytes;
}
