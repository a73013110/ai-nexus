import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type {
  ApproverRuleDto,
  DirectoryUserDto,
  FlowDefinitionDto,
  FlowPersonDto,
} from '@core/api/schema';
import { action } from '@shared/browser/action';
import { nameInitial } from '@shared/browser/format';
import { uniqueId } from '@shared/browser/unique-id';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { DraftEditor } from '@shared/ui/draft-editor';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { Select } from '@shared/ui/select';
import { SortHandle, SortableRows, type RowMove } from '@shared/ui/sortable-rows';
import { ToastService } from '@shared/ui/toast-service';
import { directorySearch } from '@features/work/collaboration/directory-search';
import { PersonPicker } from '@features/work/collaboration/person-picker';
import { FlowApi } from '../flow-api';
import {
  FEATURE_OPTIONS,
  MAX_LEVELS,
  MAX_STEPS,
  MAX_USERS,
  STEP_NAME_MAX,
  type StepDraft,
  argumentOf,
  conditionOptions,
  newStep,
  ruleDetail,
  ruleSummary,
  sameSteps,
  stepProblem,
  summarizeDraft,
  toDraft,
  toRequest,
} from './flow-step-draft';

interface Draft {
  steps: StepDraft[];
}
interface Target {
  kind: FlowDefinitionDto;
  rowVersion: number | null;
}

/**
 * 編輯一種申請的流程：關卡依序排列（可拖曳），每關選審核人的規則與參數，以及何時需要這一關。
 * 規則只列允許這個種類的；同一時間只有一關展開人員搜尋。儲存時帶打開時的版本，期間有人改過就關閉並交給頁面重讀。
 */
@Component({
  selector: 'nx-flow-definition-dialog',
  imports: [
    CompactDialog,
    ConfirmDialog,
    DialogHeader,
    DialogSaveBar,
    Field,
    Icon,
    Notice,
    PersonPicker,
    Select,
    SortHandle,
    SortableRows,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .flow-steps {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    /* 一關一段：摘要列佔滿整寬，展開的欄位排在下面（不用共用段落夠寬時的左右兩欄）。 */
    .flow-step {
      grid-template-columns: minmax(0, 1fr);
    }
    .flow-step-head {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto;
      align-items: center;
      gap: var(--p-space-2);
      min-width: 0;
    }
    .flow-step-toggle {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto;
      align-items: center;
      gap: var(--p-space-3);
      min-width: 0;
      padding: var(--p-space-1) var(--p-space-2);
      border: 0;
      border-radius: var(--radius-item);
      background: none;
      color: inherit;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .flow-step-toggle:hover {
      background: var(--hover);
    }
    .flow-step-number {
      display: grid;
      place-items: center;
      width: 1.5rem;
      height: 1.5rem;
      border-radius: var(--radius-full);
      background: var(--accent-soft);
      color: var(--accent);
      font-size: var(--text-caption);
      font-weight: 600;
      font-variant-numeric: tabular-nums;
    }
    .flow-step-title {
      display: grid;
      min-width: 0;
    }
    .flow-step-title h3 {
      margin: 0;
      font-size: var(--text-label);
      font-weight: 600;
      line-height: var(--line-ui);
      overflow-wrap: anywhere;
    }
    .flow-step-title small {
      color: var(--text-muted);
      overflow-wrap: anywhere;
    }
    .flow-step-title small.field-error {
      color: var(--error);
    }
    .flow-step-chevron {
      --icon-size: var(--icon-md);
      color: var(--text-muted);
    }
    .flow-step-toggle[aria-expanded='true'] .flow-step-chevron {
      rotate: 180deg;
    }
    /* 欄位對齊摘要的文字左緣（序號圓點＋間距＋按鈕內距）。 */
    .flow-step-body {
      padding-inline-start: calc(1.5rem + var(--p-space-3) + var(--p-space-2));
    }
    /* 名稱、審核人、規則參數、何時需要排成一列（對話框夠寬時四欄），窄時依序換行；
       欄寬固定不隨欄位數伸縮，切換規則時其他欄位不跳動。 */
    .flow-step-fields {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(11rem, 1fr));
      gap: var(--p-space-3);
      min-width: 0;
    }
    .flow-rule-detail {
      white-space: pre-line;
    }
    .flow-step-users {
      display: grid;
      justify-items: start;
      gap: var(--p-space-2);
      min-width: 0;
    }
    .flow-step-users > nx-person-picker {
      justify-self: stretch;
      max-width: 24rem;
    }
    /* 新增關卡騎在分隔線上：兩關之間是只有＋的小圓鈕（插在下面那一關之前），清單最後是帶字的膠囊，
       同一條線、同一種外觀。常駐顯示，觸控裝置沒有 hover 也找得到。 */
    .flow-step {
      position: relative;
    }
    .flow-step-insert,
    .flow-step-append {
      --icon-size: var(--icon-sm);
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: var(--p-space-1);
      height: 1.5rem;
      border: 1px solid var(--line);
      border-radius: var(--radius-full);
      background: var(--surface);
      color: var(--text-muted);
      font: inherit;
      font-size: var(--text-caption);
      cursor: pointer;
    }
    .flow-step-insert {
      position: absolute;
      inset-block-start: 0;
      inset-inline-start: 50%;
      translate: -50% -50%;
      width: 1.5rem;
      padding: 0;
    }
    .flow-step-append {
      padding: 0 var(--p-space-3) 0 var(--p-space-2);
    }
    :is(.flow-step-insert, .flow-step-append):hover:not(:disabled) {
      border-color: var(--accent);
      background: var(--accent-soft);
      color: var(--accent);
    }
    :is(.flow-step-insert, .flow-step-append):disabled {
      cursor: default;
      opacity: 0.5;
    }
    .flow-step-end {
      display: flex;
      align-items: center;
    }
    .flow-step-end::before,
    .flow-step-end::after {
      content: '';
      flex: 1;
      border-top: 1px solid var(--line);
    }
  `,
  template: `<dialog
      nxCompactDialog
      #definitionDialog
      class="platform-dialog"
      size="lg"
      [busy]="saving.pending()"
      (cancel)="cancel($event)"
    >
      @if (editor.current(); as e) {
        <form class="dialog-form" novalidate (submit)="save($event)">
          <nx-dialog-header
            [heading]="'編輯「' + e.kind.name + '」的流程'"
            detail="只影響之後送出的申請。審核中的申請照送出時的流程走完。"
            closeLabel="關閉流程設定"
          />
          <div class="dialog-body">
            @if (saving.error()) {
              <nx-notice tone="danger" [message]="saving.error()" />
            }
            <ol
              class="form-sections flow-steps"
              [nxSortableRows]="ids()"
              [sortName]="stepName"
              (sortMove)="moveStep($event)"
            >
              @for (step of e.draft.steps; track step.key; let index = $index) {
                <li
                  class="form-section form-section-wide flow-step"
                  role="group"
                  [attr.data-sort-id]="step.key"
                  [attr.aria-labelledby]="id + '-' + step.key"
                >
                  @if (index > 0) {
                    <button
                      type="button"
                      class="flow-step-insert"
                      [attr.aria-label]="'在第 ' + (index + 1) + ' 關前插入關卡'"
                      [attr.title]="'在第 ' + (index + 1) + ' 關前插入關卡'"
                      [disabled]="e.draft.steps.length >= maxSteps || saving.pending()"
                      (click)="addStep(index)"
                    >
                      <nx-icon name="plus" />
                    </button>
                  }
                  <div class="flow-step-head">
                    <nx-sort-handle
                      [id]="step.key"
                      [name]="stepName(step.key)"
                      [position]="index + 1"
                      [count]="e.draft.steps.length"
                    />
                    <button
                      type="button"
                      class="flow-step-toggle"
                      [attr.aria-expanded]="expanded() === step.key"
                      [attr.aria-controls]="id + '-' + step.key + '-body'"
                      (click)="toggle(step.key)"
                    >
                      <span class="flow-step-number" aria-hidden="true">{{ index + 1 }}</span>
                      <span class="flow-step-title">
                        <h3 [id]="id + '-' + step.key">
                          {{ step.name.trim() || '第 ' + (index + 1) + ' 關' }}
                        </h3>
                        @if (attempted() && problems()[step.key]) {
                          <small class="field-error">{{ problems()[step.key] }}</small>
                        } @else {
                          <small
                            >{{ summaries()[step.key]?.approver }}・{{
                              summaries()[step.key]?.condition
                            }}</small
                          >
                        }
                      </span>
                      <nx-icon class="flow-step-chevron" name="chevron-down" />
                    </button>
                    <button
                      type="button"
                      class="icon-button flow-step-remove"
                      [attr.aria-label]="'移除' + stepName(step.key)"
                      [disabled]="e.draft.steps.length === 1 || saving.pending()"
                      (click)="removeStep(step.key)"
                    >
                      <nx-icon name="trash" />
                    </button>
                  </div>
                  @if (expanded() === step.key) {
                    <div
                      class="form-section-body flow-step-body"
                      [id]="id + '-' + step.key + '-body'"
                    >
                      <div class="flow-step-fields">
                        <label
                          >關卡名稱<input
                            #name
                            nxField
                            autocomplete="off"
                            [attr.maxlength]="nameMax"
                            [value]="step.name"
                            [readOnly]="saving.pending()"
                            (input)="patchStep(step.key, { name: name.value })"
                        /></label>
                        <label
                          >審核人<nx-select
                            label="審核人"
                            [options]="ruleOptions()"
                            [value]="step.rule"
                            [disabled]="saving.pending()"
                            (valueChange)="chooseRule(step.key, $event)"
                        /></label>
                        @switch (argument(step.rule)) {
                          @case ('levels') {
                            <label
                              >往上幾層<input
                                #levels
                                nxField
                                type="number"
                                min="1"
                                [attr.max]="maxLevels"
                                [value]="step.levels"
                                [readOnly]="saving.pending()"
                                (input)="patchStep(step.key, { levels: levels.valueAsNumber })"
                            /></label>
                          }
                          @case ('feature') {
                            <label
                              >持有這個功能的人<nx-select
                                label="持有這個功能的人"
                                [options]="featureOptions"
                                [value]="step.feature"
                                [searchable]="true"
                                [disabled]="saving.pending()"
                                (valueChange)="patchStep(step.key, { feature: $event })"
                            /></label>
                          }
                        }
                        @if (conditions().length > 1) {
                          <label
                            >何時需要這一關<nx-select
                              label="何時需要這一關"
                              [options]="conditions()"
                              [value]="step.condition"
                              [disabled]="saving.pending()"
                              (valueChange)="patchStep(step.key, { condition: $event })"
                          /></label>
                        }
                      </div>
                      @if (detailOf(step.rule); as detail) {
                        <p class="form-note flow-rule-detail">{{ detail }}</p>
                      }
                      @if (argument(step.rule) === 'users') {
                        <div class="flow-step-users">
                          @if (step.users.length) {
                            <div class="form-chips">
                              @for (user of step.users; track user.id) {
                                <button
                                  type="button"
                                  class="form-chip"
                                  [attr.aria-label]="'移除審核人 ' + user.displayName"
                                  [disabled]="saving.pending()"
                                  (click)="removeUser(step.key, user.id)"
                                >
                                  <span class="avatar" aria-hidden="true">{{
                                    initial(user.displayName)
                                  }}</span
                                  >{{ user.displayName }}<nx-icon name="close" />
                                </button>
                              }
                            </div>
                          }
                          @if (picking() === step.key) {
                            <nx-person-picker
                              label="加入審核人"
                              [search]="people"
                              [disabled]="saving.pending() || step.users.length >= maxUsers"
                              (picked)="addUser(step.key, $event)"
                            />
                          } @else {
                            <button
                              type="button"
                              class="quiet-button"
                              [disabled]="saving.pending() || step.users.length >= maxUsers"
                              (click)="pick(step.key)"
                            >
                              <nx-icon name="plus" />加入審核人
                            </button>
                          }
                        </div>
                      }
                    </div>
                  }
                </li>
              }
            </ol>
            <div class="flow-step-end">
              <button
                type="button"
                class="flow-step-append"
                [disabled]="e.draft.steps.length >= maxSteps || saving.pending()"
                (click)="addStep()"
              >
                <nx-icon name="plus" />新增關卡
              </button>
            </div>
          </div>
          <nx-dialog-save-bar
            label="儲存流程"
            [dirty]="editor.dirty()"
            [saving]="saving.pending()"
            (revert)="revert()"
          />
        </form>
      }
    </dialog>
    <nx-confirm-dialog />`,
})
export class FlowDefinitionDialog {
  readonly id = uniqueId('nx-flow-definition');
  private readonly api = inject(FlowApi);
  private readonly toast = inject(ToastService);
  private readonly injector = inject(Injector);
  /** 儲存成功：頁面重讀。 */
  readonly saved = output<void>();
  /** 版本已過期：對話框已關閉，由頁面顯示訊息並重讀。 */
  readonly stale = output<string>();
  readonly nameMax = STEP_NAME_MAX;
  readonly maxLevels = MAX_LEVELS;
  readonly maxUsers = MAX_USERS;
  readonly maxSteps = MAX_STEPS;
  readonly featureOptions = FEATURE_OPTIONS;
  protected readonly initial = nameInitial;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('definitionDialog');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  private readonly rules = signal<readonly ApproverRuleDto[]>([]);
  readonly editor = new DraftEditor<Draft, Target>((first, second) =>
    sameSteps(first.steps, second.steps, this.rules()),
  );
  readonly saving = action();
  /** 按過儲存才顯示各關的問題，剛新增的空白關卡不先標紅。 */
  readonly attempted = signal(false);
  /** 展開編輯的那一關；同一時間只展開一關，其他關收成一行摘要。 */
  readonly expanded = signal<string | null>(null);
  /** 正在搜尋人員的那一關。 */
  readonly picking = signal<string | null>(null);
  readonly people = directorySearch(
    () =>
      this.editor
        .current()
        ?.draft.steps.find((step) => step.key === this.picking())
        ?.users.map((user) => user.id) ?? [],
    { includeSelf: true },
  );

  private readonly steps = computed(() => this.editor.current()?.draft.steps ?? []);
  readonly ids = computed(() => this.steps().map((step) => step.key));
  /** 只列允許這個種類的規則；舊設定用了已不允許的規則時仍列出，才看得到目前的值。 */
  readonly ruleOptions = computed(() => {
    const kind = this.editor.current()?.kind.kind;
    const used = new Set(this.steps().map((step) => step.rule));
    return this.rules()
      .filter((rule) => !rule.kinds.length || rule.kinds.includes(kind ?? '') || used.has(rule.key))
      .map((rule) => ({
        value: rule.key,
        label: rule.name,
        description: ruleSummary(rule.description),
      }));
  });
  /** 收合時每關的一句摘要（審核人・何時需要）。 */
  readonly summaries = computed<Record<string, { approver: string; condition: string }>>(() => {
    const kind = this.editor.current()?.kind;
    if (!kind) return {};
    return Object.fromEntries(
      this.steps().map((step) => [step.key, summarizeDraft(step, kind, this.rules())]),
    );
  });
  readonly conditions = computed(() => {
    const kind = this.editor.current()?.kind;
    return kind ? conditionOptions(kind) : [];
  });
  readonly problems = computed<Record<string, string>>(() =>
    Object.fromEntries(this.steps().map((step) => [step.key, stepProblem(step, this.rules())])),
  );
  readonly stepName = (key: string) => {
    const index = this.steps().findIndex((step) => step.key === key);
    const name = this.steps()[index]?.name.trim();
    return name ? `第 ${index + 1} 關「${name}」` : `第 ${index + 1} 關`;
  };

  open(
    kind: FlowDefinitionDto,
    rules: readonly ApproverRuleDto[],
    people: ReadonlyMap<string, FlowPersonDto>,
    /** 從頁面點了某一關（從 0 起）：展開它並把焦點放在關卡名稱。 */
    focus?: number,
  ) {
    this.rules.set(rules);
    this.saving.reset();
    this.attempted.set(false);
    this.picking.set(null);
    this.people.clear();
    const steps = kind.steps.map((step) => toDraft(step, people));
    const draft = steps.length ? steps : [newStep(this.defaultRule(kind))];
    this.editor.open({ kind, rowVersion: kind.rowVersion }, { steps: draft });
    // 指定了哪一關就展開它；否則只有一關（含還沒有流程）時直接展開，多關時先看整條順序。
    const target = focus === undefined ? undefined : draft[focus];
    this.expanded.set(target?.key ?? (draft.length === 1 ? (draft[0]?.key ?? null) : null));
    this.dialog().nativeElement.showModal();
    // showModal 會把焦點交給第一個可聚焦元素（關閉鈕）；指定關卡時移到它的名稱欄。
    if (target) this.focusName(target.key);
  }
  /** 等展開的欄位畫出來，再把焦點放到這一關的名稱欄（同時捲進視野）。 */
  private focusName(key: string) {
    afterNextRender(
      () =>
        this.dialog()
          .nativeElement.querySelector<HTMLInputElement>(`[id="${this.id}-${key}-body"] input`)
          ?.focus(),
      { injector: this.injector },
    );
  }
  toggle(key: string) {
    this.expanded.update((current) => (current === key ? null : key));
  }
  detailOf(rule: string) {
    return ruleDetail(this.rules().find((item) => item.key === rule)?.description ?? '');
  }
  argument(rule: string) {
    return argumentOf(this.rules(), rule);
  }
  patchStep(key: string, changes: Partial<StepDraft>) {
    this.editor.update(
      'steps',
      this.steps().map((step) => (step.key === key ? { ...step, ...changes } : step)),
    );
  }
  chooseRule(key: string, rule: string) {
    this.patchStep(key, { rule });
    if (this.argument(rule) === 'users') this.pick(key);
  }
  pick(key: string) {
    this.expanded.set(key);
    this.people.clear();
    this.picking.set(key);
  }
  addUser(key: string, user: DirectoryUserDto) {
    const step = this.steps().find((item) => item.key === key);
    if (!step || step.users.length >= MAX_USERS) return;
    this.patchStep(key, {
      users: [...step.users, { id: user.id, displayName: user.displayName }],
    });
    this.people.clear();
  }
  removeUser(key: string, userId: string) {
    const step = this.steps().find((item) => item.key === key);
    if (step) this.patchStep(key, { users: step.users.filter((user) => user.id !== userId) });
  }
  /** `at` 是插入的位置（從 0 起，插在原本那一關之前）；不給就接在最後。新的一關展開並聚焦名稱。 */
  addStep(at = this.steps().length) {
    const kind = this.editor.current()?.kind;
    if (!kind || this.steps().length >= MAX_STEPS) return;
    const step = newStep(this.defaultRule(kind));
    const steps = [...this.steps()];
    steps.splice(at, 0, step);
    this.editor.update('steps', steps);
    this.expanded.set(step.key);
    if (this.argument(step.rule) === 'users') this.pick(step.key);
    this.focusName(step.key);
  }
  removeStep(key: string) {
    if (this.steps().length > 1)
      this.editor.update(
        'steps',
        this.steps().filter((step) => step.key !== key),
      );
  }
  moveStep({ id, index }: RowMove) {
    const steps = [...this.steps()];
    const from = steps.findIndex((step) => step.key === id);
    if (from < 0) return;
    const [moved] = steps.splice(from, 1);
    if (moved) steps.splice(index, 0, moved);
    this.editor.update('steps', steps);
  }
  revert() {
    this.editor.revert();
    this.expanded.set(null);
    this.attempted.set(false);
    this.picking.set(null);
  }
  cancel(event: Event) {
    event.preventDefault();
    void this.requestClose();
  }
  async requestClose() {
    if (this.saving.pending()) return;
    if (
      await this.editor.confirmDiscard(
        this.confirmation(),
        '流程尚未儲存，關閉後會捨棄這次的修改。',
      )
    )
      this.dialog().nativeElement.close();
  }

  async save(event: Event) {
    event.preventDefault();
    const editing = this.editor.current();
    if (!editing || this.saving.pending() || !this.editor.dirty()) return;
    this.attempted.set(true);
    // 有問題時展開第一個有問題的關卡讓欄位就在眼前；其他關的問題寫在各自的摘要列。
    const problem = this.steps().find((step) => this.problems()[step.key]);
    if (problem) {
      this.expanded.set(problem.key);
      return;
    }
    const body = {
      steps: editing.draft.steps.map((step) => toRequest(step, this.rules())),
      rowVersion: editing.rowVersion,
    };
    const outcome = await this.saving.run(() => this.api.saveDefinition(editing.kind.kind, body));
    if (!outcome.ok) {
      // 打開時的版本已過期，再送也會被拒：關掉，從重讀後的流程重新開始。
      if (outcome.stale) {
        this.editor.markSaved();
        this.dialog().nativeElement.close();
        this.stale.emit(outcome.error);
      }
      return;
    }
    this.editor.markSaved();
    this.dialog().nativeElement.close();
    this.toast.success('已儲存流程', editing.kind.name);
    this.saved.emit();
  }

  /** 新關卡預設用第一個允許的規則；名稱留空讓人填。 */
  private defaultRule(kind: FlowDefinitionDto) {
    return (
      this.rules().find((rule) => !rule.kinds.length || rule.kinds.includes(kind.kind))?.key ?? ''
    );
  }
}
