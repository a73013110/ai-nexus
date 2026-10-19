import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { routeOf, type RoutedId } from '@core/api/catalog';
import { toSignal } from '@angular/core/rxjs-interop';
import { combineLatest, map } from 'rxjs';
import { Icon } from '@shared/ui/icon';
import {
  ReaderNavigation,
  readerReturnLabel,
  readerReturnUrl,
} from '@shared/browser/reader-navigation';
import { DocumentViewer } from './document-viewer';
import { WorkspaceMenuButton } from '@core/layout/workspace-menu-button';

type ReaderId = Extract<RoutedId, `${string}.reader`>;
const READER_HOME: Record<ReaderId, RoutedId> = {
  'files.reader': 'files',
  'knowledge.reader': 'knowledge',
  'sharing.reader': 'sharing',
};
function readerId(value: unknown): ReaderId {
  return typeof value === 'string' && Object.hasOwn(READER_HOME, value)
    ? (value as ReaderId)
    : 'knowledge.reader';
}

@Component({
  selector: 'nx-document-reader',
  imports: [WorkspaceMenuButton, Icon, DocumentViewer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './document-reader.scss',
  template: `<main class="reader-page ui-density-compact" id="feature-content" tabindex="-1">
    <div class="reader-navigation">
      <nx-workspace-menu-button />
      <a class="reader-return quiet-button" [href]="returnTo()" (click)="back($event)"
        ><nx-icon name="back" />{{ returnLabel() }}</a
      >
    </div>
    <nx-document-viewer [target]="state().target" />
  </main>`,
})
export class DocumentReader {
  private readonly route = inject(ActivatedRoute);
  private readonly reader = readerId(this.route.snapshot.data['feature']);
  private readonly navigation = inject(ReaderNavigation);
  readonly state = toSignal(
    combineLatest([this.route.paramMap, this.route.queryParamMap]).pipe(
      map(([params, query]) => ({
        target: {
          id: params.get('id')!,
          shareId: params.get('shareId'),
          // 三個閱讀器共用此元件；是哪一個由路由的目錄 ID 決定，不看網址字面。
          attachment: this.reader === 'files.reader',
          page: Number(query.get('page')) || 1,
        },
        returnTo: readerReturnUrl(query.get('returnTo')),
      })),
    ),
    { requireSync: true },
  );
  /** 沒有可信的來源時，回到閱讀器所屬模組的頁面。 */
  readonly returnTo = computed(() => this.state().returnTo ?? routeOf(READER_HOME[this.reader]));
  readonly returnLabel = computed(() => readerReturnLabel(this.returnTo()));
  constructor() {
    effect(() => this.navigation.enter(this.state().returnTo));
  }
  back(event: MouseEvent) {
    if (event.button || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    this.navigation.back(this.returnTo());
  }
}
