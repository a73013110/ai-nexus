import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ClientIssues } from './client-issues';
import { ApiClient } from '../api/api-client';
import { AuthService } from '../auth/auth-service';
import { ClientValidationError } from './safe-errors';

describe('controlled client reports', () => {
  afterEach(() => TestBed.resetTestingModule());
  it('ignores expected cancellation and local validation without reporting or showing a system failure', async () => {
    const post = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        { provide: ApiClient, useValue: { post } },
        { provide: AuthService, useValue: { generation: () => 0 } },
      ],
    });
    const service = TestBed.inject(ClientIssues);
    await service.report(new DOMException('secret cancellation detail', 'AbortError'), 'rejection');
    await service.report(new ClientValidationError('attachmentQuota'), 'exception');
    expect(post).not.toHaveBeenCalled();
    expect(service.notice()).toBe('');
  });
  it('reports only a bounded kind/fingerprint, deduplicates and shows the server code', async () => {
    const code = 'NX-' + 'B'.repeat(32);
    const post = vi.fn().mockResolvedValue({ issueCode: code, accepted: true });
    TestBed.configureTestingModule({
      providers: [
        { provide: ApiClient, useValue: { post } },
        { provide: AuthService, useValue: { generation: () => 0 } },
      ],
    });
    const service = TestBed.inject(ClientIssues);
    const error = new Error('secret prompt Authorization=token C:\\private\\file');
    await service.report(error, 'rejection');
    await service.report(error, 'rejection');
    expect(post).toHaveBeenCalledTimes(1);
    expect(post.mock.calls[0][0]).toBe('/api/v1/client-issues');
    const payload = post.mock.calls[0][1].body;
    expect(Object.keys(payload).sort()).toEqual(['fingerprint', 'kind']);
    expect(payload.kind).toBe('rejection');
    expect(payload.fingerprint).toMatch(/^[0-9a-f]{64}$/);
    expect(JSON.stringify(payload)).not.toContain('secret');
    expect(service.notice()).toContain(code);
  });
  it('keeps an explicitly local code when collection fails or admission is rejected', async () => {
    for (const result of ['failure', 'rejected']) {
      TestBed.resetTestingModule();
      const post =
        result === 'failure'
          ? vi.fn().mockRejectedValue(new Error('secret SQL error'))
          : vi.fn().mockResolvedValue({ issueCode: 'NX-' + 'C'.repeat(32), accepted: false });
      TestBed.configureTestingModule({
        providers: [
          { provide: ApiClient, useValue: { post } },
          { provide: AuthService, useValue: { generation: () => 0 } },
        ],
      });
      const service = TestBed.inject(ClientIssues);
      await service.report(new TypeError('secret'), 'exception');
      expect(service.notice()).toContain('LOCAL-');
      expect(service.notice()).toContain('尚未記入伺服器');
      expect(service.notice()).not.toContain('secret');
      expect(post).toHaveBeenCalledTimes(1);
    }
  });
});
