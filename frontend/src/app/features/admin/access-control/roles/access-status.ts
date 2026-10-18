import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { StatusBadge } from '@shared/ui/status-badge';

/** 使用者、角色、群組或功能是否開啟；停用的列會以警告標示。 */
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
