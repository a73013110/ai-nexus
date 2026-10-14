import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { StatusBadge } from '@shared/ui/status-badge';

/** Whether a user, role, group or feature is switched on; disabled rows stand out as a warning. */
@Component({
  selector: 'nx-access-status',
  imports: [StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-status-badge [tone]="enabled() ? 'neutral' : 'warning'">{{
    enabled() ? '啟用' : '停用'
  }}</nx-status-badge>`,
})
export class AccessStatus {
  readonly enabled = input.required<boolean>();
}
