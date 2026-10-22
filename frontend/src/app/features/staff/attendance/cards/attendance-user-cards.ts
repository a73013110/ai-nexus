import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import { routeOf } from '@core/api/catalog';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { formatDate } from '@shared/browser/format';
import { uniqueId } from '@shared/browser/unique-id';
import { Notice } from '@shared/ui/notice';
import { StatusBadge } from '@shared/ui/status-badge';
import { AttendanceApi } from '../attendance-api';
import { cardKindLabel } from '../attendance-presentation';
import { covers } from './attendance-cards-store';

/**
 * 某位使用者持有過的出勤卡（唯讀），給使用者管理的檢視器嵌入；Identity 不依賴出勤，由這裡另外呼叫出勤 API。
 * 沒有 `attendance.cards` 時整個區塊不顯示、也不發請求；指派與結束持有到卡號對應頁做，連結以姓名搜尋帶入。
 * 讀取以使用者 ID 為鍵，切換使用者時不會短暫顯示上一位的卡。
 */
@Component({
  selector: 'nx-attendance-user-cards',
  imports: [Notice, RouterLink, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: contents;
    }
    .user-cards {
      display: grid;
      gap: var(--p-space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .user-cards > li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--p-space-1) var(--p-space-2);
      padding: 8px 12px;
      background: var(--canvas);
      border-radius: var(--radius-block);
      font-size: var(--text-caption);
    }
    .user-cards-number {
      font-family: var(--font-code);
      font-variant-numeric: tabular-nums;
    }
    .user-cards-period {
      margin-inline-start: auto;
      color: var(--secondary);
      font-variant-numeric: tabular-nums;
    }
  `,
  template: `
    @if (visible()) {
      <section class="form-section" role="group" [attr.aria-labelledby]="headingId">
        <header class="form-section-head">
          <h3 [id]="headingId">出勤卡號</h3>
          <p class="form-note">刷卡依持有期間歸戶到此人；指派與結束持有在卡號對應頁。</p>
        </header>
        <div class="form-section-body">
          @if (read.error()) {
            <nx-notice tone="danger" [message]="read.error()"
              ><button
                notice-actions
                type="button"
                class="secondary-button"
                (click)="read.reload()"
              >
                重試
              </button></nx-notice
            >
          } @else if (periods() === null) {
            <p class="form-note" role="status">正在取得卡號…</p>
          } @else {
            <ul class="user-cards" aria-label="持有期間">
              @for (entry of periods(); track entry.item.id) {
                <li>
                  <strong class="user-cards-number">{{ entry.item.cardNumber }}</strong>
                  <nx-status-badge tone="info">{{ entry.kind }}</nx-status-badge>
                  @if (entry.current) {
                    <nx-status-badge tone="success">持有中</nx-status-badge>
                  }
                  <span class="user-cards-period"
                    ><time [attr.datetime]="entry.item.startsAt">{{ entry.starts }}</time> –
                    @if (entry.item.endsAt) {
                      <time [attr.datetime]="entry.item.endsAt">{{ entry.ends }}</time>
                    } @else {
                      持有中
                    }
                  </span>
                </li>
              } @empty {
                <li class="form-note user-cards-empty">此人沒有持有過出勤卡。</li>
              }
            </ul>
          }
          @if (session.has('attendance.cards.maintain')) {
            <a
              class="quiet-button user-cards-manage"
              [routerLink]="route"
              [queryParams]="{ search: displayName() }"
              >在卡號對應管理</a
            >
          }
        </div>
      </section>
    }
  `,
})
export class AttendanceUserCards {
  readonly userId = input.required<string>();
  /** 連到卡號對應頁時的搜尋字；該頁的搜尋比對持有人姓名與使用者名稱。 */
  readonly displayName = input.required<string>();
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AttendanceApi);
  readonly route = routeOf('attendance.cards');
  readonly headingId = uniqueId('nx-attendance-user-cards');
  readonly visible = computed(() => this.session.has('attendance.cards'));
  readonly read = apiResource({
    feature: 'attendance.cards',
    // 沒有權限時保持閒置，連請求都不發。
    params: () => (this.visible() ? this.userId() : undefined),
    loader: async (id, signal) => ({ id, items: await this.api.userAssignments(id, signal) }),
  });
  /** 這位使用者的持有期間，新到舊；尚未讀到（或還是上一位的結果）時為 null。 */
  readonly periods = computed(() => {
    const loaded = this.read.value();
    if (!loaded || loaded.id !== this.userId()) return null;
    const now = Date.now();
    return [...loaded.items]
      .sort((a, b) => Date.parse(b.startsAt) - Date.parse(a.startsAt))
      .map((item) => ({
        item,
        // 持有期間只能指派在員工卡上，有持有期間的卡也不能改成訪客卡（`CreateCardAssignment`、`UpdateCard`），
        // 所以持有期間的卡一定是員工卡；回應不帶種類，以此顯示。
        kind: cardKindLabel('employee'),
        current: covers(item, now),
        starts: formatDate(item.startsAt),
        ends: item.endsAt ? formatDate(item.endsAt) : '',
      }));
  });
}
