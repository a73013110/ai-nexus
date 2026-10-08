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

/** A non-modal information panel in the native top layer, shared by compact metrics. */
@Component({
  selector: 'nx-info-popover',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<button
      #trigger
      type="button"
      class="info-popover-trigger"
      [attr.aria-label]="description() || label()"
      aria-haspopup="dialog"
      [attr.aria-expanded]="opened()"
      [attr.aria-controls]="id"
      (click)="toggle()"
    >
      <nx-icon [name]="icon()" /><span>{{ label() }}</span>
    </button>
    <div
      #panel
      class="info-popover-panel ui-density-compact"
      [id]="id"
      role="dialog"
      popover="auto"
      tabindex="-1"
      [attr.aria-label]="description() || label()"
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
    .info-popover-panel {
      position: fixed;
      inset: auto;
      margin: 0;
      padding: var(--section-padding);
      border: 1px solid var(--line);
      border-radius: var(--p-radius-lg);
      background: var(--surface);
      color: var(--secondary);
      box-shadow: var(--shadow-floating);
      overflow: auto;
      overscroll-behavior: contain;
      text-align: left;
      font-size: var(--text-caption);
      outline: none;
    }
    .info-popover-panel:popover-open {
      animation: panel-arrive var(--motion-fast) var(--ease);
    }
  `,
})
export class InfoPopover {
  readonly label = input.required<string>();
  readonly description = input('');
  readonly icon = input('info');
  readonly align = input<'left' | 'right'>('left');
  readonly opened = signal(false);
  readonly id = 'nx-info-' + ++sequence;
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
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
    this.trigger().nativeElement.focus();
  }
  private position() {
    positionPopover(this.trigger().nativeElement, this.panel().nativeElement, 320, this.align());
  }
}
