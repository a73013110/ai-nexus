import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';
import { positionPopover } from '../browser/popover-position';
let sequence = 0;

/**
 * A non-modal information panel in the native top layer, shared by compact metrics. The trigger
 * defaults to an icon and label; project `[popover-trigger]` content for a custom glyph (e.g. the
 * context ring). Being in the top layer, the panel is never clipped by a scrolling parent. With an
 * `anchor` there is no trigger: another control (e.g. an overflow menu item) calls `toggle()`.
 */
@Component({
  selector: 'nx-info-popover',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (!anchor()) {
      <button
        #trigger
        type="button"
        class="info-popover-trigger"
        [attr.aria-label]="description() || label()"
        [attr.title]="hint() || null"
        aria-haspopup="dialog"
        [attr.aria-expanded]="opened()"
        [attr.aria-controls]="id"
        (click)="toggle()"
      >
        <ng-content select="[popover-trigger]"
          ><nx-icon [name]="icon()" /><span>{{ label() }}</span></ng-content
        >
      </button>
    }
    <div
      #panel
      class="popover-panel info-popover-panel ui-density-compact"
      [id]="id"
      role="dialog"
      popover="auto"
      tabindex="-1"
      [attr.aria-label]="panelLabel() || description() || label()"
      (toggle)="opened.set($any($event).newState === 'open')"
      (keydown.escape)="close($event)"
    >
      <ng-content />
    </div>`,
  styles: `
    :host {
      display: inline-block;
      min-width: 0;
    }
    .info-popover-trigger {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      min-height: var(--button-target);
      padding: var(--info-popover-trigger-padding, 0);
      white-space: nowrap;
      font-size: var(--text-caption);
      color: var(--secondary);
      border-radius: var(--p-radius-md);
    }
    .info-popover-trigger nx-icon {
      width: 16px;
      height: 16px;
    }
    .info-popover-trigger:hover {
      color: var(--signal);
    }
    .info-popover-trigger[aria-expanded='true'] {
      background: var(--hover);
      color: var(--ink);
    }
    .info-popover-panel {
      padding: var(--section-padding);
      color: var(--secondary);
      overflow: auto;
      text-align: left;
      font-size: var(--text-caption);
      outline: none;
    }
  `,
})
export class InfoPopover {
  readonly label = input.required<string>();
  readonly description = input('');
  readonly icon = input('info');
  readonly align = input<'left' | 'right'>('left');
  /** Name of the panel when it differs from the trigger's (which may carry a live value). */
  readonly panelLabel = input('');
  readonly hint = input('');
  readonly width = input(320);
  /** Positions the panel against this element instead of rendering a trigger. */
  readonly anchor = input<HTMLElement | null>(null);
  readonly opened = signal(false);
  readonly id = 'nx-info-' + ++sequence;
  private readonly trigger = viewChild<ElementRef<HTMLButtonElement>>('trigger');
  /** Where focus returns: the trigger, or whatever opened an anchored panel. */
  private opener: HTMLElement | null = null;
  private readonly panel = viewChild.required<ElementRef<HTMLDivElement>>('panel');
  constructor() {
    const position = () => {
      if (this.opened()) this.position();
    };
    window.addEventListener('resize', position);
    window.addEventListener('scroll', position, true);
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('resize', position);
      window.removeEventListener('scroll', position, true);
    });
  }
  toggle() {
    if (this.opened()) this.close();
    else {
      this.opener = this.trigger()?.nativeElement ?? (document.activeElement as HTMLElement | null);
      this.panel().nativeElement.showPopover();
      this.opened.set(true);
      this.position();
      this.panel().nativeElement.focus();
    }
  }
  close(event?: Event) {
    event?.stopPropagation();
    this.panel().nativeElement.hidePopover();
    this.opened.set(false);
    this.opener?.focus();
  }
  private position() {
    positionPopover(
      this.anchor() ?? this.trigger()!.nativeElement,
      this.panel().nativeElement,
      this.width(),
      this.align(),
      420,
    );
  }
}
