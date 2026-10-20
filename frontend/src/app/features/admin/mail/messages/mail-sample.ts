import type { MailSampleDto, MailSampleParameter } from '@core/api/schema';

/** 參數名對應目前填寫的字串值；旗標用 `true`／`false`。 */
export type MailSampleValues = Record<string, string>;

/** 參數的範例值，作為表單初值與「還原範例值」。 */
export function sampleExamples(sample: MailSampleDto): MailSampleValues {
  return Object.fromEntries(sample.parameters.map((p) => [p.name, p.example ?? '']));
}

/**
 * 只送出和範例不同的值：沒動的參數由伺服器以當下的範例補上（例如要求時間是「現在」，不是開啟對話框的時間）。
 */
export function changedValues(sample: MailSampleDto, values: MailSampleValues): MailSampleValues {
  const examples = sampleExamples(sample);
  return Object.fromEntries(
    sample.parameters
      .filter((p) => (values[p.name] ?? '') !== examples[p.name])
      .map((p) => [p.name, values[p.name] ?? '']),
  );
}

/** 依型別檢查目前的值；回傳第一個錯誤，沒有錯誤時為空字串。和伺服器的規則一致，送出前就能提示。 */
export function sampleError(sample: MailSampleDto, values: MailSampleValues): string {
  for (const p of sample.parameters) {
    const value = (values[p.name] ?? '').trim();
    if (!value) {
      if (p.required) return `${p.name} 必填。`;
      continue;
    }
    if (p.type === 'number' && !/^-?\d+$/.test(value)) return `${p.name} 要是整數。`;
    if (p.type === 'time' && Number.isNaN(Date.parse(value)))
      return `${p.name} 要是 ISO 8601 時間，例如 2026-01-31T09:00:00Z。`;
  }
  return '';
}

const csharpString = (value: string) =>
  '"' +
  value
    .replaceAll('\\', '\\\\')
    .replaceAll('"', '\\"')
    .replaceAll('\r', '\\r')
    .replaceAll('\n', '\\n') +
  '"';

function csharpValue(parameter: MailSampleParameter, raw: string): string {
  const value = raw.trim();
  switch (parameter.type) {
    case 'number':
      return value || '0';
    case 'flag':
      return value === 'true' ? 'true' : 'false';
    case 'time':
      return `DateTimeOffset.Parse(${csharpString(value)}, CultureInfo.InvariantCulture)`;
    default:
      return !parameter.required && !value ? 'null' : csharpString(raw);
  }
}

/**
 * 給開發者照抄的入列程式碼：範本、模型（具名參數，值為目前填寫的內容）、收件者與入列選項。
 * 敏感信另提示冪等鍵與有效時間，對應 `docs/architecture/mail-outbox.md` 的規則。
 */
export function sampleUsage(sample: MailSampleDto, values: MailSampleValues): string {
  const args = sample.parameters.map(
    (p) => `        ${p.name}: ${csharpValue(p, values[p.name] ?? p.example ?? '')}`,
  );
  const model = args.length
    ? `new ${sample.modelType}(\n${args.join(',\n')})`
    : `new ${sample.modelType}()`;
  const options = sample.sensitive
    ? `new MailEnqueueOptions(IdempotencyKey: $"${sample.category}:{challenge.Id}", ExpiresAt: challenge.ExpiresAt, RequestedBy: user.Id)`
    : 'new MailEnqueueOptions(RequestedBy: user.Id)';
  return [
    '// 在業務交易內入列，和業務變更一起 SaveChangesAsync；寄送由背景工作負責。',
    'var enqueued = await outbox.EnqueueAsync(',
    `    new ${sample.templateType}(),`,
    `    ${model},`,
    '    new MailRecipient(address, user.DisplayName, user.Id),',
    `    ${options},`,
    '    ct);',
    'if (!enqueued.IsSuccess) return enqueued.Error;   // mail_address_invalid／mail_recipient_blocked',
    'await db.SaveChangesAsync(ct);',
  ].join('\n');
}
