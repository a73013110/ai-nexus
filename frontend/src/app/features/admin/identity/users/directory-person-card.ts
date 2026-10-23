import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { formatDate } from '@shared/browser/format';
import { ChangeList } from '@shared/ui/change-list';
import { CopyValue } from '@shared/ui/copy-value';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';
import { StatusBadge } from '@shared/ui/status-badge';
import { DirectoryImportStore } from './directory-import-store';
import { fieldChanges, stateLabel, stateTone } from './directory-outcomes';

/**
 * 右側「AD 屬性」：一位人員在 AD 上的值（括號內是 LDAP 屬性名，方便與 AD 工具對照），
 * 以及匯入時會寫入平台的差異。服務帳號是唯讀連線時明確標示；寫回 AD 是預留的擴充點，尚未開放。
 */
@Component({
  selector: 'nx-directory-person-card',
  imports: [ChangeList, CopyValue, EmptyState, Icon, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './directory-person-card.scss',
  template: `@if (store.inspectedPerson(); as person) {
      <header>
        <h4>{{ person.displayName }}</h4>
        <div class="resource-badges">
          <nx-status-badge [tone]="stateTone(person.state)">{{
            stateLabel(person)
          }}</nx-status-badge>
          @if (person.disabled) {
            <nx-status-badge tone="warning">AD 停用</nx-status-badge>
          }
        </div>
      </header>
      @if (person.changes.length) {
        <section class="directory-person-changes" aria-labelledby="directory-person-changes">
          <h5 id="directory-person-changes">匯入時會更新</h5>
          <nx-change-list [changes]="changes()" />
        </section>
      }
      <dl>
        @for (attribute of attributes(); track attribute.key) {
          <div>
            <dt>
              {{ attribute.label }}<code>{{ attribute.key }}</code>
            </dt>
            <dd>{{ attribute.value || '—' }}</dd>
          </div>
        }
        <div>
          <dt>辨別名稱<code>distinguishedName</code></dt>
          <dd><nx-copy-value label="複製 DN" [value]="person.dn" /></dd>
        </div>
      </dl>
      @if (!store.writable()) {
        <p class="directory-person-locked">
          <nx-icon name="lock" /><span
            ><strong>唯讀連線</strong>目前只讀取 AD 與匯入平台，不會寫回 AD。</span
          >
        </p>
      }
    } @else {
      <nx-empty-state>
        <nx-icon name="sidebar" />
        <strong>選一個人查看 AD 屬性</strong>
        <span>按清單列尾的面板鈕，對照 AD 的值與匯入時會改的欄位。</span>
      </nx-empty-state>
    }`,
})
export class DirectoryPersonCard {
  readonly store = inject(DirectoryImportStore);
  readonly stateLabel = stateLabel;
  readonly stateTone = stateTone;
  readonly changes = computed(() => fieldChanges(this.store.inspectedPerson()?.changes ?? []));
  readonly attributes = computed(() => {
    const person = this.store.inspectedPerson();
    if (!person) return [];
    return [
      { key: 'displayName', label: '顯示名稱', value: person.displayName },
      { key: 'sAMAccountName', label: '帳號', value: person.account },
      { key: 'userPrincipalName', label: 'UPN', value: person.upn },
      { key: 'mail', label: '信箱', value: person.email },
      { key: 'department', label: '部門', value: person.department },
      { key: 'title', label: '職稱', value: person.title },
      { key: 'ou', label: '位置', value: person.path },
      {
        key: 'whenChanged',
        label: 'AD 最後異動',
        value: person.whenChanged ? formatDate(person.whenChanged) : null,
      },
      {
        // lastLogonTimestamp 只在網域控制站之間約略同步（預設最多落後 14 天），只能當參考。
        key: 'lastLogonTimestamp',
        label: '最近登入（約略）',
        value: person.lastLogon ? formatDate(person.lastLogon) : '從未登入',
      },
    ];
  });
}
