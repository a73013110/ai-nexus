import { describe, expect, it } from 'vitest';
import { DEVELOPMENT_VERSION, outdated } from './app-update-notice';

describe('outdated', () => {
  it('flags a tab whose front end is older than the server', () => {
    expect(outdated('2026.11.4', '2026.11.5')).toBe(true);
    expect(outdated('2026.11.5', '2026.11.5')).toBe(false);
  });

  it('stays quiet before any signed-in response and on development builds', () => {
    expect(outdated('2026.11.4', null)).toBe(false);
    expect(outdated(DEVELOPMENT_VERSION, '2026.11.5')).toBe(false);
  });
});
