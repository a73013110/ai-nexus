import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
  input,
  output,
  signal,
  afterRenderEffect,
} from '@angular/core';
import { WorkspaceBrand } from './workspace-brand';
import { WorkspaceNavigation } from './workspace-navigation';
import { AccountMenu } from './account-menu';
import { Icon } from '@shared/ui/icon';
import { WorkspaceLayout } from './workspace-layout';
import { NotificationStore } from '@features/notifications/notification-store';
import { CountBadge } from '@shared/ui/count-badge';
import { bindSwipe, swipeSettleTime } from '@shared/browser/swipe';

let sequence = 0;

@Component({
  selector: 'aside[nxWorkspaceSidebar]',
  imports: [WorkspaceBrand, WorkspaceNavigation, AccountMenu, Icon, CountBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './workspace-sidebar.scss',
  host: {
    class: 'workspace-sidebar',
    id: 'workspace-sidebar',
    '[class.is-compact]': 'layout.compact()',
    '[class.is-open]': 'layout.overlay()',
    '[attr.inert]': 'layout.narrow() && !layout.overlay() ? "" : null',
    '[attr.aria-hidden]': 'layout.narrow() && !layout.overlay() ? "true" : null',
    '[attr.role]': 'layout.overlay() ? "dialog" : null',
    '[attr.aria-modal]': 'layout.overlay() ? "true" : null',
    '[class.has-expanded-navigation]':
      'collapsibleNavigation() && navigationExpanded() && !layout.compact()',
    '(document:keydown)': 'key($event)',
  },
  template: `@if (layout.narrow()) {
      <button
        class="workspace-backdrop"
        aria-label="收合側欄"
        tabindex="-1"
        (click)="collapse()"
      ></button>
    }
    <div class="workspace-sidebar-heading">
      <nx-workspace-brand [compact]="layout.compact()" />
      <div class="workspace-sidebar-controls">
        <button
          type="button"
          class="icon-button sidebar-notifications"
          aria-label="通知"
          title="通知"
          [attr.aria-describedby]="notificationStatusId"
          [attr.aria-expanded]="notifications.opened()"
          aria-haspopup="dialog"
          (click)="toggleNotifications($event)"
        >
          <span class="icon-badge-anchor">
            <nx-icon name="bell" />
            <nx-count-badge [count]="notifications.unread()" [overlay]="true" />
          </span>
        </button>
        <button
          class="icon-button sidebar-toggle"
          type="button"
          [attr.aria-expanded]="layout.expanded()"
          [attr.aria-label]="layout.expanded() ? '收合側欄' : '展開側欄'"
          [title]="layout.expanded() ? '收合側欄' : '展開側欄'"
          (click)="layout.toggle()"
        >
          <nx-icon name="sidebar" />
        </button>
      </div>
      <span class="sr-only" role="status" aria-atomic="true" [id]="notificationStatusId"
        >{{ notifications.unread() }} 則未讀通知</span
      >
    </div>
    <nx-workspace-navigation
      [class.has-panel]="collapsibleNavigation()"
      [collapsible]="collapsibleNavigation()"
      [compact]="layout.compact()"
      [expanded]="navigationExpanded()"
      (expandedChange)="navigationExpanded.set($event)"
      (activated)="layout.closeMobile(); activated.emit()"
    />
    <div
      class="workspace-sidebar-content ui-density-compact"
      [hidden]="collapsibleNavigation() && navigationExpanded() && !layout.compact()"
    >
      <ng-content />
    </div>
    <nx-account-menu />`,
})
export class WorkspaceSidebar {
  readonly layout = inject(WorkspaceLayout);
  readonly notifications = inject(NotificationStore);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  readonly collapsibleNavigation = input(false);
  readonly navigationExpanded = signal(false);
  readonly notificationStatusId = `sidebar-notification-status-${++sequence}`;
  readonly activated = output<void>();
  constructor() {
    afterRenderEffect((onCleanup) => {
      if (!this.layout.overlay()) return;
      const frame = requestAnimationFrame(() => {
        if (this.layout.overlay())
          this.element.nativeElement
            .querySelector<HTMLButtonElement>('.sidebar-toggle')
            ?.focus({ preventScroll: true });
      });
      onCleanup(() => cancelAnimationFrame(frame));
    });
    inject(DestroyRef).onDestroy(this.bindSwipe());
  }
  collapse() {
    this.layout.closeMobile();
  }
  /**
   * The open drawer follows a leftward drag on its heading or the scrim and closes past a third of
   * its width or on a flick; otherwise it springs back. The nav list keeps its own scrolling.
   */
  private bindSwipe() {
    const host = this.element.nativeElement;
    const injector = inject(Injector);
    return bindSwipe(host, {
      axis: 'x',
      direction: -1,
      begin: (event) => {
        const target = event.target as Element;
        if (
          !this.layout.overlay() ||
          !target.closest('.workspace-sidebar-heading, .workspace-backdrop')
        )
          return null;
        const width = host.offsetWidth;
        const clear = () => {
          host.style.removeProperty('transform');
          host.style.removeProperty('--swipe-progress');
          delete host.dataset['swipe'];
        };
        return {
          distance: width / 3,
          move(offset) {
            host.dataset['swipe'] = 'drag';
            host.style.transform = `translateX(${offset}px)`;
            host.style.setProperty('--swipe-progress', String(Math.max(0, -offset) / width));
          },
          end: (dismiss) => {
            if (dismiss) {
              // Hold the dragged position until the closed class lands, so the slide out starts there.
              this.layout.closeMobile();
              afterNextRender(clear, { injector });
              return;
            }
            clear();
            host.dataset['swipe'] = 'return';
            window.setTimeout(() => {
              if (host.dataset['swipe'] === 'return') delete host.dataset['swipe'];
            }, swipeSettleTime(host));
          },
        };
      },
    });
  }
  /** The desktop center is non-modal, so the bell stays reachable and closes it again. */
  toggleNotifications(event: MouseEvent) {
    if (this.notifications.opened()) this.notifications.close();
    else this.notifications.open(event.currentTarget as HTMLElement);
  }
  key(event: KeyboardEvent) {
    // An open notification popover sits outside the drawer: it owns Escape, and Tab follows the bell.
    if (
      event.defaultPrevented ||
      !this.layout.overlay() ||
      this.notifications.opened() ||
      document.querySelector('dialog[open]')
    )
      return;
    if (event.key === 'Escape') {
      event.preventDefault();
      this.collapse();
    }
    if (event.key === 'Tab') {
      const controls = [
        ...this.element.nativeElement.querySelectorAll<HTMLElement>(
          'a[href], button:enabled, input, select, [tabindex="0"]',
        ),
      ].filter(
        (x) =>
          x.tabIndex >= 0 &&
          x.getClientRects().length &&
          getComputedStyle(x).visibility === 'visible' &&
          !x.classList.contains('workspace-backdrop'),
      );
      const first = controls[0],
        last = controls.at(-1);
      if (
        (event.shiftKey && document.activeElement === first) ||
        (!event.shiftKey && document.activeElement === last) ||
        !controls.includes(document.activeElement as HTMLElement)
      ) {
        event.preventDefault();
        (event.shiftKey ? last : first)?.focus();
      }
    }
  }
}
