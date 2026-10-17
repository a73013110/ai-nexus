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
    const items = fixture();
    for (let index = 1; index <= 500; index++) items.publisher.set('文'.repeat(index));
    expect(items.callbacks.size).toBe(1);
    expect(items.values).toEqual([]);
    items.tick();
    expect(items.values).toEqual(['文'.repeat(500)]);
  });
  it('flushes before terminal status and does not publish a duplicate later', () => {
    const items2 = fixture();
    items2.publisher.set('完整回答👋');
    items2.publisher.flush();
    expect(items2.values).toEqual(['完整回答👋']);
    expect(items2.callbacks.size).toBe(0);
    items2.publisher.set('完整回答👋');
    items2.tick();
    expect(items2.values).toHaveLength(1);
  });
  it('replaces reconnect snapshots and cancels queued content on abort', () => {
    const items3 = fixture();
    items3.publisher.set('舊');
    items3.tick();
    items3.publisher.set('伺服器重播快照');
    items3.tick();
    items3.publisher.set('已取消的更新');
    items3.publisher.dispose();
    items3.tick();
    expect(items3.values).toEqual(['舊', '伺服器重播快照']);
  });
});
