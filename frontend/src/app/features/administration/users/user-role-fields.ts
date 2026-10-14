import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import type { AdminCatalogDto } from '@core/api/schema';
import { ChoiceGroups, type ChoiceGroup } from '@shared/ui/choice-groups';
import { Notice } from '@shared/ui/notice';
import { AdministrationStore } from '../administration-store';
import { FeatureSummary } from '../roles/feature-summary';

/** What a user would be able to open: enabled features of enabled groups of enabled roles. */
export function userFeatures(catalog: AdminCatalogDto | null, roleIds: string[], enabled: boolean) {
  if (!catalog || !enabled) return [];
  const groups = new Set(
    catalog.roles.filter((x) => x.enabled && roleIds.includes(x.id)).flatMap((x) => x.groupIds),
  );
  const features = new Set(
    catalog.groups.filter((x) => x.enabled && groups.has(x.id)).flatMap((x) => x.featureIds),
  );
  return catalog.features.filter((x) => x.enabled && features.has(x.id));
}

export function sameIds(a: readonly string[], b: readonly string[]) {
  return a.length === b.length && a.every((id) => b.includes(id));
}

let sequence = 0;

/**
 * Role assignment and a preview of the features it grants, as two form sections; shared by the
 * create dialog and the inspector, which wrap them in `.form-sections`.
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
  readonly store = inject(AdministrationStore);
  readonly roleIds = input.required<string[]>();
  /** A disabled user keeps their roles but can open nothing. */
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
