import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { ModuleNavigation } from '@core/layout/module-navigation';
import { Notice } from '@shared/ui/notice';
import { AccessControlStore } from './access-control-store';
import { AccessEditorDialog } from './roles/access-editor-dialog';
import { AdminGroupsTab } from './roles/admin-groups-tab';

/** 功能群組：授予功能、AI 模型與附件容量。 */
@Component({
  selector: 'nx-groups-page',
  imports: [Notice, FeaturePage, ModuleNavigation, AccessEditorDialog, AdminGroupsTab],
  providers: [AccessControlStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
      [title]="session.featureName('access-control.groups')"
      description="功能群組授予功能、AI 模型與附件容量，再由角色加入。"
      layoutMode="data"
    >
      <nx-module-navigation />
      @if (store.loading()) {
        <p role="status">正在載入功能群組…</p>
      } @else if (store.error()) {
        <nx-notice [message]="store.error()"
          ><button notice-actions (click)="store.load()">重新載入</button>
        </nx-notice>
      }
      @if (store.catalog()) {
        <nx-admin-groups-tab class="ui-table-fill" (edit)="editor.open('group', $event)" />
      }
    </nx-feature-page>
    <nx-access-editor-dialog #editor />`,
})
export class GroupsPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AccessControlStore);
}
