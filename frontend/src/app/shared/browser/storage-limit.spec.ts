import { describe, expect, it } from 'vitest';
import { parseStorageLimitGb, storageLimitGb } from './storage-limit';

describe('storage capacity input', () => {
  it('preserves integer bytes and treats empty as inheritance', () => {
    expect(parseStorageLimitGb('')).toBeNull();
    expect(parseStorageLimitGb('5')).toBe(5_000_000_000);
    expect(parseStorageLimitGb('0')).toBe(0);
    for (const value of [
      1, 7, 1_048_576, 5_000_000_001, 999_999_999_999_999, 1_000_000_000_000_000,
    ])
      expect(parseStorageLimitGb(storageLimitGb(value))).toBe(value);
  });
  it('rejects negative, excessive, fractional byte and non-finite values', () => {
    for (const value of ['-1', '1000000.000000001', '0.0000000001', 'Infinity', 'NaN', '1e3'])
      expect(() => parseStorageLimitGb(value)).toThrow();
  });
});
