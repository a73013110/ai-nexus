import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';
import { CompactDialog } from './compact-dialog';

export interface Command {
  id: string;
  label: string;
  detail: string;
  icon: string;
  shortcut?: string;
}
@Component({
  selector: 'nx-command-palette',
  imports: [Icon, CompactDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './command-palette.scss',
  template: `<dialog
    nxCompactDialog
    #dialog
    class="command-dialog"
    aria-labelledby="command-title"
    (close)="query.set(''); index.set(0)"
  >
    <div class="command-search">
      <nx-icon name="search" /><label class="sr-only" id="command-title" for="command-search"
        >快捷指令</label
      ><input
        #search
        id="command-search"
        type="search"
        placeholder="搜尋指令或對話…"
        autocomplete="off"
        [value]="query()"
        (input)="searchChanged($event)"
        (keydown)="keydown($event)"
        role="combobox"
        aria-expanded="true"
        aria-controls="command-results"
        [attr.aria-activedescendant]="filtered().length ? 'command-' + index() : null"
      /><button
        type="button"
        class="icon-button"
        aria-label="關閉快捷指令"
        (click)="dialog.close()"
      >
        <nx-icon name="close" />
      </button>
    </div>
    <div class="command-results" id="command-results" role="listbox" aria-label="可用指令">
      @for (command of filtered(); track command.id; let i = $index) {
        <button
          type="button"
          [id]="'command-' + i"
          role="option"
          [attr.aria-selected]="index() === i"
          (pointermove)="index.set(i)"
          (click)="choose(command.id)"
        >
          <nx-icon [name]="command.icon" /><span
            ><strong>{{ command.label }}</strong
            ><small>{{ command.detail }}</small></span
          >
          @if (command.shortcut) {
            <kbd>{{ command.shortcut }}</kbd>
          }
        </button>
      } @empty {
        <p class="panel-note">找不到符合的指令或對話。</p>
      }
    </div>
    <div class="command-footer">
      <span><kbd>↑ ↓</kbd> 選擇 <kbd>Enter</kbd> 執行</span><span><kbd>Esc</kbd> 關閉</span>
    </div>
  </dialog>`,
})
export class CommandPalette {
  readonly commands = input.required<Command[]>();
  readonly selected = output<string>();
  readonly query = signal('');
  readonly index = signal(0);
  readonly filtered = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    return this.commands()
      .filter((x) => (x.label + ' ' + x.detail).toLocaleLowerCase().includes(query))
      .slice(0, 30);
  });
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  private readonly search = viewChild<ElementRef<HTMLInputElement>>('search');
  open() {
    this.dialog()?.nativeElement.showModal();
    this.search()?.nativeElement.focus();
  }
  searchChanged(event: Event) {
    this.query.set((event.target as HTMLInputElement).value);
    this.index.set(0);
  }
  keydown(event: KeyboardEvent) {
    if (event.isComposing) return;
    const count = this.filtered().length;
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      this.index.update((index) =>
        count ? (index + (event.key === 'ArrowDown' ? 1 : -1) + count) % count : 0,
      );
      this.dialog()
        ?.nativeElement.querySelector('#command-' + this.index())
        ?.scrollIntoView({ block: 'nearest' });
    } else if (event.key === 'Enter' && this.filtered()[this.index()]) {
      event.preventDefault();
      this.choose(this.filtered()[this.index()].id);
    }
  }
  choose(id: string) {
    this.dialog()?.nativeElement.close();
    this.selected.emit(id);
  }
}
