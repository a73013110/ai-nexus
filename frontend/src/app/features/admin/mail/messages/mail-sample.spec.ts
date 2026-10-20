import type { MailSampleDto } from '@core/api/schema';
import { changedValues, sampleError, sampleExamples, sampleUsage } from './mail-sample';

describe('mail samples', () => {
  const sample: MailSampleDto = {
    category: 'account.security-notice',
    name: '帳號安全通知',
    description: '',
    priority: 'security',
    sensitive: false,
    templateType: 'AccountNoticeMail',
    modelType: 'AccountNoticeMailModel',
    parameters: [
      { name: 'DisplayName', type: 'text', required: true, description: '', example: '王小明' },
      { name: 'Detail', type: 'text', required: false, description: '', example: '補充' },
      { name: 'Minutes', type: 'number', required: true, description: '', example: '15' },
      { name: 'ByAdministrator', type: 'flag', required: true, description: '', example: 'false' },
    ],
  };

  it('sends only the values that differ from the examples', () => {
    const values = { ...sampleExamples(sample), Detail: '', Minutes: '15' };
    expect(changedValues(sample, values)).toEqual({ Detail: '' });
  });

  it('checks required values and numbers before sending', () => {
    expect(sampleError(sample, sampleExamples(sample))).toBe('');
    expect(sampleError(sample, { ...sampleExamples(sample), DisplayName: ' ' })).toContain(
      'DisplayName',
    );
    expect(sampleError(sample, { ...sampleExamples(sample), Minutes: '十' })).toContain('整數');
    expect(sampleError(sample, { ...sampleExamples(sample), Detail: '' })).toBe('');
  });

  it('writes C# usage with named arguments and escaped strings', () => {
    const usage = sampleUsage(sample, {
      ...sampleExamples(sample),
      DisplayName: '王 "小明"',
      Detail: '',
      ByAdministrator: 'true',
    });
    expect(usage).toContain('new AccountNoticeMail(),');
    expect(usage).toContain('DisplayName: "王 \\"小明\\""');
    expect(usage).toContain('Detail: null');
    expect(usage).toContain('Minutes: 15');
    expect(usage).toContain('ByAdministrator: true),');
    expect(usage).toContain('new MailEnqueueOptions(RequestedBy: user.Id)');
  });

  it('reminds sensitive mails to set an idempotency key and expiry', () => {
    const usage = sampleUsage({ ...sample, sensitive: true, parameters: [] }, {});
    expect(usage).toContain('new AccountNoticeMailModel()');
    expect(usage).toContain('IdempotencyKey: $"account.security-notice:{challenge.Id}"');
    expect(usage).toContain('ExpiresAt: challenge.ExpiresAt');
  });
});
