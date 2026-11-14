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
import { InfoPopover } from '@shared/ui/info-popover';
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
  sameSteps,
  stepProblem,
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
    InfoPopover,
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
    /* 段落標題列：把手、關卡序號、移除鈕排在同一列（共用樣式預設把標題以外的元素排到下一列）。 */
    .flow-step-head {
      grid-template-columns: auto minmax(0, 1fr) auto;
    }
    .flow-step-head > * {
      grid-column: auto;
    }
    .flow-levels {
      max-width: 10rem;
    }
    .flow-step-add {
      display: flex;
      align-items: center;
      gap: var(--p-space-2);
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
            detail="只影響之後送出的申請；審核中的申請照送出時的流程走完。"
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
                  class="form-section"
                  role="group"
                  [attr.data-sort-id]="step.key"
                  [attr.aria-labelledby]="id + '-' + step.key"
                >
                  <header class="form-section-head flow-step-head">
                    <nx-sort-handle
                      [id]="step.key"
                      [name]="stepName(step.key)"
                      [position]="index + 1"
                      [count]="e.draft.steps.length"
                    />
                    <h3 [id]="id + '-' + step.key">第 {{ index + 1 }} 關</h3>
                    <button
                      type="button"
                      class="icon-button flow-step-remove"
                      [attr.aria-label]="'移除' + stepName(step.key)"
                      [disabled]="e.draft.steps.length === 1 || saving.pending()"
                      (click)="removeStep(step.key)"
                    >
                      <nx-icon name="trash" />
                    </button>
                  </header>
                  <div class="form-section-body">
                    <div class="form-row">
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
                      <nx-select
                        label="審核人"
                        [options]="ruleOptions()"
                        [value]="step.rule"
                        [disabled]="saving.pending()"
                        (valueChange)="chooseRule(step.key, $event)"
                      />
                    </div>
                    @switch (argument(step.rule)) {
                      @case ('levels') {
                        <label class="flow-levels"
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
                        <p class="form-note">1＝直屬主管；沒有主管的單位會跳過。</p>
                      }
                      @case ('users') {
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
                      }
                      @case ('feature') {
                        <nx-select
                          label="持有這個功能的人"
                          [options]="featureOptions"
                          [value]="step.feature"
                          [searchable]="true"
                          [disabled]="saving.pending()"
                          (valueChange)="patchStep(step.key, { feature: $event })"
                        />
                      }
                    }
                    @if (conditions().length > 1) {
                      <nx-select
                        label="何時需要這一關"
                        [options]="conditions()"
                        [value]="step.condition"
                        [disabled]="saving.pending()"
                        (valueChange)="patchStep(step.key, { condition: $event })"
                      />
                    }
                    @if (attempted() && problems()[step.key]) {
                      <p class="form-note field-error">{{ problems()[step.key] }}</p>
                    }
                  </div>
                </li>
              }
            </ol>
            <div class="flow-step-add">
              <button
                type="button"
                class="secondary-button"
                [disabled]="e.draft.steps.length >= maxSteps || saving.pending()"
                (click)="addStep()"
              >
                <nx-icon name="plus" />新增關卡
              </button>
              <nx-info-popover
                label="審核人規則說明"
                heading="審核人規則"
                [description]="rulesHelp()"
              />
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
      .map((rule) => ({ value: rule.key, label: rule.name, description: rule.description }));
  });
  readonly rulesHelp = computed(() =>
    this.ruleOptions()
      .map((rule) => `${rule.label}：${rule.description}`)
      .join('\n'),
  );
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
  ) {
    this.rules.set(rules);
    this.saving.reset();
    this.attempted.set(false);
    this.picking.set(null);
    this.people.clear();
    const steps = kind.steps.map((step) => toDraft(step, people));
    this.editor.open(
      { kind, rowVersion: kind.rowVersion },
      { steps: steps.length ? steps : [newStep(this.defaultRule(kind))] },
    );
    this.dialog().nativeElement.showModal();
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
  addStep() {
    const kind = this.editor.current()?.kind;
    if (!kind || this.steps().length >= MAX_STEPS) return;
    const step = newStep(this.defaultRule(kind));
    this.editor.update('steps', [...this.steps(), step]);
    if (this.argument(step.rule) === 'users') this.pick(step.key);
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
    if (Object.values(this.problems()).some(Boolean)) return;
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
