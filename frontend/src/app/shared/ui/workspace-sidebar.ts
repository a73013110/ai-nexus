import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
} from '@angular/core';
import { WorkspaceBrand } from './workspace-brand';
import { WorkspaceNavigation } from './workspace-navigation';
import { AccountMenu } from './account-menu';
import { Icon } from './icon';
import { WorkspaceLayout } from '../../core/preferences/workspace-layout';
import { NotificationStore } from '../../core/notifications/notification-store';

@Component({
  selector: 'aside[nxWorkspaceSidebar]',
  imports: [WorkspaceBrand, WorkspaceNavigation, AccountMenu, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'workspace-sidebar',
    '[class.is-compact]': 'layout.compact()',
    '(keydown)': 'key($event)',
  },
  template: `@if (layout.overlay()) {
      <button class="workspace-backdrop" aria-label="收合側欄" (click)="collapse()"></button>
    }
    <div class="workspace-sidebar-heading">
      <nx-workspace-brand />
      <button
        class="icon-button sidebar-toggle"
        type="button"
        [attr.aria-expanded]="!layout.compact()"
        [attr.aria-label]="layout.compact() ? '展開側欄' : '收合側欄'"
        [title]="layout.compact() ? '展開側欄' : '收合側欄'"
        (click)="layout.toggle()"
      >
        <nx-icon name="sidebar" />
      </button>
    </div>
    <nx-workspace-navigation
      [class.sidebar-navigation-bottom]="collapsibleNavigation()"
      [collapsible]="collapsibleNavigation()"
      [compact]="layout.compact()"
      (activated)="layout.closeMobile(); activated.emit()"
    />
    <div class="workspace-sidebar-content"><ng-content /></div>
    <button
      type="button"
      class="sidebar-notifications quiet-button"
      aria-label="通知"
      title="通知"
      (click)="notifications.open()"
    >
      <nx-icon name="bell" /><span>通知</span>
      @if (notifications.unread()) {
        <b
          class="notification-count"
          role="status"
          [attr.aria-label]="notifications.unread() + ' 則未讀通知'"
          >{{ notifications.unread() > 99 ? '99+' : notifications.unread() }}</b
        >
      }
    </button>
    <nx-account-menu />`,
})
export class WorkspaceSidebar {
  readonly layout = inject(WorkspaceLayout);
  readonly notifications = inject(NotificationStore);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  readonly collapsibleNavigation = input(false);
  readonly activated = output<void>();
  collapse() {
    this.layout.compact.set(true);
    this.element.nativeElement.querySelector<HTMLButtonElement>('.sidebar-toggle')?.focus();
  }
  key(event: KeyboardEvent) {
    if (!this.layout.overlay() || document.querySelector('dialog[open]')) return;
    if (event.key === 'Escape') {
      event.preventDefault();
      this.collapse();
    }
    if (event.key === 'Tab') {
      const controls = [
        ...this.element.nativeElement.querySelectorAll<HTMLElement>(
          'a[href], button:enabled, input, select, [tabindex="0"]',
        ),
      ].filter((x) => x.getClientRects().length && !x.classList.contains('workspace-backdrop'));
      const first = controls[0],
        last = controls.at(-1);
      if (
        (event.shiftKey && document.activeElement === first) ||
        (!event.shiftKey && document.activeElement === last)
      ) {
        event.preventDefault();
        (event.shiftKey ? last : first)?.focus();
      }
    }
  }
}
