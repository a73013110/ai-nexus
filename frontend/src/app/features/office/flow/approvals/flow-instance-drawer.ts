import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  viewChild,
} from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import { action } from '@shared/browser/action';
import { formatDate } from '@shared/browser/format';
import { ApprovalTimeline } from '@shared/ui/approval-timeline';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { DrawerFoot } from '@shared/ui/drawer-foot';
import { DrawerPager } from '@shared/ui/drawer-pager';
import { DrawerSummary } from '@shared/ui/drawer-summary';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { StatusBadge } from '@shared/ui/status-badge';
import { ToastService } from '@shared/ui/toast-service';
import { FlowApi } from '../flow-api';
import { flowProgress, flowStatus, flowTimeline } from '../flow-presentation';

/** 與後端 `FlowInstanceStep.CommentMaxLength` 相同。 */
const COMMENT_MAX = 500;

/**
 * 一份申請的詳情：內容、各關的進度與決定，以及目前能做的事（審核人核准或退回、申請人撤回）。
 * 抽屜自己讀詳情：審核人與能否決定是伺服器即時算的，清單上的摘要不夠。決定送出時帶畫面上的關卡，
 * 關卡已被別人推進時伺服器拒絕，這裡重讀讓畫面回到最新狀態。
 */
@Component({
  selector: 'nx-flow-instance-drawer',
  imports: [
    ApprovalTimeline,
    ConfirmDialog,
    DetailDrawer,
    DrawerFoot,
    DrawerPager,
    DrawerSummary,
    Field,
    Notice,
    PendingButton,
    StatusBadge,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: contents;
    }
    .flow-instance-skeleton {
      display: grid;
      gap: var(--p-space-4);
    }
    .flow-instance-skeleton .skeleton {
      height: 0.875rem;
    }
  `,
  templateUrl: './flow-instance-drawer.html',
})
export class FlowInstanceDrawer {
  private readonly api = inject(FlowApi);
  private readonly toast = inject(ToastService);
  readonly instanceId = input<string | null>(null);
  readonly index = input(-1);
  readonly count = input(0);
  readonly move = output<-1 | 1>();
  readonly closed = output<void>();
  /** 決定或撤回成功：頁面重讀清單。 */
  readonly changed = output<void>();
  private readonly drawer = viewChild.required(DetailDrawer);
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly drawerId = computed(() => this.drawer().id);
  readonly commentMax = COMMENT_MAX;
  protected readonly skeleton = [40, 88, 72, 92, 64];

  readonly read = apiResource({
    feature: 'flow.approvals',
    params: () => this.instanceId() ?? undefined,
    loader: (id, signal) => this.api.instance(id, signal),
  });
  /** 讀回的是目前選的那一筆才顯示；換筆時不留著上一筆。 */
  readonly instance = computed(() => {
    const value = this.read.value();
    return value && value.id === this.instanceId() ? value : null;
  });
  readonly view = computed(() => {
    const instance = this.instance();
    if (!instance) return null;
    return {
      instance,
      status: flowStatus(instance.status),
      subtitle: `${instance.kindName}・${instance.applicant.displayName}`,
      submitted: formatDate(instance.submittedAt),
      completed: instance.completedAt ? formatDate(instance.completedAt) : '',
      timeline: flowTimeline(instance),
      progress: flowProgress(instance),
    };
  });
  /** 換一筆就清掉寫到一半的意見，不會帶到別人的申請。 */
  readonly comment = linkedSignal({ source: this.instanceId, computation: () => '' });
  readonly acting = action();
  readonly foot = computed(() => {
    const instance = this.instance();
    if (instance?.canDecide)
      return {
        decide: true,
        note: instance.onBehalf ? '你不是這一關的審核人，決定會記為代審。' : '',
      };
    if (instance?.canCancel) return { decide: false, note: '還沒審完前可以撤回。' };
    return null;
  });

  open() {
    this.drawer().open();
  }
  close() {
    this.drawer().close();
  }

  async decide(approve: boolean) {
    const instance = this.instance();
    if (!instance?.canDecide || instance.currentStep === null) return;
    const comment = this.comment().trim();
    if (
      !approve &&
      !comment &&
      !(await this.confirmation().ask({
        title: '不填意見就退回？',
        message: '申請人會收到退回通知。\n寫下原因，對方才知道要補什麼。',
        confirm: '直接退回',
        danger: true,
      }))
    )
      return;
    const { id, currentStep } = instance;
    const outcome = await this.acting.run(
      () => this.api.decide(id, { stepNumber: currentStep, approve, comment: comment || null }),
      { key: approve ? 'approve' : 'reject', current: () => this.instanceId() === id },
    );
    // 失敗多半是別人先處理了這一關：重讀，畫面回到最新的關卡與按鈕。
    this.read.reload();
    if (!outcome.ok) return;
    this.toast.success(approve ? '已核准' : '已退回', instance.title);
    this.comment.set('');
    this.changed.emit();
  }

  async withdraw() {
    const instance = this.instance();
    if (!instance?.canCancel) return;
    if (
      !(await this.confirmation().ask({
        title: '撤回這份申請？',
        message: '審核人不必再處理。\n之後需要時可以重新申請。',
        confirm: '撤回',
      }))
    )
      return;
    const id = instance.id;
    const outcome = await this.acting.run(() => this.api.cancel(id), {
      key: 'cancel',
      current: () => this.instanceId() === id,
    });
    this.read.reload();
    if (!outcome.ok) return;
    this.toast.success('已撤回申請', instance.title);
    this.changed.emit();
  }
}
