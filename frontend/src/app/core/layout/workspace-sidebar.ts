import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
  afterRenderEffect,
} from '@angular/core';
import { WorkspaceBrand } from './workspace-brand';
import { WorkspaceNavigation } from './workspace-navigation';
import { AccountMenu } from './account-menu';
import { Icon } from '../../shared/ui/icon';
import { WorkspaceLayout } from './workspace-layout';
import { NotificationStore } from '../../features/notifications/notification-store';
import { CountBadge } from '../../shared/ui/count-badge';

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
  template: `@if (layout.overlay()) {
      <button class="workspace-backdrop" aria-label="收合側欄" (click)="collapse()"></button>
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
          (click)="notifications.open()"
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
      [class.sidebar-navigation-bottom]="collapsibleNavigation()"
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
  }
  collapse() {
    this.layout.closeMobile();
  }
  key(event: KeyboardEvent) {
    if (event.defaultPrevented || !this.layout.overlay() || document.querySelector('dialog[open]'))
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
