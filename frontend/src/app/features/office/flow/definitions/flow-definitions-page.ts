import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import { routeOf } from '@core/api/catalog';
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
  imports: [FeaturePage, FlowDefinitionDialog, Icon, Notice, Panel, RouterLink],
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
    .flow-kind-steps {
      display: grid;
      gap: var(--p-space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .flow-kind-steps li {
      display: grid;
      grid-template-columns: 1.5rem minmax(0, 1fr);
      gap: var(--p-space-2);
      align-items: baseline;
    }
    .flow-kind-index {
      color: var(--text-muted);
      font-variant-numeric: tabular-nums;
      text-align: end;
    }
    .flow-kind-steps strong {
      display: block;
      font-weight: 600;
    }
    .flow-kind-steps small {
      color: var(--text-muted);
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
      description="每種申請要經過哪些關卡、由誰審核；修改只影響之後送出的申請。"
      density="compact"
    >
      @if (session.has('flow.approvals')) {
        <div page-actions class="page-actions">
          <a class="quiet-button" [routerLink]="routeOf('flow.approvals')"
            ><nx-icon name="check" />{{ session.featureName('flow.approvals') }}</a
          >
        </div>
      }
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
          <nx-panel [heading]="item.kind.name" icon="order">
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
                <ol class="flow-kind-steps" [attr.aria-label]="item.kind.name + '的關卡'">
                  @for (step of item.steps; track step.index) {
                    <li>
                      <span class="flow-kind-index">{{ step.index }}</span>
                      <div>
                        <strong>{{ step.name }}</strong>
                        <small>{{ step.approver }}・{{ step.condition }}</small>
                      </div>
                    </li>
                  }
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
  readonly routeOf = routeOf;
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

  edit(kind: FlowDefinitionDto) {
    const data = this.read.value();
    if (!data) return;
    this.notice.set('');
    this.dialog().open(kind, data.rules, this.people());
  }
  stale(message: string) {
    this.notice.set(message);
    this.read.reload();
  }
}
