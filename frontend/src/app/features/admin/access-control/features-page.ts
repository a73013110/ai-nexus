import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { ModuleNavigation } from '@core/layout/module-navigation';
import { Notice } from '@shared/ui/notice';
import { AccessControlStore } from './access-control-store';
import { AccessEditorDialog } from './roles/access-editor-dialog';
import { AdminFeaturesTab } from './roles/admin-features-tab';

/** 功能與選單：名稱、開關、選單分群與順序；網址與圖示來自功能目錄，只顯示不編輯。 */
@Component({
  selector: 'nx-features-page',
  imports: [Notice, FeaturePage, ModuleNavigation, AccessEditorDialog, AdminFeaturesTab],
  providers: [AccessControlStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
      [title]="session.featureName('access-control.features')"
      description="調整功能名稱、開關，以及側欄選單的分群與順序。"
      layoutMode="data"
    >
      <nx-module-navigation />
      @if (store.loading()) {
        <p role="status">正在載入功能…</p>
      } @else if (store.error()) {
        <nx-notice [message]="store.error()"
          ><button notice-actions (click)="store.load()">重新載入</button>
        </nx-notice>
      }
      @if (store.catalog()) {
        <nx-admin-features-tab
          class="ui-table-fill"
          (edit)="editor.open('feature', $event)"
          (editGroup)="editor.open('menu', $event)"
        />
      }
    </nx-feature-page>
    <nx-access-editor-dialog #editor />`,
})
export class FeaturesPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AccessControlStore);
}
