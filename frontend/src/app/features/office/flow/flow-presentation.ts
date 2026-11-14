import type { FlowInstanceDto, FlowStepDto } from '@core/api/schema';
import { formatDate, relativeTime } from '@shared/browser/format';
import type { ApprovalStep } from '@shared/ui/approval-timeline';
import type { BadgeTone } from '@shared/ui/count-badge';

interface Presented {
  label: string;
  tone: BadgeTone;
}

/** 申請的狀態（後端 `FlowStatuses`）：審核中是要追的，用藍；退回紅、逾期黃，撤回只是使用者自己收回，用灰。 */
export const FLOW_STATUSES: Record<string, Presented> = {
  pending: { label: '審核中', tone: 'info' },
  approved: { label: '已核准', tone: 'success' },
  rejected: { label: '已退回', tone: 'danger' },
  cancelled: { label: '已撤回', tone: 'neutral' },
  expired: { label: '已逾期', tone: 'warning' },
};
export const flowStatus = (status: string): Presented =>
  FLOW_STATUSES[status] ?? { label: status, tone: 'neutral' };
export const FLOW_STATUS_OPTIONS = [
  { value: '', label: '全部狀態' },
  ...Object.entries(FLOW_STATUSES).map(([value, item]) => ({ value, label: item.label })),
];

/** 自動通過或略過的原因（後端 `FlowStepNotes`）。 */
const STEP_NOTES: Record<string, string> = {
  'not-applicable': '條件不符，略過',
  'already-approved': '已在前一關核准，自動通過',
};

/**
 * 一關在時間軸上的樣子。退回也算「已完成」（流程走到這裡結束），差別寫在關卡名稱；
 * 申請結束時還沒輪到的關卡（`stopped`）維持未到，附註「未進行」。
 * 目前關卡寫出現在的審核人與已等多久；沒有審核人時改由簽核管理員處理（`fallback`）。
 */
function timelineStep(step: FlowStepDto, instance: FlowInstanceDto): ApprovalStep {
  const key = `step-${step.stepNumber}`;
  const decided = step.decidedAt
    ? { time: formatDate(step.decidedAt), dateTime: step.decidedAt }
    : {};
  const note = [
    step.note ? (STEP_NOTES[step.note] ?? step.note) : '',
    step.onBehalf ? '代審' : '',
    step.fallback ? '無審核人，改由簽核管理員' : '',
  ]
    .filter(Boolean)
    .join('・');
  switch (step.status) {
    case 'approved':
      return {
        key,
        state: 'done',
        title: step.name,
        person: step.decider?.displayName,
        note,
        memo: step.comment,
        ...decided,
      };
    case 'rejected':
      return {
        key,
        state: 'done',
        title: `${step.name}・退回`,
        person: step.decider?.displayName,
        note,
        memo: step.comment,
        ...decided,
      };
    case 'skipped':
      return { key, state: 'done', title: step.name, note };
    case 'active':
      return {
        key,
        state: 'current',
        title: step.name,
        person: instance.approvers.map((person) => person.displayName).join('、') || null,
        note,
        waiting: step.activatedAt ? `${relativeTime(step.activatedAt)}送達` : null,
        time: step.activatedAt ? formatDate(step.activatedAt) : null,
      };
    case 'stopped':
      return { key, state: 'pending', title: step.name, note: '未進行' };
    default:
      return { key, state: 'pending', title: step.name, note };
  }
}

/** 詳情的時間軸：第一列是送出，之後依序是各關。 */
export function flowTimeline(instance: FlowInstanceDto): ApprovalStep[] {
  return [
    {
      key: 'submit',
      state: 'done',
      title: '送出申請',
      person: instance.applicant.displayName,
      time: formatDate(instance.submittedAt),
      dateTime: instance.submittedAt,
    },
    ...instance.steps.map((step) => timelineStep(step, instance)),
  ];
}

/** 時間軸標題旁的進度：審核中寫第幾關，結束後寫結果。 */
export function flowProgress(instance: FlowInstanceDto): string {
  const total = instance.steps.length;
  if (instance.status !== 'pending' || !instance.currentStep) return '';
  const index = instance.steps.findIndex((step) => step.stepNumber === instance.currentStep);
  return `第 ${index + 1} 關，共 ${total} 關`;
}
