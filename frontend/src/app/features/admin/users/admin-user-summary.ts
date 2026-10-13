import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { WorkspaceSession } from '../../../core/auth/workspace-session';
import { formatBytes, formatDate, formatNumber } from '../../../shared/browser/format';
import { Icon } from '../../../shared/ui/icon';
import { NumberTicker } from '../../../shared/ui/number-ticker';
import { AccessStatus } from '../access/access-status';
import { AdminStore } from '../admin-store';
import { AdminUserStore } from './admin-user-store';
import { signInMethods } from './user-account-fields';

/** Who the user is and their last 30 days, above every inspector tab. */
@Component({
  selector: 'nx-admin-user-summary',
  imports: [AccessStatus, Icon, NumberTicker, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './admin-user-summary.scss',
  template: `
    @if (store.current(); as user) {
      <div class="summary-identity">
        <div class="summary-title">
          <h2 id="inspector-title">{{ user.displayName }}</h2>
          <nx-access-status [enabled]="user.enabled !== false" />
        </div>
        <p class="summary-meta">
          <span>{{ user.account }}</span
          ><span>登入：{{ methods(user) }}</span
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
      @if (session.has('audit')) {
        <a
          class="quiet-button summary-audit"
          routerLink="/admin/audit"
          [queryParams]="{ search: user.id }"
          (click)="leave.emit()"
        >
          <nx-icon name="audit" />查閱活動稽核
        </a>
      }
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
  `,
})
export class AdminUserSummary {
  readonly store = inject(AdminUserStore);
  readonly session = inject(WorkspaceSession);
  private readonly admin = inject(AdminStore);
  /** The audit link leaves for another page, so the dialog closes first. */
  readonly leave = output<void>();
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
