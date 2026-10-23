import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { AccessControlStore } from './access-control-store';
import { accessPageNames, navigationTour } from './access-control-tour';
import { NavigationStore } from './navigation-store';
import { NavigationEditor } from './navigation/navigation-editor';
import { NavigationItemDialog } from './navigation/navigation-item-dialog';

/** 選單：側欄的分群、頁面與外部連結，以及它們的順序與層級。 */
@Component({
  selector: 'nx-navigation-page',
  imports: [Notice, PendingButton, FeaturePage, NavigationEditor, NavigationItemDialog],
  providers: [AccessControlStore, NavigationStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
      [title]="session.featureName('access-control.navigation')"
      description="安排側欄的分群、頁面與外部連結；每個人只看到自己有權限的項目。"
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
      @if (store.ready() || !store.error()) {
        <nx-navigation-editor
          class="ui-table-fill"
          (create)="dialog.create($event.kind, $event.parentId)"
          (edit)="dialog.edit($event)"
        />
      }
    </nx-feature-page>
    <nx-navigation-item-dialog #dialog />`,
})
export class NavigationPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(NavigationStore);
  readonly tour = computed(() =>
    navigationTour({
      names: accessPageNames((id) => this.session.featureName(id)),
      testing: this.session.testing(),
    }),
  );
}
