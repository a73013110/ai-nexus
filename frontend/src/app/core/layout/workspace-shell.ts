import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { bindHotkeys } from '@shared/browser/hotkeys';
import { CommandPalette, type Command } from '@shared/ui/command-palette';
import { ProductTour } from '@shared/ui/product-tour';
import { Toaster } from '@shared/ui/toaster';
import { NavigationPreferences } from './navigation-preferences';
import { featureIcon, navigationSections } from './navigation-tree';
import { SidebarPanel } from './sidebar-panel';
import { WorkspaceCommands } from './workspace-commands';
import { WorkspaceLayout } from './workspace-layout';
import { WorkspaceSidebar } from './workspace-sidebar';

/**
 * The signed-in frame. The sidebar lives here once, so navigating between pages keeps its state
 * and lets motion continue across routes; pages contribute only their main area, an optional
 * sidebar panel and their Ctrl+K commands.
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
  readonly commands = computed<Command[]>(() => {
    const source = this.workspaceCommands.source();
    const features = navigationSections(this.pins.tree()).flatMap((section) =>
      section.features.map((feature) => ({
        id: 'feature:' + feature.id,
        label: feature.name,
        detail: section.path ? '前往功能 · ' + section.path : '前往功能',
        icon: featureIcon(feature.icon),
      })),
    );
    return [...(source?.actions() ?? []), ...features, ...(source?.entries?.() ?? [])];
  });
  constructor() {
    this.workspaceCommands.attach(() => this.palette().open());
    bindHotkeys(
      { 'mod+k': () => this.palette().open() },
      () =>
        !this.tours.active() && !this.tours.loading() && !document.querySelector('dialog[open]'),
    );
  }
  run(id: string) {
    this.layout.closeMobile();
    const feature = id.startsWith('feature:')
      ? this.pins.features().find((x) => 'feature:' + x.id === id)
      : null;
    if (feature) void this.router.navigateByUrl(feature.route!);
    else this.workspaceCommands.source()?.run(id);
  }
}
