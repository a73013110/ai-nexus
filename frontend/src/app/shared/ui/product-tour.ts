import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationStart, Router } from '@angular/router';
import type { Driver, DriveStep } from 'driver.js';
import { ThemeService } from '../../core/preferences/theme-service';

export interface TourStep {
  target: string;
  title: string;
  description: string;
  side?: 'top' | 'right' | 'bottom' | 'left';
}
export interface ProductTourDefinition {
  steps: readonly TourStep[];
}
const escape = (value: string) =>
  value.replace(
    /[&<>"']/g,
    (character) =>
      ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character]!,
  );

/** Lazy tour engine; pages supply content, while lifecycle, motion and theming stay shared. */
@Injectable({ providedIn: 'root' })
export class ProductTour {
  private readonly themes = inject(ThemeService);
  private instance?: Driver;
  private sequence = 0;
  private stylesheet?: Promise<void>;
  readonly loading = signal(false);
  readonly active = signal(false);
  constructor() {
    inject(Router)
      .events.pipe(takeUntilDestroyed())
      .subscribe((event) => {
        if (event instanceof NavigationStart) this.stop();
      });
    inject(DestroyRef).onDestroy(() => this.stop());
  }
  async start(definition: ProductTourDefinition) {
    this.stop();
    const sequence = ++this.sequence;
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.loading.set(true);
    try {
      const [{ driver }] = await Promise.all([import('driver.js'), this.loadStyles()]);
      if (sequence !== this.sequence) return;
      const visible = (selector: string) =>
        [...document.querySelectorAll<HTMLElement>(selector)].find(
          (element) => element.checkVisibility() && !element.closest('[inert]'),
        );
      const steps: DriveStep[] = definition.steps
        .filter((step) => visible(step.target))
        .map((step) => ({
          element: () => visible(step.target)!,
          popover: {
            title: escape(step.title),
            description: escape(step.description),
            side: step.side ?? 'top',
            align: 'center',
          },
        }));
      if (!steps.length) return;
      this.instance = driver({
        steps,
        animate: !this.themes.reducedMotion(),
        duration: 320,
        smoothScroll: false,
        allowScroll: false,
        disableActiveInteraction: true,
        skipMissingElement: true,
        stagePadding: 6,
        stageRadius: 12,
        overlayOpacity: 0.58,
        overlayColor: '#101c28',
        popoverClass: 'nx-tour',
        showProgress: true,
        progressText: '{{current}} / {{total}}',
        nextBtnText: '下一步',
        prevBtnText: '上一步',
        doneBtnText: '開始使用',
        closeBtnLabel: '跳過導覽',
        onPopoverRender: (popover, { index }) => {
          const eyebrow = document.createElement('span');
          eyebrow.className = 'tour-eyebrow';
          eyebrow.textContent = 'AI NEXUS · 快速導覽';
          popover.wrapper.prepend(eyebrow);
          popover.wrapper.style.setProperty(
            '--tour-progress',
            String(((index ?? 0) + 1) / steps.length),
          );
          popover.wrapper.classList.toggle('tour-reduced-motion', this.themes.reducedMotion());
        },
        onDestroyed: () => {
          this.instance = undefined;
          this.active.set(false);
          requestAnimationFrame(() => {
            if (sequence === this.sequence && !this.active() && opener?.isConnected)
              opener.focus({ preventScroll: true });
          });
        },
      });
      this.active.set(true);
      this.instance.drive();
    } catch (error) {
      if (sequence === this.sequence) {
        this.stop();
        throw error;
      }
    } finally {
      if (sequence === this.sequence) this.loading.set(false);
    }
  }
  stop() {
    ++this.sequence;
    this.instance?.destroy();
    this.instance = undefined;
    this.active.set(false);
    this.loading.set(false);
  }
  // driver.js styles and the Nexus overrides load only when a tour starts; the override is
  // appended after driver.css so it wins at equal specificity.
  private loadStyles() {
    return (this.stylesheet ??= Promise.all(
      ['vendor/driver/driver.css', 'driver-tour.css'].map((href) => this.loadStylesheet(href)),
    ).then(
      () => undefined,
      (error: unknown) => {
        this.stylesheet = undefined;
        throw error;
      },
    ));
  }
  private loadStylesheet(href: string) {
    return new Promise<void>((resolve, reject) => {
      const link = document.createElement('link');
      link.rel = 'stylesheet';
      link.href = new URL(href, document.baseURI).href;
      link.onload = () => resolve();
      link.onerror = () => {
        link.remove();
        reject(new Error('Tour stylesheet could not be loaded'));
      };
      document.head.append(link);
    });
  }
}
