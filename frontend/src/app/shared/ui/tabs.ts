import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
} from '@angular/core';

export interface TabItem {
  value: string;
  label: string;
}
let sequence = 0;

@Component({
  selector: 'nx-tabs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'tablist', '[attr.aria-label]': 'label()' },
  styles: `
    :host {
      display: flex;
      gap: 0.25rem;
      padding-inline: 0.75rem;
      border-bottom: 1px solid var(--line);
      overflow-x: auto;
    }
    button {
      flex: none;
      min-height: var(--button-target);
      padding: 0.5rem 0.75rem;
      color: var(--secondary);
      font-size: var(--p-font-sm);
      border-bottom: 2px solid transparent;
    }
    button[aria-selected='true'] {
      color: var(--accent);
      border-bottom-color: var(--accent);
      font-weight: 600;
    }
  `,
  template: `@for (item of items(); track item.value) {
    <button
      type="button"
      role="tab"
      [id]="tabId(item.value)"
      [attr.aria-controls]="panelId(item.value)"
      [attr.aria-selected]="value() === item.value"
      [tabIndex]="value() === item.value ? 0 : -1"
      (click)="valueChange.emit(item.value)"
      (keydown)="key($event, $index)"
    >
      {{ item.label }}
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
