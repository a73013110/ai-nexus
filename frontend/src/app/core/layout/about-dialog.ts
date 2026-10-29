import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import { apiResource } from '@core/api/api-resource';
import type { ReleaseNoteDto } from '@core/api/schema';
import { formatDate } from '@shared/browser/format';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';

const GROUPS = [
  { type: 'feat', label: '新功能' },
  { type: 'fix', label: '修正' },
] as const;

/** 本版更新依 commit 類型分組；只列 `feat` 與 `fix`，其餘（文件、重構）使用者感覺不到。 */
export function releaseNoteGroups(changes: readonly ReleaseNoteDto[]) {
  return GROUPS.map((group) => ({
    label: group.label,
    items: changes.filter((change) => change.type === group.type),
  })).filter((group) => group.items.length);
}

/** 使用者選單的「關於」：版本、發布日期與本版更新。開啟時才讀，同一次登入只讀一次。 */
@Component({
  selector: 'nx-about-dialog',
  imports: [CompactDialog, DialogHeader, Notice, PendingButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './about-dialog.scss',
  template: `<dialog nxCompactDialog size="sm" #dialog class="platform-dialog" aria-label="關於">
    <nx-dialog-header heading="關於 AI Nexus" />
    <div class="dialog-body">
      @if (release.value(); as current) {
        <dl class="ui-description-list">
          <div>
            <dt>版本</dt>
            <dd class="about-code">v{{ current.version }}</dd>
          </div>
          @if (current.builtAt) {
            <div>
              <dt>發布日期</dt>
              <dd>{{ date(current.builtAt) }}</dd>
            </div>
          }
          @if (current.revision) {
            <div>
              <dt>修訂</dt>
              <dd class="about-code" [title]="current.revision">
                {{ current.revision.slice(0, 7) }}
              </dd>
            </div>
          }
        </dl>
        @for (group of groups(); track group.label) {
          <section class="about-notes">
            <h3 class="ui-section-heading">{{ group.label }}</h3>
            <ul>
              @for (item of group.items; track $index) {
                <li>{{ item.subject }}</li>
              }
            </ul>
          </section>
        } @empty {
          <p class="form-note">這一版沒有附上更新說明。</p>
        }
      } @else if (release.error()) {
        <nx-notice tone="danger" [message]="release.error()">
          <button
            nxPending
            notice-actions
            type="button"
            class="secondary-button"
            [pending]="release.refreshing()"
            (click)="release.reload()"
          >
            重試
          </button>
        </nx-notice>
      } @else {
        <div class="about-loading" role="status">
          <span class="sr-only">正在載入版本資訊…</span>
          <span class="skeleton" aria-hidden="true"></span>
          <span class="skeleton" aria-hidden="true"></span>
          <span class="skeleton" aria-hidden="true"></span>
        </div>
      }
    </div>
  </dialog>`,
})
export class AboutDialog {
  private readonly api = inject(ApiClient);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly requested = signal(false);
  readonly release = apiResource({
    params: () => this.requested() || undefined,
    loader: (_, signal) => this.api.get('/api/v1/releases/current', { signal }),
  });
  readonly groups = computed(() => releaseNoteGroups(this.release.value()?.changes ?? []));
  readonly date = formatDate;
  open() {
    this.requested.set(true);
    this.dialog().nativeElement.showModal();
  }
}
