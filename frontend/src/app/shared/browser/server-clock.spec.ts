import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { ServerClock } from './server-clock';

describe('ServerClock', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('follows the server time even when the browser clock is off', () => {
    const clock = TestBed.inject(ServerClock);
    const receivedAt = Date.now();
    clock.sync(new Date(receivedAt + 5 * 60_000).toISOString(), receivedAt);
    expect(Math.abs(clock.now() - (receivedAt + 5 * 60_000))).toBeLessThan(1000);
  });

  it('ignores a server time it cannot read', () => {
    const clock = TestBed.inject(ServerClock);
    const before = clock.now();
    clock.sync('not a date');
    expect(Math.abs(clock.now() - before)).toBeLessThan(1000);
  });
});
