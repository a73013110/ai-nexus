import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { openExternal } from '@shared/browser/external-link';
import { bindHotkeys } from '@shared/browser/hotkeys';
import { CommandPalette, type Command } from '@shared/ui/command-palette';
import { ProductTour } from '@shared/ui/product-tour';
import { Toaster } from '@shared/ui/toaster';
import { NavigationPreferences } from './navigation-preferences';
import { navigationIcon, navigationNodes, navigationSections } from './navigation-tree';
import { SidebarPanel } from './sidebar-panel';
import { SHORTCUTS } from './keyboard-shortcuts';
import { AppUpdateNotice } from './app-update-notice';
import { WorkspaceCommands } from './workspace-commands';
import { WorkspaceLayout } from './workspace-layout';
import { WorkspaceSidebar } from './workspace-sidebar';

/**
 * 已登入的外框。側邊欄只在這裡存在一次，因此在頁面之間導覽時會保留其狀態，且動效可以跨路由延續；
 * 頁面只提供主要區域、
 * 選用的側邊欄面板，以及它們的 Ctrl+K 指令。
 */
@Component({
  selector: 'nx-workspace-shell',
  imports: [RouterOutlet, NgTemplateOutlet, WorkspaceSidebar, CommandPalette, Toaster],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'workbench' },
  template: `<aside
      nxWorkspaceSidebar
      aria-label="工作區導覽"
      [collapsibleNavigation]="!!panel.template()"
    >
      <ng-container [ngTemplateOutlet]="panel.template()" />
    </aside>
    <div class="workspace-main" [attr.inert]="layout.overlay() ? '' : null">
      <router-outlet />
    </div>
    <nx-command-palette [commands]="commands()" (selected)="run($event)" />
    <nx-toaster />`,
})
export class WorkspaceShell {
  readonly layout = inject(WorkspaceLayout);
  readonly panel = inject(SidebarPanel);
  private readonly workspaceCommands = inject(WorkspaceCommands);
  private readonly pins = inject(NavigationPreferences);
  private readonly router = inject(Router);
  private readonly tours = inject(ProductTour);
  private readonly palette = viewChild.required(CommandPalette);
  /**
   * 選單的每個頁面與外部連結都是一個指令，依選單順序，說明帶分群路徑。ID 用節點 ID 而不是功能 ID：
   * 同一頁面可以放在兩個分群，兩個位置都要能搜尋到。
   */
  readonly commands = computed<Command[]>(() => {
    const source = this.workspaceCommands.source();
    const entries = navigationSections(this.pins.tree()).flatMap((section) =>
      section.entries.map((node) => {
        const verb = node.kind === 'link' ? '開啟外部連結' : '前往功能';
        return {
          id: 'navigation:' + node.id,
          label: node.name,
          detail: section.path ? verb + ' · ' + section.path : verb,
          icon: navigationIcon(node)!,
        };
      }),
    );
    return [...(source?.actions() ?? []), ...entries, ...(source?.entries?.() ?? [])];
  });
  constructor() {
    // 伺服器版本只出現在登入後的回應，新版提示跟著已登入的外框建立。
    inject(AppUpdateNotice);
    this.workspaceCommands.attach(() => this.palette().open());
    bindHotkeys(
      { [SHORTCUTS.commandPalette.keys]: () => this.palette().open() },
      () =>
        !this.tours.active() && !this.tours.loading() && !document.querySelector('dialog[open]'),
    );
  }
  run(id: string) {
    this.layout.closeMobile();
    const node = id.startsWith('navigation:')
      ? navigationNodes(this.pins.tree()).find((item) => 'navigation:' + item.id === id)
      : null;
    if (node?.kind === 'link' && node.url) openExternal(node.url, node.openInNewTab);
    else if (node?.route) void this.router.navigateByUrl(node.route);
    else this.workspaceCommands.source()?.run(id);
  }
}
