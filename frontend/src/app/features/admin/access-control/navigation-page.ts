import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { Notice } from '@shared/ui/notice';
import { AccessControlStore } from './access-control-store';
import { accessPageNames, navigationTour } from './access-control-tour';
import { NavigationStore } from './navigation-store';
import { NavigationEditor } from './navigation/navigation-editor';
import { NavigationItemDialog } from './navigation/navigation-item-dialog';

/** 選單：側欄的分群、頁面與外部連結，以及它們的順序與層級。 */
@Component({
  selector: 'nx-navigation-page',
  imports: [Notice, FeaturePage, NavigationEditor, NavigationItemDialog],
  providers: [AccessControlStore, NavigationStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
      [title]="session.featureName('access-control.navigation')"
      description="安排側欄的分群、頁面與外部連結；每個人只看到自己有權限的項目。"
      layoutMode="data"
      [tour]="tour()"
    >
      @if (store.loading() && !store.ready()) {
        <p role="status">正在載入選單…</p>
      } @else if (store.error()) {
        <nx-notice [message]="store.error()"
          ><button notice-actions (click)="store.load()">重新載入</button>
        </nx-notice>
      }
      @if (store.ready()) {
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
