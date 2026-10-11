import { ApplicationRef, inject, Injectable } from '@angular/core';
import {
  withViewTransitions,
  type ActivatedRouteSnapshot,
  type ViewTransitionInfo,
} from '@angular/router';
import { ThemeService } from '../preferences/theme-service';

/**
 * Shared-element morphs with the View Transitions API. Static pairs are named in `motion.scss`
 * under `:root[data-morphing]`, so the names (and the stacking contexts they create) exist only
 * while a transition runs; one-off pairs are armed by the element they start from and land on the
 * element marked `data-morph="<name>"`. Without the API, or with reduced motion, updates apply
 * directly.
 */
@Injectable({ providedIn: 'root' })
export class ViewMorph {
  private readonly app = inject(ApplicationRef);
  private readonly theme = inject(ThemeService);
  private armed: { name: string; element: HTMLElement } | null = null;
  private running = 0;
  private morphs = 0;
  private kind: { name: string; origin?: { x: number; y: number } } | null = null;
  private settled: Promise<unknown> = Promise.resolve();

  /** A morph is running; route transitions give way instead of cutting it short. */
  get active() {
    return this.morphs > 0;
  }

  get enabled() {
    return 'startViewTransition' in document && !this.theme.reducedMotion();
  }

  /**
   * The next transition (a route change or `run`) is choreographed as `name`: it is exposed as
   * `:root[data-morph-kind]` while it runs, with `origin` (viewport pixels) as `--morph-x`,
   * `--morph-y` and the radius `--morph-r` that reaches the farthest corner from it.
   */
  next(name: string, origin?: { x: number; y: number }) {
    this.kind = { name, origin };
  }

  /** A transition is not going to run; the pending kind must not leak into a later one. */
  discard() {
    this.kind = null;
  }

  /** Resolves once the `run` morphs started so far have finished. */
  whenSettled() {
    return this.settled;
  }

  /** Marks the document while `transition` runs so the CSS names apply. */
  track(transition: ViewTransition) {
    const root = document.documentElement;
    const kind = this.kind;
    this.kind = null;
    this.running++;
    root.dataset['morphing'] = '';
    if (kind) {
      root.dataset['morphKind'] = kind.name;
      if (kind.origin) {
        const { x, y } = kind.origin;
        const reach = Math.hypot(Math.max(x, innerWidth - x), Math.max(y, innerHeight - y));
        root.style.setProperty('--morph-x', `${x}px`);
        root.style.setProperty('--morph-y', `${y}px`);
        root.style.setProperty('--morph-r', `${Math.ceil(reach)}px`);
      }
    }
    // A newer transition skips this one; its promises then reject, which is expected.
    transition.ready.catch(() => undefined);
    return transition.finished
      .catch(() => undefined)
      .finally(() => {
        if (--this.running === 0) delete root.dataset['morphing'];
        if (kind && root.dataset['morphKind'] === kind.name) {
          delete root.dataset['morphKind'];
          for (const name of ['--morph-x', '--morph-y', '--morph-r'])
            root.style.removeProperty(name);
        }
      });
  }

  /** The next `run` for `name` starts from this element. */
  arm(name: string, element: HTMLElement) {
    this.armed = { name, element };
  }

  /**
   * Applies `update` inside a view transition. With `name`, it only animates when an armed source
   * for that name is still on the page; otherwise the update is applied at once.
   */
  async run(update: () => void, name?: string) {
    const source = this.armed?.name === name ? this.armed?.element : null;
    if (name) this.armed = null;
    if (!this.enabled || (name && !source?.isConnected)) {
      this.discard();
      return update();
    }
    if (name && source) source.style.viewTransitionName = name;
    let target: HTMLElement | null = null;
    this.morphs++;
    const transition = document.startViewTransition(() => {
      update();
      this.app.tick();
      if (!name || !source) return;
      source.style.viewTransitionName = '';
      target = document.querySelector<HTMLElement>(`[data-morph="${name}"]`);
      if (target) target.style.viewTransitionName = name;
    });
    this.settled = this.track(transition).finally(() => {
      this.morphs--;
      if (source) source.style.viewTransitionName = '';
      if (target) target.style.viewTransitionName = '';
    });
    await transition.updateCallbackDone.catch(() => undefined);
  }
}

const reusedPage = (from: ActivatedRouteSnapshot, to: ActivatedRouteSnapshot) => {
  let a: ActivatedRouteSnapshot | null = from;
  let b: ActivatedRouteSnapshot | null = to;
  while (a?.firstChild && b?.firstChild) [a, b] = [a.firstChild, b.firstChild];
  return !!a?.routeConfig && a.routeConfig === b?.routeConfig;
};

/**
 * Router view transitions: pages cross-fade while named elements (brand, navigation pill) morph.
 * Angular activates the new route one task later with this feature (also when skipped), so code
 * and tests that act right after a click wait for the URL instead of assuming it changed.
 * Skipped for reduced motion, while a `ViewMorph` runs, and when the page component is reused
 * (`/chat` ↔ `/chat/:id`), whose own state changes decide what morphs.
 */
export const withNexusViewTransitions = () =>
  withViewTransitions({
    skipInitialTransition: true,
    onViewTransitionCreated: ({ transition, from, to }: ViewTransitionInfo) => {
      const morph = inject(ViewMorph);
      if (!morph.enabled || morph.active || reusedPage(from, to)) {
        morph.discard();
        transition.skipTransition();
      } else void morph.track(transition);
    },
  });
