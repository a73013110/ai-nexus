import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarPanel } from './sidebar-panel';
import { WorkspaceLayout } from './workspace-layout';
import { WorkspaceSidebar } from './workspace-sidebar';

/**
 * The signed-in frame. The sidebar lives here once, so navigating between pages keeps its state
 * and lets motion continue across routes; pages contribute only their main area and an optional
 * sidebar panel.
 */
@Component({
  selector: 'nx-workspace-shell',
  imports: [RouterOutlet, NgTemplateOutlet, WorkspaceSidebar],
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
    </div>`,
})
export class WorkspaceShell {
  readonly layout = inject(WorkspaceLayout);
  readonly panel = inject(SidebarPanel);
}
