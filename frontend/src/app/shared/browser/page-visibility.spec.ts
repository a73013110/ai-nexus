import { DestroyRef } from '@angular/core';
import { describe, expect, it, vi } from 'vitest';
import { onVisibilityChange } from './page-visibility';

describe('onVisibilityChange', () => {
  it('stops listening once the owner is destroyed', () => {
    let destroy = () => {};
    const handler = vi.fn();
    onVisibilityChange(handler, {
      onDestroy: (callback: () => void) => {
        destroy = callback;
        return () => {};
      },
    } as DestroyRef);

    document.dispatchEvent(new Event('visibilitychange'));
    expect(handler).toHaveBeenCalledTimes(1);

    destroy();
    document.dispatchEvent(new Event('visibilitychange'));
    expect(handler).toHaveBeenCalledTimes(1);
  });
});
