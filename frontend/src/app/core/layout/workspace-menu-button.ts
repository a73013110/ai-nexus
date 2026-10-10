import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { WorkspaceLayout } from './workspace-layout';
import { Icon } from '../../shared/ui/icon';

/** The same reachable mobile navigation entry on chat, feature pages and readers. */
@Component({
  selector: 'nx-workspace-menu-button',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './workspace-menu-button.scss',
  template: `@if (layout.narrow()) {
    <button
      type="button"
      class="icon-button workspace-menu-button"
      aria-label="展開側欄"
      title="展開側欄"
      aria-controls="workspace-sidebar"
      [attr.aria-expanded]="layout.overlay()"
      (click)="layout.toggle()"
    >
      <nx-icon name="sidebar" />
    </button>
  }`,
})
export class WorkspaceMenuButton {
  readonly layout = inject(WorkspaceLayout);
}
