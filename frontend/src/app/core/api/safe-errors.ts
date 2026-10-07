import { publicErrorHints } from './public-error-catalog';

const serverPattern = /^NX-[0-9A-F]{32}$/;
const localPattern = /^LOCAL-[0-9A-F]{16}$/;
const localIssues = new WeakMap<object, string>();
const localValidationHints = {
  storageLimit: '容量請輸入 0 至 1,000,000 GB，最多九位小數；留空可恢復群組或預設上限。',
  tokenLimit: 'token 上限需為 0 至 1,000,000,000,000 的整數；留空繼承設定。',
  attachmentQuota: publicErrorHints['attachment_quota'],
  featureAccess: publicErrorHints['feature_forbidden'],
  evaluationFileSize: '題庫檔案最多 1.4 MB。',
  evaluationFormat: '題庫格式不正確，請先匯出一份範本。',
  retrievalEvaluationFormat: '驗收集需為含 1 至 20 題的 JSON 陣列。',
  reviewRepository: '這份 review 屬於其他程式庫。',
} as const;
/** Local validation uses fixed reviewed text, never arbitrary exception messages. */
export class ClientValidationError extends Error {
  constructor(readonly code: keyof typeof localValidationHints) {
    super(
      Object.hasOwn(localValidationHints, code)
        ? localValidationHints[code]
        : publicErrorHints['invalid_request'],
    );
  }
}
export function isCancellation(error: unknown): boolean {
  return (error instanceof Error || error instanceof DOMException) && error.name === 'AbortError';
}
export function validIssueCode(value: unknown): value is string {
  return typeof value === 'string' && serverPattern.test(value);
}
export function newLocalIssue(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(8));
  return (
    'LOCAL-' +
    Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0'))
      .join('')
      .toUpperCase()
  );
}
export function systemProblem(issueCode?: string | null): string {
  return validIssueCode(issueCode)
    ? `操作未完成，請聯絡管理員。查證代碼：${issueCode}`
    : `操作未完成，請確認連線或聯絡管理員。本機問題代碼：${localPattern.test(issueCode ?? '') ? issueCode : newLocalIssue()}（尚未記入伺服器）`;
}
/** Only reviewed operation hints or opaque issue codes enter general UI surfaces. */
export class ApiError extends Error {
  readonly issueCode: string;
  readonly traceId?: string;
  constructor(
    readonly status: number,
    readonly code: string,
    _untrustedMessage?: string,
    issueCode?: string,
  ) {
    const issue = validIssueCode(issueCode) ? issueCode : newLocalIssue();
    super(
      status < 500 && status > 0 && Object.hasOwn(publicErrorHints, code)
        ? publicErrorHints[code] + (validIssueCode(issueCode) ? ` 查證代碼：${issueCode}` : '')
        : status === 0
          ? `連線中斷，請確認區網連線後重試。本機問題代碼：${issue}（尚未記入伺服器）`
          : systemProblem(issue),
    );
    this.issueCode = issue;
  }
}
export function safeMessage(error: unknown): string {
  if (error instanceof ApiError || error instanceof ClientValidationError) return error.message;
  let code: string;
  if (typeof error === 'object' && error !== null) {
    code = localIssues.get(error) ?? newLocalIssue();
    localIssues.set(error, code);
  } else code = newLocalIssue();
  return systemProblem(code);
}
export function issueInMessage(value: string | null | undefined): string | null {
  return value?.match(/\b(?:NX-[0-9A-F]{32}|LOCAL-[0-9A-F]{16})\b/)?.[0] ?? null;
}
