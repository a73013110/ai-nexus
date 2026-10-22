import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';

/**
 * 出勤四個頁面共用的佔位頁，之後各自換成真頁面。標題取路由 `data.feature` 的功能名稱，
 * 在範本裡讀，管理員改了名稱覆寫時跟著更新。
 */
@Component({
  selector: 'nx-attendance-placeholder-page',
  imports: [FeaturePage, EmptyState, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page [title]="session.featureName(feature)">
    <nx-empty-state>
      <nx-icon name="clock" /><strong>建置中</strong>
      <p>這個頁面還在開發，完成後會在這裡提供。</p>
    </nx-empty-state>
  </nx-feature-page>`,
})
export class AttendancePlaceholderPage {
  protected readonly session = inject(WorkspaceSession);
  protected readonly feature = String(inject(ActivatedRoute).snapshot.data['feature']);
}
