import { Directive, ElementRef, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';
import { ReaderNavigation } from './reader-navigation';

/** Native links support new tabs; same-tab navigation also retains the reading position. */
@Directive({
  selector: 'a[nxReaderLink]',
  host: { '[attr.href]': 'href()', '[attr.data-reader-id]': 'id()', '(click)': 'open($event)' },
})
export class ReaderLink {
  private readonly router = inject(Router);
  private readonly navigation = inject(ReaderNavigation);
  private readonly element = inject<ElementRef<HTMLAnchorElement>>(ElementRef);
  readonly id = input.required<string>({ alias: 'nxReaderLink' });
  readonly attachment = input(false, { alias: 'readerAttachment' });
  readonly page = input<number | null>(null, { alias: 'readerPage' });
  private readonly origin = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );
  readonly href = computed(() =>
    this.router.serializeUrl(
      this.router.createUrlTree(
        this.attachment() ? ['/reader/attachment', this.id()] : ['/reader', this.id()],
        { queryParams: { page: this.page(), returnTo: this.origin() } },
      ),
    ),
  );

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
    void this.router.navigateByUrl(this.href(), {
      state: { readerOrigin: this.navigation.capture(this.id(), this.element.nativeElement) },
    });
  }
}
