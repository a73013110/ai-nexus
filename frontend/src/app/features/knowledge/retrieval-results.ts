import { Notice } from '../../shared/ui/notice';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { KnowledgeSearchDto } from '../../core/api/schema';
import { ReaderLink } from '../../shared/browser/reader-link';
import { Icon } from '../../shared/ui/icon';
import { Card } from '../../shared/ui/card';

@Component({
  selector: 'nx-retrieval-results',
  imports: [Notice, ReaderLink, Icon, Card],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './retrieval-results.scss',
  template: `
    <p class="form-note" role="status">
      模式：{{ result().mode }} · {{ result().hits.length }} 個結果
    </p>
    <p class="form-note">
      改寫 {{ result().rewriteMs }} ms · 向量 {{ result().embedMs }} ms · 召回
      {{ result().searchMs }} ms · 重排 {{ result().rerankMs }} ms
    </p>
    <div class="knowledge-hits">
      @for (hit of result().hits; track $index) {
        <a
          nxCard
          class="knowledge-hit"
          [nxReaderLink]="hit.documentId!"
          [readerPage]="hit.pageNumber!"
        >
          <strong>{{ hit.title }}</strong>
          <span
            >第 {{ hit.pageNumber
            }}{{ (hit.endPage || 0) > (hit.pageNumber || 0) ? '–' + hit.endPage : '' }} 頁<nx-icon
              name="chevron"
          /></span>
          @if (hit.headingPath) {
            <small>{{ hit.headingPath }}</small>
          }
          <small
            >向量排名 {{ hit.vectorRank ?? '—' }} · 全文排名 {{ hit.ftsRank ?? '—' }} · RRF
            {{ score(hit.rrfScore) }} · 重排 {{ score(hit.rerankScore) }}</small
          >
          <p>{{ hit.text }}</p>
        </a>
      } @empty {
        <nx-notice tone="warning"
          >資料不足：目前沒有符合門檻的來源。請調整查詢或確認索引已完成。</nx-notice
        >
      }
    </div>
  `,
})
export class RetrievalResults {
  readonly result = input.required<KnowledgeSearchDto>();
  score(value: number | null | undefined) {
    return value == null ? '—' : value.toFixed(4);
  }
}
