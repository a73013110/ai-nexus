import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  linkedSignal,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { UnitImportPreviewDto, UnitImportResultDto } from '@core/api/schema';
import { ApiError, safeMessage } from '@core/errors/safe-errors';
import { ViewScope } from '@shared/browser/view-scope';
import { ChoiceGroups } from '@shared/ui/choice-groups';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { StatusBadge } from '@shared/ui/status-badge';
import { OrganizationApi } from '../organization-api';
import { unitImportTour } from '../organization-tour';
import {
  hasChanges,
  importChoices,
  importCounts,
  importDepartments,
  issueLabel,
} from './unit-import-presentation';

/**
 * 從公文系統匯入部門與主管（`organization.import`）：開啟時先預覽（不寫入），勾選要套用的部門後套用。
 * 一個部門的單位與主管變動一起勾選；本地已停用、套用會重新啟用的部門預設不勾。未勾選的這次不動，下次預覽仍會列出。
 * 套用送回預覽的指紋與未勾選的部門，伺服器重算後不同就拒絕（`organization_import_stale`），這時清掉舊預覽、
 * 請使用者重新預覽，不會套用沒看過的變更。
 */
@Component({
  selector: 'nx-unit-import-dialog',
  imports: [ChoiceGroups, CompactDialog, DialogHeader, Notice, PendingButton, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    #importDialog
    class="platform-dialog"
    size="lg"
    [busy]="applying()"
    (cancel)="cancel($event)"
  >
    <nx-dialog-header
      heading="從公文系統匯入"
      closeLabel="關閉匯入"
      [detail]="detail()"
      [tour]="tour"
    />
    <div class="dialog-body" [attr.aria-busy]="loading() || null">
      @if (error()) {
        <nx-notice tone="danger" [message]="error()"
          ><button
            notice-actions
            nxPending
            type="button"
            [pending]="loading()"
            [disabled]="applying()"
            (click)="refresh()"
          >
            {{ loading() ? '預覽中…' : '重新預覽' }}
          </button></nx-notice
        >
      }
      @if (loading() && !error()) {
        <div class="import-skeleton" role="status">
          @for (width of skeleton; track $index) {
            <span class="skeleton" [style.width.%]="width"></span>
          }
          <span class="sr-only">正在載入公文系統的部門與主管…</span>
        </div>
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
        } @else {
          <section class="import-section import-units" aria-labelledby="unit-import-units">
            <h3 id="unit-import-units">
              部門
              <span class="ui-count-label"
                >已選 {{ selected().length }}／{{ departments().length }}</span
              >
            </h3>
            <p class="form-note">
              勾選這次要套用的部門；未勾選的維持原樣，下次匯入仍會列出。本地已停用、套用會重新啟用的部門預設不勾。
            </p>
            <nx-choice-groups
              label="要套用的部門"
              [stacked]="true"
              [groups]="choices()"
              [selected]="selected()"
              [disabled]="applying()"
              (selectedChange)="selected.set($event)"
            />
          </section>
        }
        @if (p.issues.length) {
          <section class="import-section import-issues" aria-labelledby="unit-import-issues">
            <h3 id="unit-import-issues">
              問題 <span class="ui-count-label">{{ p.issues.length }} 筆</span>
            </h3>
            <p class="form-note">
              這些列或帳號不會匯入，其他變動照常套用。請在公文系統修正，或讓對應的人先登入平台後再匯入。
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
      <button
        type="button"
        class="secondary-button"
        [attr.aria-disabled]="applying() || null"
        (click)="close()"
      >
        取消
      </button>
      <button
        nxPending
        type="button"
        class="primary-button unit-import-apply"
        [pending]="applying()"
        [disabled]="!canApply() && !applying()"
        (click)="apply()"
      >
        {{ applying() ? '匯入中…' : '套用 ' + selected().length + ' 個部門' }}
      </button>
    </footer>
  </dialog>`,
  styles: `
    .import-skeleton {
      display: grid;
      gap: var(--p-space-3);
    }
    .import-skeleton .skeleton {
      height: 2.25rem;
    }
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
  private readonly api = inject(OrganizationApi);
  private readonly scope = inject(ViewScope);
  /** 套用成功；頁面據此重新載入並提示摘要。 */
  readonly applied = output<UnitImportResultDto>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('importDialog');
  readonly issueText = issueLabel;
  readonly preview = signal<UnitImportPreviewDto | null>(null);
  readonly departments = computed(() => {
    const preview = this.preview();
    return preview ? importDepartments(preview) : [];
  });
  readonly choices = computed(() => importChoices(this.departments()));
  /** 要套用的部門（`gdDepNo` 字串）；換一份預覽時回到預設：會重新啟用本地停用單位的不勾。 */
  readonly selected = linkedSignal(() =>
    this.departments()
      .filter((item) => !item.reenables)
      .map((item) => item.id),
  );
  readonly loading = signal(false);
  readonly applying = signal(false);
  readonly error = signal('');
  readonly busy = computed(() => this.loading() || this.applying());
  /** 骨架列的寬度（%）：先是摘要，再是逐筆變動。 */
  protected readonly skeleton = [60, 100, 100, 100, 100];
  readonly counts = computed(() => {
    const preview = this.preview();
    return preview ? importCounts(preview) : null;
  });
  readonly changed = computed(() => {
    const preview = this.preview();
    return !!preview && hasChanges(preview);
  });
  readonly tour = unitImportTour();
  readonly canApply = computed(() => this.selected().length > 0 && !this.busy());
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
      const chosen = new Set(this.selected());
      const excluded = this.departments()
        .filter((item) => !chosen.has(item.id))
        .map((item) => Number(item.id));
      const result = await this.api.applyImport(preview.fingerprint, excluded);
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
