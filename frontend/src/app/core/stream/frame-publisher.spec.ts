import { describe, expect, it } from 'vitest';
import { FramePublisher } from './frame-publisher';

function fixture() {
  const values: string[] = [];
  const callbacks = new Map<number, () => void>();
  let id = 0;
  const publisher = new FramePublisher(
    (value) => values.push(value),
    (callback) => {
      callbacks.set(++id, callback);
      return id;
    },
    (key) => callbacks.delete(key),
  );
  return {
    publisher,
    values,
    callbacks,
    tick: () => {
      const queued = [...callbacks.values()];
      callbacks.clear();
      queued.forEach((callback) => callback());
    },
  };
}

describe('stream frame publisher', () => {
  it('coalesces bursty deltas into the latest complete content', () => {
    const f = fixture();
    for (let i = 1; i <= 500; i++) f.publisher.set('文'.repeat(i));
    expect(f.callbacks.size).toBe(1);
    expect(f.values).toEqual([]);
    f.tick();
    expect(f.values).toEqual(['文'.repeat(500)]);
  });
  it('flushes before terminal status and does not publish a duplicate later', () => {
    const f = fixture();
    f.publisher.set('完整回答👋');
    f.publisher.flush();
    expect(f.values).toEqual(['完整回答👋']);
    expect(f.callbacks.size).toBe(0);
    f.publisher.set('完整回答👋');
    f.tick();
    expect(f.values).toHaveLength(1);
  });
  it('replaces reconnect snapshots and cancels queued content on abort', () => {
    const f = fixture();
    f.publisher.set('舊');
    f.tick();
    f.publisher.set('伺服器重播快照');
    f.tick();
    f.publisher.set('已取消的更新');
    f.publisher.dispose();
    f.tick();
    expect(f.values).toEqual(['舊', '伺服器重播快照']);
  });
});
