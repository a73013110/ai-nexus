import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { GdFormDetailDto, GdPerson } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { ApprovalTimeline } from '@shared/ui/approval-timeline';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { CopyValue } from '@shared/ui/copy-value';
import { DialogHeader } from '@shared/ui/dialog-header';
import { FileList } from '@shared/ui/file-list';
import { Notice } from '@shared/ui/notice';
import { StatusBadge } from '@shared/ui/status-badge';
import { AttendanceApi } from '../attendance-api';
import { GD_FORM_FLOW, attendanceProblem } from '../attendance-presentation';
import { GdFormSpan } from './gd-form-span';
import {
  approvalSteps,
  attachmentItems,
  formHeading,
  personDetail,
  progressText,
  stamp,
} from './gd-form-presentation';

/** 側欄的一個人：頭像取姓名第一個字，名字下一行是單位與職稱。 */
interface SidePerson {
  role: string;
  name: string;
  initial: string;
  detail: string;
}

/**
 * 公文系統的一張假單或出差單（唯讀），從出勤紀錄點單號開啟。標題列寫假別（出差寫事由）、狀態與單號；
 * 主欄是期間摘要、事由、附件與簽核流程，側欄是申請人、代理人與單據各自的欄位，手機上側欄排到主欄之後。
 * 期間摘要（含跨期間假單的各段）交給 {@link GdFormSpan}。
 * 流程或附件讀不到時只在那一塊說明，表單其他內容照常顯示。
 */
@Component({
  selector: 'nx-gd-form-dialog',
  imports: [
    ApprovalTimeline,
    CompactDialog,
    CopyValue,
    DialogHeader,
    FileList,
    GdFormSpan,
    Notice,
    StatusBadge,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ViewScope],
  styleUrl: './gd-form-dialog.scss',
  template: `<dialog
    nxCompactDialog
    #formDialog
    class="platform-dialog gd-form-dialog"
    size="lg"
    (close)="closed()"
  >
    <nx-dialog-header
      [heading]="heading()"
      [icon]="travel() ? 'location' : 'calendar'"
      closeLabel="關閉表單"
    >
      @if (form(); as form) {
        <nx-status-badge dialog-title [tone]="flow().tone">{{ flow().label }}</nx-status-badge>
      }
      <p dialog-subtitle class="dialog-subtitle">
        <span>{{ travel() ? '出差單' : '請假單' }}</span>
        <nx-copy-value label="單號" [value]="desNo()" />
        @if (submitted()) {
          <span>{{ submitted() }} 申請</span>
        }
      </p>
    </nx-dialog-header>
    <div class="dialog-body dialog-body-flush" [attr.aria-busy]="loading() || null">
      @if (error()) {
        <div class="form-state"><nx-notice [message]="error()" /></div>
      } @else if (loading()) {
        <div class="form-state form-skeleton" role="status">
          <span class="sr-only">正在讀取表單…</span>
          <span class="skeleton"></span><span class="skeleton"></span><span class="skeleton"></span>
        </div>
      } @else if (form(); as form) {
        <div class="form-layout">
          <div class="form-main">
            <nx-gd-form-span [form]="form" [focus]="focus()" />
            @if (travel(); as travel) {
              @if (travel.place || travel.transport) {
                <dl class="form-facts">
                  @if (travel.place) {
                    <div>
                      <dt class="form-heading">地點</dt>
                      <dd>{{ travel.place }}</dd>
                    </div>
                  }
                  @if (travel.transport) {
                    <div>
                      <dt class="form-heading">交通工具</dt>
                      <dd>{{ travel.transport }}</dd>
                    </div>
                  }
                </dl>
              }
            }
            @if (form.reason) {
              <section class="form-block" aria-labelledby="gd-form-reason">
                <h3 id="gd-form-reason" class="form-heading">
                  {{ travel() ? '事由' : '請假事由' }}
                </h3>
                <p class="form-text">{{ form.reason }}</p>
              </section>
            }
            <section class="form-block" aria-labelledby="gd-form-files">
              <h3 id="gd-form-files" class="form-heading">附件</h3>
              @if (kinds().length) {
                <ul class="form-chips" aria-label="證明文件">
                  @for (kind of kinds(); track kind) {
                    <li>{{ kind }}</li>
                  }
                </ul>
              }
              @if (form.attachmentNote) {
                <p class="form-text">{{ form.attachmentNote }}</p>
              }
              @if (form.attachments === null) {
                <p class="form-missing">附件暫時讀不到，請稍後再開一次。</p>
              } @else if (files().length) {
                <nx-file-list [files]="files()" [stacked]="true" />
              } @else if (!form.attachmentNote && !kinds().length) {
                <p class="form-missing">沒有附件。</p>
              }
            </section>
            @if (form.steps === null) {
              <section class="form-block" aria-labelledby="gd-form-flow">
                <h3 id="gd-form-flow" class="form-heading">簽核流程</h3>
                <p class="form-missing">簽核流程暫時讀不到，請稍後再開一次。</p>
              </section>
            } @else {
              <nx-approval-timeline [steps]="steps()" [summary]="progress()" />
            }
          </div>
          <aside class="form-side" aria-label="表單資訊">
            <dl class="form-props">
              @for (person of people(); track person.role) {
                <div>
                  <dt>{{ person.role }}</dt>
                  <dd class="form-person">
                    @if (person.initial) {
                      <span class="avatar" aria-hidden="true">{{ person.initial }}</span>
                    }
                    <span
                      >{{ person.name }}
                      @if (person.detail) {
                        <small>{{ person.detail }}</small>
                      }
                    </span>
                  </dd>
                </div>
              }
              @if (travel()?.phone; as phone) {
                <div>
                  <dt>聯絡電話</dt>
                  <dd>{{ phone }}</dd>
                </div>
              }
            </dl>
            <hr />
            <dl class="form-props">
              @if (form.leave; as leave) {
                <div>
                  <dt>假單類型</dt>
                  <dd>{{ form.segments.length ? '跨期間假單' : '一般假單' }}</dd>
                </div>
                @if (leave.relation) {
                  <div>
                    <dt>親屬關係</dt>
                    <dd>{{ leave.relation }}</dd>
                  </div>
                }
              }
              @if (travel(); as travel) {
                <div>
                  <dt>類別</dt>
                  <dd>{{ travel.studyKind || '出差' }}</dd>
                </div>
              }
            </dl>
          </aside>
        </div>
      }
    </div>
  </dialog>`,
})
export class GdFormDialog {
  private readonly api = inject(AttendanceApi);
  private readonly scope = inject(ViewScope);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('formDialog');
  private controller?: AbortController;
  readonly desNo = signal('');
  readonly focus = signal<string | null>(null);
  readonly form = signal<GdFormDetailDto | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly travel = computed(() => this.form()?.travel ?? null);
  readonly heading = computed(() => {
    const form = this.form();
    return form ? formHeading(form) : this.loading() ? '正在讀取表單…' : '表單';
  });
  readonly flow = computed(() => {
    const status = this.form()?.flowStatus;
    return (status && GD_FORM_FLOW[status]) || { label: status ?? '', tone: 'neutral' as const };
  });
  readonly submitted = computed(() => {
    const at = this.form()?.submittedAt;
    return at ? stamp(at) : '';
  });
  readonly people = computed(() => {
    const form = this.form();
    if (!form) return [];
    return [
      side(form.kind === 'Travel' ? '出差人' : '請假人', form.applicant),
      side('代理人', form.agent),
    ];
  });
  readonly kinds = computed(() => this.form()?.leave?.attachmentKinds ?? []);
  readonly files = computed(() => attachmentItems(this.form()?.attachments ?? []));
  readonly steps = computed(() => approvalSteps(this.form()?.steps ?? []));
  readonly progress = computed(() => progressText(this.form()?.steps ?? []));

  /** 開啟一張單；`date` 是出勤紀錄上點開的那一天，跨期間的月曆會標出來。 */
  open(desNo: string, date: string | null = null) {
    this.desNo.set(desNo);
    this.focus.set(date);
    this.form.set(null);
    this.error.set('');
    this.dialog().nativeElement.showModal();
    void this.load(desNo);
  }

  private async load(desNo: string) {
    this.controller?.abort();
    const controller = (this.controller = new AbortController());
    const alive = this.scope.guard();
    const current = () => alive() && this.controller === controller;
    this.loading.set(true);
    try {
      const form = await this.api.form(desNo, controller.signal);
      if (current()) this.form.set(form);
    } catch (error) {
      if (current() && !controller.signal.aborted) this.error.set(attendanceProblem(error));
    } finally {
      if (current()) this.loading.set(false);
    }
  }

  /** 關閉時停止還在進行的讀取；內容留到下次開啟才換掉，退場動畫期間不會閃成空白。 */
  closed() {
    this.controller?.abort();
    this.controller = undefined;
    this.loading.set(false);
  }
}

function side(role: string, person: GdPerson | null): SidePerson {
  const name = person?.name?.trim() || '—';
  return { role, name, initial: name === '—' ? '' : name[0], detail: personDetail(person) };
}
