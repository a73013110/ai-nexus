import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  input,
  viewChild,
} from '@angular/core';
import { nexusPath } from '../graphics/nexus-logo';

@Component({
  selector: 'nx-brand-wordmark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.is-compact]': 'compact()' },
  styleUrl: './brand-wordmark.scss',
  template: `<span class="brand-wordmark" role="img" aria-label="AI Nexus">
    <span class="brand-prefix" aria-hidden="true">AI</span>
    <strong aria-hidden="true"
      ><span #mark class="brand-symbol" [style.opacity]="markVisible() ? 1 : 0">
        <svg viewBox="-1 -1 2 2" aria-hidden="true">
          <path [attr.d]="path" fill="currentColor" />
        </svg> </span
      ><span class="brand-suffix">exus</span></strong
    >
  </span>`,
})
export class BrandWordmark {
  readonly compact = input(false);
  readonly markVisible = input(true);
  readonly path = nexusPath;
  private readonly mark = viewChild<ElementRef<HTMLElement>>('mark');
  readonly markElement = computed(() => this.mark()?.nativeElement ?? null);
}
