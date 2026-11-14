import type { ApproverRuleDto, FlowDefinitionDto } from '@core/api/schema';
import { describe, expect, it } from 'vitest';
import {
  describeStep,
  newStep,
  sameSteps,
  stepProblem,
  toDraft,
  toRequest,
} from './flow-step-draft';

const rules: ApproverRuleDto[] = [
  {
    key: 'applicant-manager',
    name: '申請人的主管',
    description: '',
    argument: 'levels',
    kinds: [],
  },
  { key: 'users', name: '指定人員', description: '', argument: 'users', kinds: [] },
  {
    key: 'repository-stewards',
    name: '儲存庫負責人',
    description: '',
    argument: 'none',
    kinds: [],
  },
];
const kind: FlowDefinitionDto = {
  kind: 'repository-access',
  name: '儲存庫權限',
  description: '',
  facts: [
    {
      key: 'permission',
      name: '權限',
      values: [
        { value: 'read', name: '讀取' },
        { value: 'write', name: '寫入' },
      ],
    },
  ],
  steps: [],
  updatedAt: null,
  rowVersion: 1,
};
const people = new Map([['u1', { id: 'u1', displayName: '林美華' }]]);

describe('flow step draft', () => {
  it('round-trips the saved argument and condition of the selected rule only', () => {
    const draft = toDraft(
      {
        name: '主管',
        approverRule: 'applicant-manager',
        approverArgument: '{"levels":2}',
        conditionFact: 'permission',
        conditionValue: 'write',
      },
      people,
    );

    expect(toRequest({ ...draft, users: [{ id: 'u1', displayName: '林美華' }] }, rules)).toEqual({
      name: '主管',
      approverRule: 'applicant-manager',
      approverArgument: '{"levels":2}',
      conditionFact: 'permission',
      conditionValue: 'write',
    });
    expect(
      toRequest({ ...draft, rule: 'repository-stewards', condition: '' }, rules),
    ).toMatchObject({
      approverArgument: null,
      conditionFact: null,
      conditionValue: null,
    });
  });

  it('explains what is missing before saving', () => {
    expect(stepProblem(newStep('users'), rules)).toBe('請填寫關卡名稱。');
    expect(stepProblem({ ...newStep('users'), name: '審核' }, rules)).toBe(
      '請至少指定一位審核人。',
    );
    expect(
      stepProblem({ ...newStep('applicant-manager'), name: '主管', levels: 9 }, rules),
    ).toContain('1 到 5');
    expect(stepProblem({ ...newStep('repository-stewards'), name: '負責人' }, rules)).toBe('');
  });

  it('ignores display-only differences when comparing drafts', () => {
    const step = {
      ...newStep('users'),
      name: '審核',
      users: [{ id: 'u1', displayName: '林美華' }],
    };
    expect(
      sameSteps(
        [step],
        [{ ...step, key: 'other', users: [{ id: 'u1', displayName: '舊名' }] }],
        rules,
      ),
    ).toBe(true);
    expect(sameSteps([step], [{ ...step, name: '複核' }], rules)).toBe(false);
  });

  it('describes a saved step in one sentence', () => {
    expect(
      describeStep(
        {
          name: '審核',
          approverRule: 'users',
          approverArgument: '{"userIds":["u1","u2"]}',
          conditionFact: 'permission',
          conditionValue: 'write',
        },
        kind,
        rules,
        people,
      ),
    ).toEqual({ approver: '指定人員：林美華、（已移除的使用者）', condition: '權限是「寫入」時' });
  });
});
