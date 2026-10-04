import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';

export interface SelectOption {
  value: string;
  label: string;
  description?: string;
  disabled?: boolean;
}
let sequence = 0;

/** Single-choice listbox. The native top layer prevents clipping inside dialogs and scroll panels. */
@Component({
  selector: 'nx-select',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<button
      #trigger
      type="button"
      class="select-trigger"
      role="combobox"
      [attr.aria-label]="label()"
      [attr.aria-expanded]="opened()"
      aria-haspopup="listbox"
      [attr.aria-controls]="id"
      [attr.aria-activedescendant]="opened() && active() >= 0 ? id + '-' + active() : null"
      [disabled]="disabled() || !options().length"
      (click)="toggle()"
      (keydown)="key($event)"
    >
      <span>{{ selected()?.label || placeholder() }}</span
      ><nx-icon name="chevron" />
    </button>
    <div
      #panel
      [id]="id"
      popover="auto"
      class="select-panel"
      role="listbox"
      [attr.aria-label]="label()"
      (toggle)="toggled($event)"
    >
      @for (option of options(); track option.value; let i = $index) {
        <button
          type="button"
          role="option"
          tabindex="-1"
          class="select-option"
          [id]="id + '-' + i"
          [attr.aria-selected]="option.value === value()"
          [attr.aria-disabled]="!!option.disabled"
          [disabled]="option.disabled"
          [class.is-active]="active() === i"
          (pointermove)="active.set(i)"
          (click)="choose(i)"
        >
          <span
            ><strong>{{ option.label }}</strong>
            @if (option.description) {
              <small>{{ option.description }}</small>
            }
          </span>
          @if (option.value === value()) {
            <nx-icon name="check" />
          }
        </button>
      }
    </div>`,
})
export class Select {
  readonly label = input.required<string>();
  readonly value = input('');
  readonly options = input.required<SelectOption[]>();
  readonly disabled = input(false);
  readonly placeholder = input('請選擇');
  readonly valueChange = output<string>();
  readonly selected = computed(() => this.options().find((x) => x.value === this.value()));
  readonly opened = signal(false);
  readonly active = signal(-1);
  readonly id = `nx-select-${++sequence}`;
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly panel = viewChild.required<ElementRef<HTMLDivElement>>('panel');
  private search = '';
  private searchTimer?: ReturnType<typeof setTimeout>;
  constructor() {
    const reposition = () => {
      if (this.opened()) this.position();
    };
    window.addEventListener('resize', reposition);
    window.addEventListener('scroll', reposition, true);
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.searchTimer);
      window.removeEventListener('resize', reposition);
      window.removeEventListener('scroll', reposition, true);
    });
  }
  toggle() {
    this.opened() ? this.close() : this.open();
  }
  private open() {
    if (this.disabled() || !this.options().length) return;
    this.active.set(
      Math.max(
        0,
        this.options().findIndex((x) => x.value === this.value()),
      ),
    );
    this.panel().nativeElement.showPopover();
    this.opened.set(true);
    this.position();
    this.reveal();
  }
  private position() {
    const anchor = this.trigger().nativeElement.getBoundingClientRect(),
      panel = this.panel().nativeElement;
    const width = Math.min(Math.max(anchor.width, 220), innerWidth - 24);
    panel.style.width = `${width}px`;
    panel.style.maxHeight = `${Math.max(120, Math.min(320, innerHeight - 32))}px`;
    const height = panel.getBoundingClientRect().height;
    panel.style.left = `${Math.max(12, Math.min(anchor.left, innerWidth - width - 12))}px`;
    panel.style.top = `${Math.max(12, anchor.bottom + height + 8 < innerHeight ? anchor.bottom + 6 : anchor.top - height - 6)}px`;
  }
  close() {
    this.panel().nativeElement.hidePopover();
    this.opened.set(false);
  }
  toggled(event: Event) {
    this.opened.set((event as ToggleEvent).newState === 'open');
  }
  choose(index: number) {
    const option = this.options()[index];
    if (!option || option.disabled) return;
    this.valueChange.emit(option.value);
    this.close();
    this.trigger().nativeElement.focus();
  }
  key(event: KeyboardEvent) {
    if (event.isComposing) return;
    const key = event.key;
    if (key === 'Tab') {
      this.close();
      return;
    }
    if (key === 'Escape' && this.opened()) {
      event.preventDefault();
      event.stopPropagation();
      this.close();
      return;
    }
    if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(key)) {
      event.preventDefault();
      if (!this.opened()) this.open();
      const enabled = this.options()
        .map((x, i) => (x.disabled ? -1 : i))
        .filter((x) => x >= 0);
      if (!enabled.length) return;
      const old = enabled.indexOf(this.active());
      this.active.set(
        key === 'Home'
          ? enabled[0]
          : key === 'End'
            ? enabled.at(-1)!
            : enabled[(old + (key === 'ArrowDown' ? 1 : -1) + enabled.length) % enabled.length],
      );
      this.reveal();
      return;
    }
    if ((key === 'Enter' || key === ' ') && this.opened()) {
      event.preventDefault();
      this.choose(this.active());
      return;
    }
    if (key.length === 1 && key !== ' ' && !event.ctrlKey && !event.metaKey && !event.altKey) {
      event.preventDefault();
      if (!this.opened()) this.open();
      clearTimeout(this.searchTimer);
      this.search += key.toLocaleLowerCase();
      const index = this.options().findIndex(
        (x) => !x.disabled && x.label.toLocaleLowerCase().startsWith(this.search),
      );
      if (index >= 0) {
        this.active.set(index);
        this.reveal();
      }
      this.searchTimer = setTimeout(() => (this.search = ''), 650);
    }
  }
  private reveal() {
    requestAnimationFrame(() =>
      document.getElementById(`${this.id}-${this.active()}`)?.scrollIntoView({ block: 'nearest' }),
    );
  }
}
