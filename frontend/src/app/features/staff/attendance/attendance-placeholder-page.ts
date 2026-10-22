import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';

/**
 * 佔位頁：出勤的四個頁面（紀錄、卡號對應、匯入紀錄、假日設定）共用，之後各自換成真頁面。
 * 標題取自路由的 `data.feature`，不寫死名稱。
 */
@Component({
  selector: 'nx-attendance-placeholder-page',
  imports: [FeaturePage, EmptyState, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page [title]="title">
    <nx-empty-state>
      <nx-icon name="clock" /><strong>建置中</strong>
      <p>這個頁面還在開發，完成後會在這裡提供。</p>
    </nx-empty-state>
  </nx-feature-page>`,
})
export class AttendancePlaceholderPage {
  protected readonly title = inject(WorkspaceSession).featureName(
    String(inject(ActivatedRoute).snapshot.data['feature']),
  );
}
