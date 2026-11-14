import type { FlowInstanceDto, FlowStepDto } from '@core/api/schema';
import { describe, expect, it } from 'vitest';
import { flowProgress, flowStatus, flowTimeline } from './flow-presentation';

const step = (
  stepNumber: number,
  status: string,
  extra: Partial<FlowStepDto> = {},
): FlowStepDto => ({
  stepNumber,
  name: `第${stepNumber}關`,
  status,
  note: null,
  fallback: false,
  activatedAt: null,
  decidedAt: null,
  decider: null,
  onBehalf: false,
  comment: null,
  ...extra,
});

const instance = (steps: FlowStepDto[], extra: Partial<FlowInstanceDto> = {}): FlowInstanceDto => ({
  id: 'i1',
  kind: 'repository-access',
  kindName: '儲存庫權限',
  title: '申請 example/nexus 讀取權限',
  details: [],
  applicant: { id: 'u1', displayName: '王小明' },
  status: 'pending',
  currentStep: 2,
  submittedAt: '2026-11-01T01:00:00Z',
  completedAt: null,
  steps,
  approvers: [{ id: 'u2', displayName: '林美華' }],
  canDecide: false,
  onBehalf: false,
  canCancel: false,
  ...extra,
});

describe('flow presentation', () => {
  it('starts with the submission and marks the current step with its approvers', () => {
    const timeline = flowTimeline(
      instance([
        step(1, 'skipped', { note: 'not-applicable' }),
        step(2, 'active', { activatedAt: '2026-11-01T01:00:00Z' }),
        step(3, 'waiting'),
      ]),
    );

    expect(timeline.map((item) => item.state)).toEqual(['done', 'done', 'current', 'pending']);
    expect(timeline[0]?.person).toBe('王小明');
    expect(timeline[1]?.note).toBe('條件不符，略過');
    expect(timeline[2]?.person).toBe('林美華');
  });

  it('writes rejection, proxy decisions and steps the request never reached', () => {
    const timeline = flowTimeline(
      instance(
        [
          step(1, 'rejected', {
            decider: { id: 'u3', displayName: '陳志明' },
            decidedAt: '2026-11-02T01:00:00Z',
            onBehalf: true,
            comment: '請補充用途',
          }),
          step(2, 'stopped'),
        ],
        { status: 'rejected', currentStep: null },
      ),
    );

    expect(timeline[1]).toMatchObject({ title: '第1關・退回', note: '代審', memo: '請補充用途' });
    expect(timeline[2]).toMatchObject({ state: 'pending', note: '未進行' });
  });

  it('shows progress only while pending and keeps unknown statuses visible', () => {
    expect(flowProgress(instance([step(1, 'approved'), step(2, 'active')]))).toBe(
      '第 2 關，共 2 關',
    );
    expect(
      flowProgress(instance([step(1, 'approved')], { status: 'approved', currentStep: null })),
    ).toBe('');
    expect(flowStatus('archived').label).toBe('archived');
  });
});
