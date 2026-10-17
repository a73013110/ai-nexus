import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { WorkspaceSession } from '../auth/workspace-session';
import { Notice } from '@shared/ui/notice';

/**
 * 帳號缺少所需功能時，頁面向其顯示的內容，每個頁面都相同。API 會以 `feature_forbidden` 回應這類請求，
 * 但不會指出是哪個功能；
 * 由頁面自己指出。
 */
@Component({
  selector: 'nx-feature-access-notice',
  imports: [Notice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: contents; }',
  template: `<nx-notice tone="danger" icon="lock" [message]="message()" />`,
})
export class FeatureAccessNotice {
  private readonly session = inject(WorkspaceSession);
  /** 頁面需要的功能 ID，即 `/me` 中授予的那個。 */
  readonly feature = input.required<string>();
  readonly message = computed(
    () => `需要「${this.session.featureName(this.feature())}」權限，請聯絡管理員。`,
  );
}
