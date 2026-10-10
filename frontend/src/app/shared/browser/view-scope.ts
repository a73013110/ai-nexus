import { safeMessage } from '../../core/errors/safe-errors';
import { DestroyRef, Injectable, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth-service';

/** Per-view guards and timers prevent late responses from crossing account/navigation changes. */
@Injectable()
export class ViewScope {
  private readonly auth = inject(AuthService);
  private alive = true;
  private timers = new Set<ReturnType<typeof setTimeout>>();
  private keyed = new Map<string, ReturnType<typeof setTimeout>>();
  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.alive = false;
      this.timers.forEach(clearTimeout);
      this.timers.clear();
    });
  }
  guard() {
    const generation = this.auth.generation();
    return () => this.alive && generation === this.auth.generation();
  }
  later(work: () => void, delay = 2000, key?: string) {
    const guard = this.guard();
    if (key && this.keyed.has(key)) {
      const old = this.keyed.get(key)!;
      clearTimeout(old);
      this.timers.delete(old);
    }
    const timer = setTimeout(() => {
      this.timers.delete(timer);
      if (key) this.keyed.delete(key);
      if (guard()) work();
    }, delay);
    this.timers.add(timer);
    if (key) this.keyed.set(key, timer);
  }
  cancel(key: string) {
    const timer = this.keyed.get(key);
    if (timer !== undefined) {
      clearTimeout(timer);
      this.timers.delete(timer);
      this.keyed.delete(key);
    }
  }
  message(error: unknown) {
    return safeMessage(error);
  }
}
