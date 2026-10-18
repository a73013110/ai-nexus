import { describe, expect, it } from 'vitest';
import { salutation } from './login-welcome';

describe('salutation', () => {
  it('follows the local time of day', () => {
    expect([4, 5, 10, 11, 13, 14, 17, 18, 23].map(salutation)).toEqual([
      '晚上好',
      '早安',
      '早安',
      '午安',
      '午安',
      '下午好',
      '下午好',
      '晚上好',
      '晚上好',
    ]);
  });
});
