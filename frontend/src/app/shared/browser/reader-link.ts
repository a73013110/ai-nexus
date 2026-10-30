import { Directive, ElementRef, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';
import {
  ReaderOverlay,
  originalOnlyHref,
  readerCommands,
  type FileScope,
  type RemoteReaderFile,
} from './reader-overlay';

/**
 * 原生連結支援開新分頁；同分頁導覽也會保留閱讀位置。
 * 遠端檔案（`readerRemote`）與稽核檢視中的對話附件沒有閱讀器網址：連結指向內容本身，
 * 開新分頁就是瀏覽器直接顯示原檔。
 */
@Directive({
  selector: 'a[nxReaderLink]',
  host: { '[attr.href]': 'href()', '[attr.data-reader-id]': 'id()', '(click)': 'open($event)' },
})
export class ReaderLink {
  private readonly router = inject(Router);
  private readonly overlay = inject(ReaderOverlay);
  private readonly element = inject<ElementRef<HTMLAnchorElement>>(ElementRef);
  readonly id = input.required<string>({ alias: 'nxReaderLink' });
  readonly attachment = input(false, { alias: 'readerAttachment' });
  readonly page = input<number | null>(null, { alias: 'readerPage' });
  readonly scope = input<FileScope | null>(null, { alias: 'readerScope' });
  readonly remote = input<RemoteReaderFile | null>(null, { alias: 'readerRemote' });
  private readonly origin = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );
  readonly href = computed(() => {
    const original = originalOnlyHref({
      id: this.id(),
      scope: this.scope(),
      remote: this.remote(),
    });
    if (original) return original;
    return this.router.serializeUrl(
      this.router.createUrlTree(
        readerCommands({ id: this.id(), attachment: this.attachment(), scope: this.scope() }),
        { queryParams: { page: this.page(), returnTo: this.origin() } },
      ),
    );
  });

  open(event: MouseEvent) {
    if (
      event.defaultPrevented ||
      event.button !== 0 ||
      event.ctrlKey ||
      event.metaKey ||
      event.shiftKey ||
      event.altKey ||
      (this.element.nativeElement.target && this.element.nativeElement.target !== '_self')
    )
      return;
    event.preventDefault();
    this.overlay.open({
      id: this.id(),
      attachment: this.attachment(),
      page: this.page(),
      scope: this.scope(),
      remote: this.remote(),
    });
  }
}
