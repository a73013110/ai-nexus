import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { UnitImportPreviewDto, UnitImportResultDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ApiError, safeMessage } from '@core/errors/safe-errors';
import { ViewScope } from '@shared/browser/view-scope';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';
import { StatusBadge } from '@shared/ui/status-badge';
import { OrganizationApi } from '../organization-api';
import { unitImportTour } from '../organization-tour';
import {
  ACTION_LABELS,
  ACTION_TONES,
  changeNote,
  hasChanges,
  importCounts,
  issueLabel,
} from './unit-import-presentation';

/**
 * 從公文系統匯入部門與主管（`organization.import`）：開啟時先預覽（不寫入），確認後套用。
 * 套用送回預覽的指紋，伺服器重算後不同就拒絕（`organization_import_stale`），這時清掉舊預覽、請使用者重新預覽，
 * 不會套用沒看過的變更。測試身分可以預覽，但不能套用。
 */
@Component({
  selector: 'nx-unit-import-dialog',
  imports: [CompactDialog, DialogHeader, Notice, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    #importDialog
    class="platform-dialog"
    size="lg"
    (cancel)="cancel($event)"
  >
    <nx-dialog-header
      heading="從公文系統匯入"
      closeLabel="關閉匯入"
      [detail]="detail()"
      [tour]="tour()"
    />
    <div class="dialog-body" [attr.aria-busy]="loading()">
      @if (error()) {
        <nx-notice tone="danger" [message]="error()"
          ><button notice-actions type="button" [disabled]="busy()" (click)="refresh()">
            重新預覽
          </button></nx-notice
        >
      }
      @if (loading()) {
        <p role="status" class="form-note">正在讀取公文系統的部門與主管…</p>
      } @else if (preview(); as p) {
        @let n = counts()!;
        <ul class="import-summary" aria-label="變動摘要">
          <li>
            新增 <strong>{{ n.create }}</strong>
          </li>
          <li>
            綁定 <strong>{{ n.adopt }}</strong>
          </li>
          <li>
            更新 <strong>{{ n.update }}</strong>
          </li>
          <li>
            停用 <strong>{{ n.disable }}</strong>
          </li>
          <li>
            設為主管 <strong>{{ n.managersAdded }}</strong>
          </li>
          <li>
            取消主管 <strong>{{ n.managersRemoved }}</strong>
          </li>
          <li>
            問題 <strong>{{ n.issues }}</strong>
          </li>
        </ul>
        <p class="form-note import-rule">
          來自公文系統的單位，名稱、上層、啟用、順序與主管以公文系統為準；手建的單位不受影響，公文系統已沒有的部門只停用、不刪除。
        </p>
        @if (!changed()) {
          <p class="form-note" role="status">組織已與公文系統一致，沒有需要套用的變動。</p>
        }
        @if (p.units.length) {
          <section class="import-section import-units" aria-labelledby="unit-import-units">
            <h3 id="unit-import-units">
              單位 <span class="ui-count-label">{{ p.units.length }} 個</span>
            </h3>
            <ul class="import-list">
              @for (change of p.units; track change.gdDepNo) {
                <li>
                  <nx-status-badge [tone]="tones[change.action]">{{
                    labels[change.action]
                  }}</nx-status-badge>
                  <span class="import-text"
                    ><strong>{{ change.path.join(' › ') }}</strong>
                    @if (note(change); as text) {
                      <small>{{ text }}</small>
                    }
                  </span>
                </li>
              }
            </ul>
          </section>
        }
        @if (p.managers.length) {
          <section class="import-section import-managers" aria-labelledby="unit-import-managers">
            <h3 id="unit-import-managers">
              主管 <span class="ui-count-label">{{ p.managers.length }} 筆</span>
            </h3>
            <ul class="import-list">
              @for (manager of p.managers; track manager.gdDepNo + ':' + manager.userId) {
                <li>
                  <nx-status-badge [tone]="manager.added ? 'success' : 'neutral'">{{
                    manager.added
                      ? manager.deputy
                        ? '設為主管（副主管）'
                        : '設為主管'
                      : '取消主管'
                  }}</nx-status-badge>
                  <span class="import-text"
                    ><strong>{{ manager.displayName || manager.account }}</strong>
                    <small
                      >{{ manager.path.join(' › ')
                      }}{{
                        manager.joinsUnit ? '；並加入為成員' : manager.added ? '' : '；保留成員資格'
                      }}</small
                    >
                  </span>
                </li>
              }
            </ul>
          </section>
        }
        @if (p.issues.length) {
          <section class="import-section import-issues" aria-labelledby="unit-import-issues">
            <h3 id="unit-import-issues">
              問題 <span class="ui-count-label">{{ p.issues.length }} 筆</span>
            </h3>
            <p class="form-note">
              這些列或帳號不會匯入，其他變動照常套用；請在公文系統修正，或讓對應的人先登入平台後再匯入。
            </p>
            <ul class="import-list">
              @for (issue of p.issues; track $index) {
                <li>
                  <nx-status-badge tone="warning">{{ issueText(issue) }}</nx-status-badge>
                  <span class="import-text"
                    ><strong>{{ issue.name || '（沒有名稱）' }}</strong>
                    <small
                      >部門編號 {{ issue.gdDepNo
                      }}{{ issue.gdUserId ? '；公文帳號 ' + issue.gdUserId : '' }}</small
                    >
                  </span>
                </li>
              }
            </ul>
          </section>
        }
      }
    </div>
    <footer class="dialog-foot">
      @if (session.testing()) {
        <span class="dialog-note" role="status">{{ session.testingNote() }}</span>
      }
      <button type="button" class="secondary-button" [disabled]="applying()" (click)="close()">
        取消
      </button>
      <button
        type="button"
        class="primary-button unit-import-apply"
        [disabled]="!canApply()"
        (click)="apply()"
      >
        {{ applying() ? '匯入中…' : '套用匯入' }}
      </button>
    </footer>
  </dialog>`,
  styles: `
    .import-summary {
      display: flex;
      flex-wrap: wrap;
      gap: var(--p-space-2) var(--p-space-4);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .import-section {
      display: grid;
      gap: var(--p-space-2);
    }
    .import-section h3 {
      margin: 0;
    }
    .import-list {
      display: grid;
      gap: var(--p-space-1);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .import-list li {
      display: flex;
      gap: var(--p-space-2);
      align-items: baseline;
      padding: var(--p-space-2) var(--p-space-3);
      border: 1px solid var(--line);
      border-radius: var(--radius-item);
    }
    .import-text {
      display: grid;
      min-width: 0;
    }
    .import-text small {
      color: var(--secondary);
      font-size: var(--text-caption);
    }
  `,
})
export class UnitImportDialog {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(OrganizationApi);
  private readonly scope = inject(ViewScope);
  /** 套用成功；頁面據此重新載入並提示摘要。 */
  readonly applied = output<UnitImportResultDto>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('importDialog');
  readonly labels = ACTION_LABELS;
  readonly tones = ACTION_TONES;
  readonly note = changeNote;
  readonly issueText = issueLabel;
  readonly preview = signal<UnitImportPreviewDto | null>(null);
  readonly loading = signal(false);
  readonly applying = signal(false);
  readonly error = signal('');
  readonly busy = computed(() => this.loading() || this.applying());
  readonly counts = computed(() => {
    const preview = this.preview();
    return preview ? importCounts(preview) : null;
  });
  readonly changed = computed(() => {
    const preview = this.preview();
    return !!preview && hasChanges(preview);
  });
  readonly tour = computed(() => unitImportTour({ testing: this.session.testing() }));
  readonly canApply = computed(() => this.changed() && !this.busy() && !this.session.testing());
  readonly detail = computed(() => {
    const preview = this.preview();
    return preview ? `公文系統共 ${preview.sourceUnits} 個部門` : '';
  });

  open() {
    this.preview.set(null);
    this.error.set('');
    this.dialog().nativeElement.showModal();
    void this.refresh();
  }
  close() {
    if (!this.applying()) this.dialog().nativeElement.close();
  }
  cancel(event: Event) {
    event.preventDefault();
    this.close();
  }

  async refresh() {
    if (this.busy()) return;
    const alive = this.scope.guard();
    this.loading.set(true);
    this.error.set('');
    try {
      const preview = await this.api.previewImport();
      if (alive()) this.preview.set(preview);
    } catch (error) {
      if (!alive()) return;
      this.preview.set(null);
      this.error.set(safeMessage(error));
    } finally {
      if (alive()) this.loading.set(false);
    }
  }

  async apply() {
    const preview = this.preview();
    if (!preview || !this.canApply()) return;
    const alive = this.scope.guard();
    this.applying.set(true);
    this.error.set('');
    try {
      const result = await this.api.applyImport(preview.fingerprint);
      if (!alive()) return;
      this.dialog().nativeElement.close();
      this.applied.emit(result);
    } catch (error) {
      if (!alive()) return;
      // 預覽過期：舊的清單已不代表會套用的內容，清掉它，請使用者重新預覽。
      if (error instanceof ApiError && error.code === 'organization_import_stale')
        this.preview.set(null);
      this.error.set(safeMessage(error));
    } finally {
      if (alive()) this.applying.set(false);
    }
  }
}
