import { describe, expect, it } from 'vitest';
import {
  ApiError,
  ClientValidationError,
  issueInMessage,
  safeMessage,
  systemProblem,
} from './safe-errors';

describe('public error boundary', () => {
  const secret = 'Password=never-display; https://private.test/model C:\\service\\file';
  const issue = 'NX-' + 'A'.repeat(32);
  it('ignores provider titles, original errors and unsupported public codes', () => {
    for (const error of [
      new ApiError(503, 'provider_failed', secret, issue),
      new ApiError(400, 'unknown', secret),
      new Error(secret),
      secret,
    ]) {
      const text = safeMessage(error);
      expect(text).not.toContain('never-display');
      expect(text).not.toContain('private.test');
      expect(text).not.toContain('service\\file');
    }
    expect(safeMessage(new ApiError(503, 'provider_failed', secret, issue))).toBe(
      '操作未完成，請聯絡管理員。查證代碼：' + issue,
    );
    expect(safeMessage(new ApiError(400, 'toString', secret))).not.toContain('native code');
  });
  it('retains fixed validation hints and labels offline codes as local', () => {
    expect(safeMessage(new ApiError(400, 'invalid_request', secret))).toBe('請求格式不正確。');
    expect(safeMessage(new ApiError(400, 'invalid_request', secret, issue))).toContain(
      '查證代碼：' + issue,
    );
    const text = safeMessage(new ApiError(0, 'network_error', secret));
    expect(text).toContain('LOCAL-');
    expect(text).toContain('尚未記入伺服器');
    expect(text).not.toContain('查證代碼：NX');
    expect(systemProblem('caller-forged-code')).not.toContain('caller-forged-code');
  });
  it('produces stable local codes for the same error and validates copy targets', () => {
    const error = new Error(secret);
    expect(safeMessage(error)).toBe(safeMessage(error));
    expect(issueInMessage('失敗 ' + issue)).toBe(issue);
    expect(issueInMessage(secret)).toBeNull();
  });
  it('preserves reviewed local validation without accepting arbitrary messages', () => {
    expect(safeMessage(new ClientValidationError('attachmentQuota'))).toContain('附件容量不足');
    expect(safeMessage(new ClientValidationError('storageLimit'))).toContain('最多九位小數');
    expect(safeMessage(new ClientValidationError('tokenLimit'))).toContain('整數');
    expect(safeMessage(new Error('附件容量不足 ' + secret))).not.toContain('never-display');
  });
});
