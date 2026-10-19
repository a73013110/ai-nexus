import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { currentUrl } from './current-url';
import { catalogModule, featureForUrl, modulePages } from '../api/catalog';
import { WorkspaceSession } from '../auth/workspace-session';
import { ViewLinks, type ViewLink } from '@shared/ui/view-links';

/**
 * 同一模組各頁之間的連結列（例：存取控制的角色、功能群組、功能與選單），由目錄依目前網址推得，
 * 只列帳號有授權的頁面；少於兩頁時不顯示。放在頁面內容最上方。
 */
@Component({
  selector: 'nx-module-navigation',
  imports: [ViewLinks],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: contents; }',
  template: `@if (links().length > 1) {
    <nx-view-links [label]="label()" [options]="links()" [value]="current()" />
  }`,
})
export class ModuleNavigation {
  private readonly session = inject(WorkspaceSession);
  private readonly url = currentUrl();
  private readonly feature = computed(() => featureForUrl(this.url()));
  readonly current = computed(() => this.feature()?.id ?? '');
  readonly label = computed(() => {
    const module = catalogModule(this.feature()?.module ?? '');
    return module ? `${module.name}頁面` : '模組頁面';
  });
  readonly links = computed<ViewLink[]>(() => {
    const module = this.feature()?.module;
    if (!module) return [];
    return modulePages(module)
      .filter((page) => this.session.has(page.id))
      .map((page) => ({
        value: page.id,
        label: this.session.featureName(page.id),
        link: page.route,
      }));
  });
}
