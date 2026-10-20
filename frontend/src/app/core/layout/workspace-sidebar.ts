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
import { NotificationStore } from '@features/platform/notifications/notification-store';
import { CountBadge } from '@shared/ui/count-badge';
import { bindSwipe, swipeSettleTime } from '@shared/browser/swipe';
import { uniqueId } from '@shared/browser/unique-id';

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
      (activated)="closeMobileAndActivate()"
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
  readonly notificationStatusId = uniqueId('sidebar-notification-status');
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
  closeMobileAndActivate() {
    this.layout.closeMobile();
    this.activated.emit();
  }
  collapse() {
    this.layout.closeMobile();
  }
  /**
   * 開啟的抽屜會跟隨標題或遮罩上的向左拖曳；拖過寬度的三分之一或快速輕彈時關閉，
   * 否則會彈回。導覽列表保有自己的捲動。
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
              // 保持拖曳的位置，直到 closed class 生效，讓滑出動畫從那裡開始。
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
  /** 桌面版中央是非模態的，因此鈴鐺仍可觸及，並能再次關閉它。 */
  toggleNotifications(event: MouseEvent) {
    if (this.notifications.opened()) this.notifications.close();
    else this.notifications.open(event.currentTarget as HTMLElement);
  }
  key(event: KeyboardEvent) {
    // 開啟中的通知 popover 位於抽屜之外：它負責 Escape，Tab 則跟隨鈴鐺。
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
        (item) =>
          item.tabIndex >= 0 &&
          item.getClientRects().length &&
          getComputedStyle(item).visibility === 'visible' &&
          !item.classList.contains('workspace-backdrop'),
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
