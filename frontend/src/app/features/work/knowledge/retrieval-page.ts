import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { RetrievalAdmin } from './retrieval/retrieval-admin';
import { retrievalTour } from './retrieval-tour';

/** 檢索管理：嵌入設定檔的重建與啟用、檢索能力檢查與管理端試查。 */
@Component({
  selector: 'nx-retrieval-page',
  imports: [FeaturePage, RetrievalAdmin],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
    [title]="session.featureName('knowledge.retrieval')"
    description="管理知識檢索的向量索引，並檢查與試查檢索結果。"
    [tour]="tour()"
  >
    <nx-retrieval-admin />
  </nx-feature-page>`,
})
export class RetrievalPage {
  readonly session = inject(WorkspaceSession);
  readonly tour = computed(() => retrievalTour({ testing: this.session.testing() }));
}
