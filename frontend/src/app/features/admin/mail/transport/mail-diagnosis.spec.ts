import {
  diagnosisStatus,
  diagnosisStep,
  diagnosisVerdict,
  groupedThumbprint,
} from './mail-diagnosis';

describe('mail diagnosis presentation', () => {
  const step = (id: string, status: string, summary = '') => ({
    id,
    status,
    summary,
    elapsedMs: 1,
    smtpStatus: null,
  });

  it('names the first failing step as the verdict', () => {
    const verdict = diagnosisVerdict({
      succeeded: false,
      steps: [
        step('connect', 'passed'),
        step('tls', 'failed', '憑證不被接受'),
        step('auth', 'skipped'),
      ],
    });
    expect(verdict.tone).toBe('danger');
    expect(verdict.message).toBe('在「TLS」失敗：憑證不被接受');
  });

  it('passes with a caution when a step only warned', () => {
    expect(
      diagnosisVerdict({
        succeeded: true,
        steps: [step('certificate', 'warning', '撤銷狀態無法確認'), step('auth', 'passed')],
      }),
    ).toEqual({ tone: 'warning', message: '可以寄送，但「伺服器憑證」需要注意：撤銷狀態無法確認' });
    expect(diagnosisVerdict({ succeeded: true, steps: [step('reset', 'passed')] }).tone).toBe(
      'success',
    );
  });

  it('labels steps and statuses and keeps unknown ones readable', () => {
    expect(diagnosisStep('recipient')).toBe('收件者（RCPT TO）');
    expect(diagnosisStep('new-step')).toBe('new-step');
    expect(diagnosisStatus('failed').tone).toBe('danger');
    expect(diagnosisStatus('odd')).toEqual({ label: 'odd', tone: 'neutral' });
    expect(groupedThumbprint('769512A08F6B')).toBe('7695 12A0 8F6B');
  });
});
