import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { FlowDefinitionDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { formatDate } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { Panel } from '@shared/ui/panel';
import { FlowApi } from '../flow-api';
import { FlowDefinitionDialog } from './flow-definition-dialog';
import { describeStep } from './flow-step-draft';

/**
 * 簽核流程（`flow.definitions`）：每種申請一條流程，依序列出各關的審核人與何時需要。
 * 修改只影響之後送出的申請；審核中的申請用送出時的快照走完。
 */
@Component({
  selector: 'nx-flow-definitions-page',
  imports: [FeaturePage, FlowDefinitionDialog, Icon, Notice, Panel],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .flow-kinds {
      display: grid;
      gap: var(--p-space-4);
    }
    .flow-kind-body {
      display: grid;
      gap: var(--p-space-3);
    }
    .flow-kind-body p {
      margin: 0;
    }
    /* 流程線：申請人 → 各關 → 核准，由左到右，放不下就換行；箭頭跟在每一關前面，換行時不會落單在行尾。 */
    .flow-line {
      display: flex;
      flex-wrap: wrap;
      align-items: stretch;
      gap: var(--p-space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .flow-line li {
      display: flex;
      align-items: stretch;
      gap: var(--p-space-2);
      min-width: 0;
    }
    .flow-line-arrow {
      --icon-size: var(--icon-md);
      align-self: center;
      color: var(--text-muted);
    }
    .flow-node {
      display: grid;
      align-content: start;
      gap: 2px;
      min-width: 8rem;
      max-width: 16rem;
      padding: var(--p-space-2) var(--p-space-3);
      border: 1px solid var(--line);
      border-radius: var(--radius-block);
      background: var(--surface-subtle);
    }
    /* 關卡可點：直接打開流程並展開這一關。 */
    .flow-node.is-step {
      color: inherit;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .flow-node.is-step:hover {
      border-color: var(--control-line);
      background: var(--hover);
    }
    .flow-node-index {
      color: var(--text-muted);
      font-size: var(--text-caption);
      font-variant-numeric: tabular-nums;
    }
    .flow-node strong {
      font-weight: 600;
      overflow-wrap: anywhere;
    }
    .flow-node small {
      color: var(--text-muted);
      overflow-wrap: anywhere;
    }
    /* 起點與終點只是標示，不是關卡：虛線、不填底。 */
    .flow-node.is-end {
      display: flex;
      align-items: center;
      gap: var(--p-space-1);
      min-width: 0;
      border-style: dashed;
      background: none;
      color: var(--text-muted);
      font-size: var(--text-caption);
    }
    .flow-node.is-end nx-icon {
      --icon-size: var(--icon-md);
    }
    .flow-skeleton {
      display: grid;
      gap: var(--p-space-3);
    }
    .flow-skeleton .skeleton {
      height: 6rem;
    }
  `,
  template: `<nx-feature-page
      [title]="session.featureName('flow.definitions')"
      description="每種申請要經過哪些關卡、由誰審核。修改只影響之後送出的申請。"
      density="compact"
    >
      @if (notice()) {
        <nx-notice
          tone="warning"
          [message]="notice()"
          [dismissible]="true"
          (dismissed)="notice.set('')"
        />
      }
      @if (read.error() && !read.value()) {
        <nx-notice
          tone="danger"
          [message]="read.error()"
          retryable
          [retrying]="read.refreshing()"
          (retry)="read.reload()"
        />
      } @else if (!read.value()) {
        <div class="flow-skeleton" role="status">
          <span class="skeleton"></span>
          <span class="sr-only">正在載入簽核流程…</span>
        </div>
      }
      <div class="flow-kinds">
        @for (item of kinds(); track item.kind.kind) {
          <nx-panel [heading]="item.kind.name" icon="flow">
            <button
              panel-actions
              type="button"
              class="secondary-button flow-edit"
              [attr.aria-label]="'編輯「' + item.kind.name + '」的流程'"
              (click)="edit(item.kind)"
            >
              <nx-icon name="edit" />編輯流程
            </button>
            <div class="flow-kind-body">
              <p>{{ item.kind.description }}</p>
              @if (item.steps.length) {
                <ol class="flow-line" [attr.aria-label]="item.kind.name + '的關卡'">
                  <li aria-hidden="true">
                    <span class="flow-node is-end"><nx-icon name="user" />申請人</span>
                  </li>
                  @for (step of item.steps; track step.index) {
                    <li>
                      <nx-icon class="flow-line-arrow" name="chevron" aria-hidden="true" />
                      <button
                        type="button"
                        class="flow-node is-step"
                        [attr.aria-label]="
                          '編輯第 ' + step.index + ' 關「' + step.name + '」：' + step.approver
                        "
                        (click)="edit(item.kind, step.index - 1)"
                      >
                        <span class="flow-node-index">第 {{ step.index }} 關</span>
                        <strong>{{ step.name }}</strong>
                        <small>{{ step.approver }}・{{ step.condition }}</small>
                      </button>
                    </li>
                  }
                  <li aria-hidden="true">
                    <nx-icon class="flow-line-arrow" name="chevron" />
                    <span class="flow-node is-end"><nx-icon name="check" />核准</span>
                  </li>
                </ol>
              } @else {
                <nx-notice
                  tone="warning"
                  message="還沒有流程，這種申請暫時無法送出。
按「編輯流程」設定第一關。"
                />
              }
              @if (item.updated) {
                <p class="form-note">最後修改：{{ item.updated }}</p>
              }
            </div>
          </nx-panel>
        }
      </div>
    </nx-feature-page>
    <nx-flow-definition-dialog (saved)="read.reload()" (stale)="stale($event)" />`,
})
export class FlowDefinitionsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(FlowApi);
  private readonly dialog = viewChild.required(FlowDefinitionDialog);
  readonly read = apiResource({
    feature: 'flow.definitions',
    loader: (_, signal) => this.api.definitions(signal),
  });
  /** 儲存時版本已過期的說明；重讀後由使用者再開一次。 */
  readonly notice = signal('');
  private readonly people = computed(
    () => new Map((this.read.value()?.people ?? []).map((person) => [person.id, person])),
  );
  readonly kinds = computed(() => {
    const data = this.read.value();
    if (!data) return [];
    return data.kinds.map((kind) => ({
      kind,
      updated: kind.updatedAt ? formatDate(kind.updatedAt) : '',
      steps: kind.steps.map((step, index) => ({
        index: index + 1,
        name: step.name,
        ...describeStep(step, kind, data.rules, this.people()),
      })),
    }));
  });

  /** `step` 是點了哪一關（從 0 起）；從「編輯流程」進來時不指定，先看整條順序。 */
  edit(kind: FlowDefinitionDto, step?: number) {
    const data = this.read.value();
    if (!data) return;
    this.notice.set('');
    this.dialog().open(kind, data.rules, this.people(), step);
  }
  stale(message: string) {
    this.notice.set(message);
    this.read.reload();
  }
}
