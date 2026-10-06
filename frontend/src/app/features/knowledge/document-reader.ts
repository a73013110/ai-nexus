import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { combineLatest, map } from 'rxjs';
import { WorkspaceSidebar } from '../../shared/ui/workspace-sidebar';
import { Icon } from '../../shared/ui/icon';
import {
  ReaderNavigation,
  readerReturnLabel,
  readerReturnUrl,
} from '../../shared/browser/reader-navigation';
import { DocumentViewer } from './document-viewer';
import { WorkspaceLayout } from '../../core/preferences/workspace-layout';

@Component({
  selector: 'nx-document-reader',
  imports: [WorkspaceSidebar, Icon, DocumentViewer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="feature-layout">
    <aside nxWorkspaceSidebar class="feature-sidebar" aria-label="工作區導覽"></aside>
    <main
      class="reader-page"
      id="feature-content"
      tabindex="-1"
      [attr.inert]="layout.overlay() ? '' : null"
    >
      <a class="reader-return quiet-button" [href]="returnTo()" (click)="back($event)"
        ><nx-icon name="back" />{{ returnLabel() }}</a
      >
      <nx-document-viewer [target]="state().target" />
    </main>
  </div>`,
})
export class DocumentReader {
  readonly layout = inject(WorkspaceLayout);
  private readonly route = inject(ActivatedRoute);
  private readonly navigation = inject(ReaderNavigation);
  readonly state = toSignal(
    combineLatest([this.route.paramMap, this.route.queryParamMap]).pipe(
      map(([params, query]) => ({
        target: {
          id: params.get('id')!,
          shareId: params.get('shareId'),
          attachment: this.route.snapshot.routeConfig?.path?.includes('attachment') ?? false,
          page: Number(query.get('page')) || 1,
        },
        returnTo: readerReturnUrl(query.get('returnTo')),
      })),
    ),
    { requireSync: true },
  );
  readonly returnTo = computed(() => this.state().returnTo ?? '/files');
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
