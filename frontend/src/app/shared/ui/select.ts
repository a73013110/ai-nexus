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
import { positionPopover } from '../browser/popover-position';

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
      [attr.aria-haspopup]="searchable() ? 'dialog' : 'listbox'"
      [attr.aria-controls]="searchable() ? id + '-panel' : id"
      [attr.aria-activedescendant]="opened() && active() >= 0 ? id + '-' + active() : null"
      [disabled]="disabled() || !options().length"
      (click)="toggle()"
      (keydown)="key($event)"
    >
      <span [attr.title]="selected()?.label || placeholder()">{{
        selected()?.label || placeholder()
      }}</span
      ><nx-icon name="chevron" />
    </button>
    <div
      #panel
      [id]="id + '-panel'"
      popover="auto"
      class="select-panel"
      [attr.role]="searchable() ? 'dialog' : null"
      [attr.aria-label]="label()"
      (toggle)="toggled($event)"
    >
      @if (searchable()) {
        <div class="select-search">
          <nx-icon name="search" /><input
            type="search"
            role="combobox"
            aria-expanded="true"
            aria-haspopup="listbox"
            [attr.aria-controls]="id"
            [attr.aria-activedescendant]="active() >= 0 ? id + '-' + active() : null"
            [attr.aria-label]="'搜尋' + label()"
            [placeholder]="'搜尋' + label() + '…'"
            [value]="query()"
            (input)="filter($any($event.target).value)"
            (keydown)="searchKey($event)"
          />
        </div>
        <p class="select-count">{{ visibleOptions().length }} 個選項</p>
      }
      <div role="listbox" [id]="id" [attr.aria-label]="label()">
        @for (entry of visibleOptions(); track entry.option.value) {
          @let option = entry.option; @let i = entry.index;
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
              ><strong [attr.title]="option.label">{{ option.label }}</strong>
              @if (option.description) {
                <small>{{ option.description }}</small>
              }
            </span>
            @if (option.value === value()) {
              <nx-icon name="check" />
            }
          </button>
        } @empty {
          <p class="select-empty">沒有符合的選項</p>
        }
      </div>
    </div>`,
})
export class Select {
  readonly label = input.required<string>();
  readonly value = input('');
  readonly options = input.required<SelectOption[]>();
  readonly disabled = input(false);
  readonly placeholder = input('請選擇');
  readonly searchable = input(false);
  readonly query = signal('');
  readonly visibleOptions = computed(() =>
    this.options()
      .map((option, index) => ({ option, index }))
      .filter(({ option }) =>
        `${option.label} ${option.description || ''}`
          .toLocaleLowerCase()
          .includes(this.query().trim().toLocaleLowerCase()),
      ),
  );
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
    this.query.set('');
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
    positionPopover(
      this.trigger().nativeElement,
      this.panel().nativeElement,
      this.searchable() ? 360 : 240,
    );
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
      const enabled = this.visibleOptions()
        .map(({ option, index }) => (option.disabled ? -1 : index))
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
  filter(query: string) {
    this.query.set(query);
    this.active.set(this.visibleOptions().find((x) => !x.option.disabled)?.index ?? -1);
    this.position();
  }
  searchKey(event: KeyboardEvent) {
    if (['ArrowDown', 'ArrowUp', 'Home', 'End', 'Enter', 'Escape', 'Tab'].includes(event.key))
      this.key(event);
    if (event.key === 'Escape') this.trigger().nativeElement.focus();
  }
}
