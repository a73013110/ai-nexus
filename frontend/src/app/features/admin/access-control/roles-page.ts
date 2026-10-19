import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { ModuleNavigation } from '@core/layout/module-navigation';
import { Notice } from '@shared/ui/notice';
import { AccessControlStore } from './access-control-store';
import { AccessEditorDialog } from './roles/access-editor-dialog';
import { AdminRolesTab } from './roles/admin-roles-tab';

/** 角色：組合功能群組，再指派給使用者。 */
@Component({
  selector: 'nx-roles-page',
  imports: [Notice, FeaturePage, ModuleNavigation, AccessEditorDialog, AdminRolesTab],
  providers: [AccessControlStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
      [title]="session.featureName('access-control.roles')"
      description="以角色組合功能群組；使用者取得角色後才能使用其中的功能。"
      layoutMode="data"
    >
      <nx-module-navigation />
      @if (store.loading()) {
        <p role="status">正在載入角色…</p>
      } @else if (store.error()) {
        <nx-notice [message]="store.error()"
          ><button notice-actions (click)="store.load()">重新載入</button>
        </nx-notice>
      }
      @if (store.catalog()) {
        <nx-admin-roles-tab class="ui-table-fill" (edit)="editor.open('role', $event)" />
      }
    </nx-feature-page>
    <nx-access-editor-dialog #editor />`,
})
export class RolesPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AccessControlStore);
}
