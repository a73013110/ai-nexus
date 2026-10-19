import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import type { AdminCatalogDto } from '@core/api/schema';
import { ChoiceGroups, type ChoiceGroup } from '@shared/ui/choice-groups';
import { Notice } from '@shared/ui/notice';
import { AccessControlStore } from '@features/admin/access-control/access-control-store';
import { FeatureSummary } from '@features/admin/access-control/roles/feature-summary';

/** 使用者能開啟的功能：已啟用角色中，已啟用群組裡已啟用的功能。 */
export function userFeatures(catalog: AdminCatalogDto | null, roleIds: string[], enabled: boolean) {
  if (!catalog || !enabled) return [];
  const groups = new Set(
    catalog.roles
      .filter((item) => item.enabled && roleIds.includes(item.id))
      .flatMap((item) => item.groupIds),
  );
  const features = new Set(
    catalog.groups
      .filter((item) => item.enabled && groups.has(item.id))
      .flatMap((item) => item.featureIds),
  );
  return catalog.features.filter((item) => item.enabled && features.has(item.id));
}

let sequence = 0;

/**
 * 角色指派與其授予功能的預覽，分成兩個表單區段；由建立對話框與檢視器共用，
 * 兩者會把它們包在 `.form-sections` 中。
 */
@Component({
  selector: 'nx-user-role-fields',
  imports: [ChoiceGroups, FeatureSummary, Notice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: contents;
    }
  `,
  template: `
    <section class="form-section" role="group" [attr.aria-labelledby]="id + '-roles'">
      <header class="form-section-head">
        <h3 [id]="id + '-roles'">角色</h3>
        <p class="form-note">可複選；可用功能取各角色的聯集。</p>
      </header>
      <div class="form-section-body">
        <nx-choice-groups
          label="分派角色"
          [groups]="choices()"
          [selected]="roleIds()"
          [disabled]="disabled()"
          (selectedChange)="roleIdsChange.emit($event)"
        />
      </div>
    </section>
    <section class="form-section" role="group" [attr.aria-labelledby]="id + '-preview'">
      <header class="form-section-head">
        <h3 [id]="id + '-preview'">儲存後可用功能</h3>
        <p class="form-note">依目前勾選的角色試算。</p>
      </header>
      <div class="form-section-body">
        @if (!enabled()) {
          <nx-notice tone="warning" message="使用者已停用，儲存後無法使用任何功能。" />
        }
        <nx-feature-summary [features]="features()" [groups]="store.navigationGroups()" />
      </div>
    </section>
  `,
})
export class UserRoleFields {
  readonly id = `nx-user-roles-${++sequence}`;
  readonly store = inject(AccessControlStore);
  readonly roleIds = input.required<string[]>();
  /** 停用的使用者保留角色，但無法開啟任何內容。 */
  readonly enabled = input(true);
  readonly disabled = input(false);
  readonly roleIdsChange = output<string[]>();
  /** 角色沒有分類，收成一張卡片；段落標題已是「角色」，卡片名稱說明範圍。 */
  readonly choices = computed<ChoiceGroup[]>(() => [
    {
      id: 'roles',
      name: '全部角色',
      options: (this.store.catalog()?.roles ?? []).map((role) => ({
        id: role.id,
        label: role.name + (role.enabled ? '' : '（停用）'),
        description: role.id,
      })),
    },
  ]);
  readonly features = computed(() =>
    userFeatures(this.store.catalog(), this.roleIds(), this.enabled()),
  );
}
