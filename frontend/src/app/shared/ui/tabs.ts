import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
} from '@angular/core';
import { CountBadge } from './count-badge';
import { SlidingIndicator } from './sliding-indicator';

export interface TabItem {
  value: string;
  label: string;
  /** A neutral CountBadge after the label; the tab's name carries the number. */
  count?: number;
  /** The tab holds unsaved changes: a dot after the label, and the tab's name says so. */
  dirty?: boolean;
}
let sequence = 0;

/** Tabs for linked tabpanels; shares ViewSwitch's sliding underline. */
@Component({
  selector: 'nx-tabs',
  imports: [CountBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  hostDirectives: [SlidingIndicator],
  host: { role: 'tablist', '[attr.aria-label]': 'label()' },
  styles: `
    /* Like ViewSwitch: min-height keeps a sideways scroller from being squeezed in a column, and
       the underline sits on the bottom edge whatever padding the host is given. */
    :host {
      position: relative;
      isolation: isolate;
      display: flex;
      gap: var(--p-space-1);
      min-height: min-content;
      padding-inline: var(--p-space-3);
      border-bottom: 1px solid var(--line);
      overflow-x: auto;
      scrollbar-width: none;
    }
    :host::before {
      content: '';
      position: absolute;
      z-index: -1;
      bottom: 0;
      left: 0;
      width: var(--indicator-w, 0);
      height: 2px;
      border-radius: var(--p-radius-pill);
      background: var(--accent);
      opacity: 0;
      pointer-events: none;
      transform: translateX(var(--indicator-x, 0));
    }
    :host([data-indicator])::before {
      opacity: 1;
    }
    :host([data-indicator-ready])::before {
      transition:
        transform var(--motion-morph) var(--ease-spring),
        width var(--motion-morph) var(--ease-spring);
    }
    button {
      display: inline-flex;
      align-items: center;
      gap: var(--p-space-2);
      flex: none;
      min-height: var(--button-target);
      padding: var(--p-space-2) var(--tab-padding-inline, var(--p-space-3));
      border-radius: var(--p-radius-md);
      color: var(--secondary);
      font-size: var(--p-font-sm);
      white-space: nowrap;
    }
    button:hover {
      color: var(--ink);
    }
    button[aria-selected='true'],
    button[aria-selected='true']:hover {
      background: transparent;
      color: var(--accent);
      font-weight: 600;
    }
    /* The save bar's unsaved colour (dialog-save-bar.scss), so the two read as one signal. */
    .tab-dirty {
      width: 0.375rem;
      height: 0.375rem;
      border-radius: var(--p-radius-pill);
      background: var(--signal);
    }
  `,
  template: `@for (item of items(); track item.value) {
    <button
      type="button"
      role="tab"
      [id]="tabId(item.value)"
      [attr.aria-controls]="panelId(item.value)"
      [attr.aria-label]="accessibleName(item)"
      [attr.aria-selected]="value() === item.value"
      [tabIndex]="value() === item.value ? 0 : -1"
      (click)="valueChange.emit(item.value)"
      (keydown)="key($event, $index)"
    >
      {{ item.label }}
      @if (item.count) {
        <nx-count-badge tone="neutral" [count]="item.count" />
      }
      @if (item.dirty) {
        <span class="tab-dirty" aria-hidden="true"></span>
      }
    </button>
  }`,
})
export class Tabs {
  readonly label = input.required<string>();
  readonly items = input.required<readonly TabItem[]>();
  readonly value = input.required<string>();
  readonly valueChange = output<string>();
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly id = `nx-tabs-${++sequence}`;
  /** Only when the visible label is not the whole name: a count, or unsaved changes. */
  accessibleName(item: TabItem) {
    if (!item.count && !item.dirty) return null;
    return (
      item.label +
      (item.count ? '，' + item.count + ' 則' : '') +
      (item.dirty ? '（有未儲存的變更）' : '')
    );
  }
  tabId(value: string) {
    return `${this.id}-tab-${value}`;
  }
  panelId(value: string) {
    return `${this.id}-panel-${value}`;
  }
  key(event: KeyboardEvent, index: number) {
    const length = this.items().length;
    let next: number;
    switch (event.key) {
      case 'ArrowRight':
        next = (index + 1) % length;
        break;
      case 'ArrowLeft':
        next = (index - 1 + length) % length;
        break;
      case 'Home':
        next = 0;
        break;
      case 'End':
        next = length - 1;
        break;
      default:
        return;
    }
    event.preventDefault();
    this.valueChange.emit(this.items()[next].value);
    this.element.nativeElement.querySelectorAll<HTMLButtonElement>('button')[next]?.focus();
  }
}
