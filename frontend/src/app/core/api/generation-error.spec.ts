import { describe, expect, it } from 'vitest';
import { generationError } from './generation-error';

describe('generation failure guidance', () => {
  it('distinguishes lost executors, truncated streams and temporary quota limits', () => {
    expect(generationError('executor_lost')).toContain('服務已中斷');
    expect(generationError('provider_stream_incomplete')).toContain('串流提前結束');
    expect(generationError('google_quota_exceeded')).toContain('稍後重試');
    expect(generationError('unknown')).toContain('已保留');
  });
});
