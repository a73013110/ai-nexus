import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { AccessControlStore } from './access-control-store';
import { accessPageNames, groupsTour } from './access-control-tour';
import { AccessEditorDialog } from './roles/access-editor-dialog';
import { AdminGroupsTab } from './roles/admin-groups-tab';

/** 功能群組：授予功能、AI 模型與附件容量。 */
@Component({
  selector: 'nx-groups-page',
  imports: [Notice, PendingButton, FeaturePage, AccessEditorDialog, AdminGroupsTab],
  providers: [AccessControlStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
      [title]="session.featureName('access-control.groups')"
      description="功能群組授予功能、AI 模型與附件容量，再由角色加入。"
      layoutMode="data"
      [tour]="tour()"
    >
      <!-- 首次載入由表格的骨架表示；讀取失敗且沒有資料時只留提示與重試。 -->
      @if (store.error()) {
        <nx-notice [message]="store.error()"
          ><button
            notice-actions
            nxPending
            type="button"
            [pending]="store.refreshing()"
            (click)="store.load()"
          >
            {{ store.refreshing() ? '重新載入中…' : '重新載入' }}
          </button>
        </nx-notice>
      }
      @if (store.catalog() || !store.error()) {
        <nx-admin-groups-tab class="ui-table-fill" (edit)="editor.open('group', $event)" />
      }
    </nx-feature-page>
    <nx-access-editor-dialog #editor />`,
})
export class GroupsPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AccessControlStore);
  readonly tour = computed(() =>
    groupsTour({
      names: accessPageNames((id) => this.session.featureName(id)),
    }),
  );
}
