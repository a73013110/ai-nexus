import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { Notice } from '@shared/ui/notice';
import { AccessControlStore } from './access-control-store';
import { ViewScope } from '@shared/browser/view-scope';
import { accessPageNames, rolesTour } from './access-control-tour';
import { AccessEditorDialog } from './roles/access-editor-dialog';
import { AdminRolesTab } from './roles/admin-roles-tab';

/** 角色：組合功能群組，再指派給使用者。 */
@Component({
  selector: 'nx-roles-page',
  imports: [Notice, FeaturePage, AccessEditorDialog, AdminRolesTab],
  providers: [AccessControlStore, ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
      [title]="session.featureName('access-control.roles')"
      description="以角色組合功能群組；使用者取得角色後才能使用其中的功能。"
      layoutMode="data"
      [tour]="tour()"
    >
      <!-- 首次載入由表格的骨架表示；讀取失敗且沒有資料時只留提示與重試。 -->
      @if (store.error()) {
        <nx-notice
          [message]="store.error()"
          retryable
          [retrying]="store.refreshing()"
          (retry)="store.load()"
        />
      }
      @if (store.catalog() || !store.error()) {
        <nx-admin-roles-tab class="ui-table-fill" (edit)="editor.open('role', $event)" />
      }
    </nx-feature-page>
    <nx-access-editor-dialog #editor />`,
})
export class RolesPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AccessControlStore);
  readonly tour = computed(() =>
    rolesTour({
      names: accessPageNames((id) => this.session.featureName(id)),
      assign: this.session.has('identity.users'),
    }),
  );
}
