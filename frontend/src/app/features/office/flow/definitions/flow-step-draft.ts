import type {
  ApproverRuleDto,
  FlowDefinitionDto,
  FlowPersonDto,
  FlowStepDefinitionDto,
  FlowStepRequest,
} from '@core/api/schema';
import { CATALOG_FEATURES } from '@core/api/feature-catalog';

/** 與後端 `FlowDefinitionStep.MaxSteps`、`NameMaxLength`、`ApplicantManagerRule.MaxLevels`、`UsersRule.MaxUsers` 相同。 */
export const MAX_STEPS = 20;
export const STEP_NAME_MAX = 60;
export const MAX_LEVELS = 5;
export const MAX_USERS = 20;

/**
 * 編輯中的一關。規則的參數依形式（`ApproverRuleDto.argument`）分欄位保存，切換規則時其他形式的值留著，
 * 換回來不必重填；送出時只序列化目前規則用到的那一種。`key` 只在畫面上識別這一關（排序、追蹤）。
 */
export interface StepDraft {
  key: string;
  name: string;
  rule: string;
  levels: number;
  users: FlowPersonDto[];
  feature: string;
  /** `事實=值`；空字串表示每次都要經過這一關。 */
  condition: string;
}

interface Argument {
  levels?: unknown;
  userIds?: unknown;
  feature?: unknown;
}

const parse = (json: string | null): Argument => {
  if (!json) return {};
  try {
    const value: unknown = JSON.parse(json);
    return value && typeof value === 'object' ? (value as Argument) : {};
  } catch {
    return {};
  }
};

let sequence = 0;
const nextKey = () => `step-${++sequence}`;

export const conditionKey = (fact: string | null, value: string | null) =>
  fact && value ? `${fact}=${value}` : '';

/** 已儲存的一關轉成草稿；人員的姓名取自回應的 `people`，查不到時顯示「已移除的使用者」。 */
export function toDraft(
  step: FlowStepDefinitionDto,
  people: ReadonlyMap<string, FlowPersonDto>,
): StepDraft {
  const argument = parse(step.approverArgument);
  const ids = Array.isArray(argument.userIds)
    ? argument.userIds.filter((id): id is string => typeof id === 'string')
    : [];
  return {
    key: nextKey(),
    name: step.name,
    rule: step.approverRule,
    levels: typeof argument.levels === 'number' ? argument.levels : 1,
    users: ids.map((id) => people.get(id) ?? { id, displayName: '（已移除的使用者）' }),
    feature: typeof argument.feature === 'string' ? argument.feature : '',
    condition: conditionKey(step.conditionFact, step.conditionValue),
  };
}

export const newStep = (rule: string): StepDraft => ({
  key: nextKey(),
  name: '',
  rule,
  levels: 1,
  users: [],
  feature: '',
  condition: '',
});

/** 規則的參數形式；規則不存在（舊設定）時當作沒有參數。 */
export const argumentOf = (rules: readonly ApproverRuleDto[], rule: string) =>
  rules.find((item) => item.key === rule)?.argument ?? 'none';

/** 這一關還不能儲存的原因；伺服器仍會以相同規則再檢查一次。 */
export function stepProblem(step: StepDraft, rules: readonly ApproverRuleDto[]): string {
  if (!step.name.trim()) return '請填寫關卡名稱。';
  if (!rules.some((rule) => rule.key === step.rule)) return '請選擇審核人。';
  switch (argumentOf(rules, step.rule)) {
    case 'levels':
      return Number.isInteger(step.levels) && step.levels >= 1 && step.levels <= MAX_LEVELS
        ? ''
        : `往上的層數請填 1 到 ${MAX_LEVELS}。`;
    case 'users':
      return step.users.length ? '' : '請至少指定一位審核人。';
    case 'feature':
      return step.feature ? '' : '請選擇功能。';
    default:
      return '';
  }
}

export function toRequest(step: StepDraft, rules: readonly ApproverRuleDto[]): FlowStepRequest {
  const argument = (() => {
    switch (argumentOf(rules, step.rule)) {
      case 'levels':
        return JSON.stringify({ levels: step.levels });
      case 'users':
        return JSON.stringify({ userIds: step.users.map((user) => user.id) });
      case 'feature':
        return JSON.stringify({ feature: step.feature });
      default:
        return null;
    }
  })();
  const [fact = '', value = ''] = step.condition.split('=');
  return {
    name: step.name.trim(),
    approverRule: step.rule,
    approverArgument: argument,
    conditionFact: fact || null,
    conditionValue: value || null,
  };
}

/** 草稿相同（比較送出的內容，畫面用的 `key` 與姓名不算）。 */
export const sameSteps = (
  first: readonly StepDraft[],
  second: readonly StepDraft[],
  rules: readonly ApproverRuleDto[],
) =>
  JSON.stringify(first.map((step) => toRequest(step, rules))) ===
  JSON.stringify(second.map((step) => toRequest(step, rules)));

/** 規則說明的第一行：一句摘要，放在下拉選項的名稱下。 */
export const ruleSummary = (description: string) => description.split('\n', 1)[0]?.trim() ?? '';

/** 規則說明第一行之後的細節（每行一點），選定後列在欄位下方。 */
export const ruleDetail = (description: string) =>
  description.split('\n').slice(1).join('\n').trim();

/** 「何時需要這一關」的選項：每次都要，或種類宣告的事實等於某個值時。 */
export function conditionOptions(kind: FlowDefinitionDto) {
  return [
    { value: '', label: '每次都要' },
    ...kind.facts.flatMap((fact) =>
      fact.values.map((value) => ({
        value: conditionKey(fact.key, value.value),
        label: `${fact.name}是「${value.name}」時`,
      })),
    ),
  ];
}

/** 可以指定的功能：授權裡看得到的頁面與動作（讀取器不能單獨授予）。 */
export const FEATURE_OPTIONS = CATALOG_FEATURES.filter((feature) => feature.kind !== 'reader').map(
  (feature) => ({ value: feature.id, label: feature.name, description: feature.id }),
);

/** 已儲存的一關寫成一句話：審核人與參數、何時需要。 */
export function describeStep(
  step: FlowStepDefinitionDto,
  kind: FlowDefinitionDto,
  rules: readonly ApproverRuleDto[],
  people: ReadonlyMap<string, FlowPersonDto>,
) {
  return summarizeDraft(toDraft(step, people), kind, rules);
}

/** 編輯中的一關寫成一句話（收合時的摘要也用這個）；還沒選人、選功能時只寫規則名稱。 */
export function summarizeDraft(
  draft: StepDraft,
  kind: FlowDefinitionDto,
  rules: readonly ApproverRuleDto[],
) {
  const rule = rules.find((item) => item.key === draft.rule);
  const approver = (() => {
    if (!rule) return draft.rule;
    switch (rule.argument) {
      case 'levels':
        return draft.levels === 1
          ? `${rule.name}（直屬）`
          : `${rule.name}（往上 ${draft.levels} 層）`;
      case 'users':
        return draft.users.length
          ? `${rule.name}：${draft.users.map((user) => user.displayName).join('、')}`
          : rule.name;
      case 'feature': {
        if (!draft.feature) return rule.name;
        const feature = CATALOG_FEATURES.find((item) => item.id === draft.feature);
        return `${rule.name}：${feature?.name ?? draft.feature}`;
      }
      default:
        return rule.name;
    }
  })();
  const condition =
    conditionOptions(kind).find((option) => option.value === draft.condition)?.label ?? '每次都要';
  return { approver, condition };
}
