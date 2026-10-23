import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { safeMessage } from '@core/errors/safe-errors';
import { ChangeList } from '@shared/ui/change-list';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { PropertyList } from '@shared/ui/property-list';
import { StatusBadge } from '@shared/ui/status-badge';
import { accountAlerts, attributeGroups } from './directory-attributes';
import { DirectoryImportStore } from './directory-import-store';
import { fieldChanges, stateLabel, stateTone } from './directory-outcomes';

/**
 * 右側「AD 屬性」：標題下先列會擋住登入的狀態（停用、鎖定、密碼過期），再是匯入時會寫入平台的差異，
 * 最後是分組、可篩選的完整 AD 屬性（點選時才讀）。服務帳號是唯讀連線時明確標示；寫回 AD 是預留的擴充點，尚未開放。
 */
@Component({
  selector: 'nx-directory-person-card',
  imports: [ChangeList, EmptyState, Icon, Notice, PendingButton, PropertyList, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './directory-person-card.scss',
  template: `@if (store.inspectedPerson(); as person) {
      <header>
        <h4 tabindex="-1">{{ person.displayName }}</h4>
        <span class="directory-person-account">{{ person.account }}</span>
        <div class="directory-person-badges">
          <nx-status-badge [tone]="stateTone(person.state)">{{
            stateLabel(person)
          }}</nx-status-badge>
          @for (alert of alerts(); track alert.label) {
            <nx-status-badge [tone]="alert.tone">{{ alert.label }}</nx-status-badge>
          } @empty {
            @if (person.disabled) {
              <nx-status-badge tone="warning">已停用</nx-status-badge>
            }
          }
        </div>
      </header>
      @if (person.changes.length) {
        <section class="directory-person-changes" aria-labelledby="directory-person-changes">
          <h5 id="directory-person-changes">匯入時會更新</h5>
          <nx-change-list [changes]="changes()" />
        </section>
      }
      @if (store.inspectedDetail()) {
        <nx-property-list
          label="AD 屬性"
          [groups]="groups()"
          [class.is-refreshing]="store.detail.refreshing() || store.refreshing()"
          [attr.aria-busy]="store.detail.refreshing() || store.refreshing() || null"
        />
      } @else if (store.detailError()) {
        <nx-notice tone="danger" [message]="error()" />
        <button
          nxPending
          type="button"
          class="secondary-button"
          [pending]="store.detail.refreshing()"
          (click)="store.detail.reload()"
        >
          <nx-icon name="refresh" />{{ store.detail.refreshing() ? '重新讀取中…' : '重新讀取' }}
        </button>
      } @else {
        <div class="directory-person-skeleton" role="status">
          @for (width of skeleton; track $index) {
            <span class="skeleton" [style.width.%]="width"></span>
          }
          <span class="sr-only">正在載入 AD 屬性…</span>
        </div>
      }
      @if (!store.writable()) {
        <p class="directory-person-locked">
          <nx-icon name="lock" /><span
            ><strong>唯讀連線</strong>目前只讀取 AD 與匯入平台，不會寫回 AD。</span
          >
        </p>
      }
    } @else {
      <nx-empty-state>
        <nx-icon name="user" />
        <strong>選一個人查看 AD 屬性</strong>
        <span>點清單中的人，或在匯入籃點名字，就會在這裡列出 AD 上的完整屬性。</span>
      </nx-empty-state>
    }`,
})
export class DirectoryPersonCard {
  readonly store = inject(DirectoryImportStore);
  readonly stateLabel = stateLabel;
  readonly stateTone = stateTone;
  protected readonly skeleton = [40, 86, 72, 90, 64, 80, 56];
  readonly changes = computed(() => fieldChanges(this.store.inspectedPerson()?.changes ?? []));
  readonly alerts = computed(() => {
    const detail = this.store.inspectedDetail();
    return detail ? accountAlerts(detail) : [];
  });
  readonly groups = computed(() => {
    const detail = this.store.inspectedDetail();
    return detail ? attributeGroups(detail) : [];
  });
  readonly error = computed(() => safeMessage(this.store.detailError()));
}
