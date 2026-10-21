import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { Notice } from '@shared/ui/notice';
import { AccessControlStore } from './access-control-store';
import { accessPageNames, featuresTour } from './access-control-tour';
import { AccessEditorDialog } from './roles/access-editor-dialog';
import { AdminFeaturesTab } from './roles/admin-features-tab';

/** 功能：名稱與開關；網址與圖示來自功能目錄，只顯示不編輯。選單位置在選單頁。 */
@Component({
  selector: 'nx-features-page',
  imports: [Notice, FeaturePage, AccessEditorDialog, AdminFeaturesTab],
  providers: [AccessControlStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
      [title]="session.featureName('access-control.features')"
      description="調整功能名稱與開關；停用的功能不授予任何人。"
      layoutMode="data"
      [tour]="tour()"
    >
      @if (store.loading()) {
        <p role="status">正在載入功能…</p>
      } @else if (store.error()) {
        <nx-notice [message]="store.error()"
          ><button notice-actions (click)="store.load()">重新載入</button>
        </nx-notice>
      }
      @if (store.catalog()) {
        <nx-admin-features-tab class="ui-table-fill" (edit)="editor.open('feature', $event)" />
      }
    </nx-feature-page>
    <nx-access-editor-dialog #editor />`,
})
export class FeaturesPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AccessControlStore);
  readonly tour = computed(() =>
    featuresTour({
      names: accessPageNames((id) => this.session.featureName(id)),
      testing: this.session.testing(),
    }),
  );
}
