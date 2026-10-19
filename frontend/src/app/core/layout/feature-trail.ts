import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { currentUrl } from './current-url';
import { catalogArea, catalogModule, featureForUrl } from '../api/catalog';
import { WorkspaceSession } from '../auth/workspace-session';

/**
 * 目前網址在目錄中的位置：區、模組、頁面。單頁模組（ID 就是模組名）或模組名與頁面名相同時不重複模組；
 * 頁面名用 `featureName`，管理員的覆寫名稱優先。不在目錄中的網址為空陣列。
 */
export function featureTrail(url: string, name: (id: string) => string): string[] {
  const feature = featureForUrl(url);
  const module = feature && catalogModule(feature.module);
  const area = module && catalogArea(module.area);
  if (!feature || !module || !area) return [];
  const page = name(feature.id);
  return feature.id === module.id || module.name === page
    ? [area.name, page]
    : [area.name, module.name, page];
}

/** `FeaturePage` 標題上方的麵包屑；只是位置提示，不是導覽，所以不放連結。 */
@Component({
  selector: 'nx-feature-trail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      flex: 1 0 100%;
      min-width: 0;
    }
    ol {
      display: flex;
      gap: var(--p-space-1);
      margin: 0;
      padding: 0;
      overflow: hidden;
      list-style: none;
      color: var(--muted);
      font-size: var(--p-font-xs);
      line-height: 1.4;
      white-space: nowrap;
    }
    li + li::before {
      /* 分隔符號只是裝飾，替代文字留空，報讀器只念各層名稱。 */
      content: '›' / '';
      margin-inline-end: var(--p-space-1);
    }
    li:last-child {
      overflow: hidden;
      text-overflow: ellipsis;
    }
  `,
  template: `@if (trail().length) {
    <nav aria-label="目前位置">
      <ol>
        @for (item of trail(); track $index) {
          <li [attr.aria-current]="$last ? 'page' : null">{{ item }}</li>
        }
      </ol>
    </nav>
  }`,
})
export class FeatureTrail {
  private readonly session = inject(WorkspaceSession);
  private readonly url = currentUrl();
  readonly trail = computed(() => {
    this.session.me();
    return featureTrail(this.url(), (id) => this.session.featureName(id));
  });
}
