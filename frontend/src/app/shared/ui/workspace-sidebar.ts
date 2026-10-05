import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { WorkspaceBrand } from './workspace-brand';
import { WorkspaceNavigation } from './workspace-navigation';
import { AccountMenu } from './account-menu';

@Component({
  selector: 'aside[nxWorkspaceSidebar]',
  imports: [WorkspaceBrand, WorkspaceNavigation, AccountMenu],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'workspace-sidebar' },
  template: `<ng-content select="[sidebar-controls]" />
    <nx-workspace-brand />
    <nx-workspace-navigation
      [class.sidebar-navigation-bottom]="collapsibleNavigation()"
      [collapsible]="collapsibleNavigation()"
      (activated)="activated.emit()"
    />
    <div class="workspace-sidebar-content"><ng-content /></div>
    <nx-account-menu />`,
})
export class WorkspaceSidebar {
  readonly collapsibleNavigation = input(false);
  readonly activated = output<void>();
}
