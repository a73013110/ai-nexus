import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { WorkspaceSession } from '../auth/workspace-session';
import { Notice } from '../../shared/ui/notice';

/**
 * What a page shows an account without the feature it needs, the same on every page. The API
 * answers such a request with `feature_forbidden`, which does not name the feature; the page does.
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
  /** The feature ID the page needs, as granted in `/me`. */
  readonly feature = input.required<string>();
  readonly message = computed(
    () => `需要「${this.session.featureName(this.feature())}」權限，請聯絡管理員。`,
  );
}
