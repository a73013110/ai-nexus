import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import { routeOf } from '@core/api/catalog';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { PendingButton } from '@shared/ui/pending-button';
import { OrganizationApi } from './organization-api';

/**
 * 使用者所屬的組織單位（唯讀），給使用者管理的檢視器用；調整所屬要到組織頁。
 * 讀取以使用者 ID 為鍵，切換使用者時不會短暫顯示上一位的單位。
 */
@Component({
  selector: 'nx-user-units',
  imports: [PendingButton, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="resource-badges" aria-label="所屬單位">
    @for (unit of units(); track unit.unitId) {
      <span [class.is-muted]="!unit.enabled" [title]="unit.path.join(' › ')"
        >{{ unit.path.at(-1) }}{{ unit.isPrimary ? '（主要）' : ''
        }}{{ unit.isManager ? ' · 主管' : '' }}{{ unit.enabled ? '' : ' · 停用' }}</span
      >
    } @empty {
      @if (loaded()) {
        <small class="form-note">尚未分派單位</small>
      } @else if (error()) {
        <small class="form-note">{{ error() }}</small>
        <button
          nxPending
          type="button"
          class="quiet-button"
          [pending]="refreshing()"
          (click)="reload()"
        >
          {{ refreshing() ? '重新載入中…' : '重新載入' }}
        </button>
      } @else {
        <span class="skeleton unit-skeleton" role="status"
          ><span class="sr-only">正在載入所屬單位…</span></span
        >
      }
    }
    @if (session.has('organization')) {
      <a class="quiet-button" [routerLink]="route">組織</a>
    }
  </div>`,
  styles: `
    .is-muted {
      color: var(--secondary);
    }
    .unit-skeleton {
      width: 6rem;
      height: 1.25rem;
    }
  `,
})
export class UserUnits {
  readonly userId = input.required<string>();
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(OrganizationApi);
  readonly route = routeOf('organization');
  private readonly read = apiResource({
    feature: ['organization', 'identity.users'],
    params: () => this.userId(),
    loader: async (id) => ({ id, units: await this.api.userUnits(id) }),
  });
  readonly units = computed(() => {
    const loaded = this.read.value();
    return loaded && loaded.id === this.userId() ? loaded.units : [];
  });
  readonly loaded = computed(() => this.read.value()?.id === this.userId());
  readonly error = this.read.error;
  readonly refreshing = this.read.refreshing;
  reload() {
    this.read.reload();
  }
}
