import { Notice } from '@shared/ui/notice';
import { CompactDialog } from '@shared/ui/compact-dialog';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterRenderEffect,
  computed,
  effect,
  inject,
  linkedSignal,
  untracked,
  viewChild,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { below } from '@shared/browser/breakpoints';
import { mediaQuery } from '@shared/browser/media-query';
import { anchoredPopover } from '@shared/browser/anchored-popover';
import { positionBesidePopover, positionPopover } from '@shared/browser/popover-position';
import { WorkspaceLayout } from '@core/layout/workspace-layout';
import { NotificationStore } from './notification-store';
import { Icon } from '@shared/ui/icon';
import { Tabs, type TabItem } from '@shared/ui/tabs';
import { ToastHistory } from '@shared/ui/toast-history';
import { ToastService } from '@shared/ui/toast-service';
import { FrameMotion, ViewMotion } from '@shared/ui/view-motion';
import { NotificationFeed } from './notification-feed';

/**
 * 伺服器收件匣，以及旁邊此分頁的提示：收件匣才會保留，工作階段列表只用來找讀取前就消失的確認訊息。
 * 桌面版是綁定到鈴鐺的非模態 popover；手機上是同內容的模態底部面板。兩者都會跟隨所選分頁的高度。
 */
@Component({
  selector: 'nx-notification-center',
  imports: [
    Notice,
    CompactDialog,
    FrameMotion,
    NgTemplateOutlet,
    NotificationFeed,
    Icon,
    Tabs,
    ToastHistory,
    ViewMotion,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './notification-center.scss',
  template: `<ng-template #content>
      <header class="dialog-bar">
        <div class="dialog-title">
          <h2 id="notification-title">通知</h2>
          @if (store.unread()) {
            <small>{{ store.unread() }} 則未讀</small>
          }
        </div>
        @if (store.view() === 'inbox') {
          <button
            type="button"
            class="quiet-button"
            [attr.aria-pressed]="store.unreadOnly()"
            (click)="toggleUnreadOnly()"
          >
            只看未讀
          </button>
          <button
            type="button"
            class="icon-button"
            aria-label="全部標為已讀"
            title="全部標為已讀"
            [disabled]="!store.unread() || store.loading()"
            (click)="store.readAll()"
          >
            <nx-icon name="check" />
          </button>
          <button
            type="button"
            class="icon-button"
            aria-label="重新整理通知"
            title="重新整理"
            [disabled]="store.loading()"
            (click)="store.refresh()"
          >
            <nx-icon name="refresh" />
          </button>
        }
        <button
          autofocus
          type="button"
          class="icon-button"
          aria-label="關閉通知"
          (click)="store.close()"
        >
          <nx-icon name="close" />
        </button>
      </header>
      <nx-tabs
        #tabs
        label="通知來源"
        [items]="views()"
        [value]="store.view()"
        (valueChange)="store.view.set($any($event))"
      />
      <div
        role="tabpanel"
        class="notification-panel"
        [id]="tabs.panelId(store.view())"
        [attr.aria-labelledby]="tabs.tabId(store.view())"
        [nxViewMotion]="store.view()"
      >
        @if (store.view() === 'session') {
          <nx-toast-history (activated)="leave()" />
        } @else {
          @defer (when store.opened()) {
            <nx-notification-feed />
          } @loading {
            <p class="form-note" role="status">正在載入通知...</p>
          } @error {
            <nx-notice message="通知介面暫時無法載入，請重新整理頁面。" />
          }
        }
      </div>
    </ng-template>
    @if (sheet()) {
      <dialog
        nxCompactDialog
        #sheetDialog
        size="md"
        class="platform-dialog notification-center"
        aria-labelledby="notification-title"
        (cancel)="store.close()"
        (close)="store.close()"
      >
        <ng-container [ngTemplateOutlet]="content" />
      </dialog>
    } @else {
      <div
        #panel
        nxFrameMotion
        popover="auto"
        role="dialog"
        aria-labelledby="notification-title"
        class="popover-panel notification-center notification-popover ui-density-compact"
        (toggle)="toggled($event)"
        (keydown)="key($event)"
      >
        <ng-container [ngTemplateOutlet]="content" />
      </div>
    }`,
})
export class NotificationCenter {
  readonly store = inject(NotificationStore);
  private readonly toasts = inject(ToastService);
  private readonly layout = inject(WorkspaceLayout);
  readonly views = computed<TabItem[]>(() => [
    { value: 'inbox', label: '通知匣' },
    { value: 'session', label: '本次工作階段', count: this.toasts.history().length },
  ]);
  readonly toggle = (value: boolean) => !value;
  private readonly phone = mediaQuery(below('phone'));
  /** 手機使用模態底部面板，較寬的螢幕使用 popover。開啟時決定，退場時沿用。 */
  readonly sheet = linkedSignal<boolean, boolean>({
    source: this.store.opened,
    computation: (opened, previous) =>
      opened || !previous ? untracked(this.phone) : previous.value,
  });
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('sheetDialog');
  private readonly panel = viewChild<ElementRef<HTMLDivElement>>('panel');
  private readonly popover = anchoredPopover({
    panel: () => this.panel()?.nativeElement,
    place: (panel) => this.place(panel),
  });
  constructor() {
    afterRenderEffect(() => {
      const opened = this.store.opened();
      const dialog = this.dialog()?.nativeElement;
      if (dialog && opened && !dialog.open) dialog.showModal();
      else if (dialog && !opened && dialog.open) dialog.close();
      const panel = this.panel()?.nativeElement;
      if (!panel) return;
      const shown = panel.matches(':popover-open');
      if (opened && !shown) {
        const trigger = this.store.trigger?.isConnected ? this.store.trigger : undefined;
        // 鈴鐺成為觸發者：Tab 會從鈴鐺進入面板，離開後再回到鈴鐺。
        this.popover.show(trigger);
      } else if (!opened && shown) this.popover.hide();
    });
    // 開啟後才載入動態；面板隨內容增長時仍要貼在錨點上。
    const observer = new ResizeObserver(() => this.popover.reposition());
    afterRenderEffect((onCleanup) => {
      const panel = this.panel()?.nativeElement;
      if (!panel) return;
      observer.observe(panel);
      onCleanup(() => observer.unobserve(panel));
    });
    // 面板開啟中跨越手機斷點，會讓畫面上留著錯誤的形式。
    effect(() => {
      const phone = this.phone();
      untracked(() => {
        if (this.store.opened() && this.sheet() !== phone) this.store.close();
      });
    });
    inject(DestroyRef).onDestroy(() => observer.disconnect());
  }
  /** 在鈴鐺下方，或在圖示欄上時位於其旁邊。 */
  private place(panel: HTMLElement) {
    const trigger = this.store.trigger;
    if (!trigger?.isConnected) {
      panel.style.left = panel.style.top = '12px';
      return;
    }
    if (this.layout.compact())
      positionBesidePopover(
        trigger,
        panel,
        480,
        640,
        trigger.closest('.workspace-sidebar')?.getBoundingClientRect().right,
      );
    else positionPopover(trigger, panel, 480, 'right', 640);
  }
  /** 點外部輕關閉由原生處理；store 同步，焦點回到鈴鐺。 */
  toggleUnreadOnly() {
    this.store.unreadOnly.update(this.toggle);
    this.store.refresh();
  }
  toggled(event: Event) {
    this.popover.sync(event);
    if (this.popover.opened()) return;
    if (this.store.opened()) this.store.close();
    const focused = document.activeElement;
    if (!focused || focused === document.body || this.panel()?.nativeElement.contains(focused))
      this.store.trigger?.focus({ preventScroll: true });
  }
  key(event: KeyboardEvent) {
    if (event.key !== 'Escape') return;
    // 在這裡處理，避免手機側邊欄的 Escape 把底下的抽屜一起關掉。
    event.preventDefault();
    event.stopPropagation();
    this.store.close();
  }
  /** 已記錄的動作可能會導覽，因此面板與手機側邊欄要先讓開。 */
  leave() {
    this.store.close();
    this.layout.closeMobile();
  }
}
