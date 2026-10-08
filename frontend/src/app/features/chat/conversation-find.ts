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
import type { Message } from '../../core/api/types';
import { Icon } from '../../shared/ui/icon';

@Component({
  selector: 'nx-conversation-find',
  host: { class: 'ui-density-compact' },
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ` <div class="conversation-find" role="search" aria-label="搜尋目前對話">
    <nx-icon name="search" /><label class="sr-only" for="message-find">尋找訊息</label
    ><input
      #search
      id="message-find"
      type="search"
      placeholder="尋找這段對話的文字…"
      [value]="query()"
      (input)="searchChanged($event)"
      (keydown)="keydown($event)"
    />
    <span class="find-count" role="status"
      >{{ matches().length ? index() + 1 : 0 }} / {{ matches().length }}</span
    >
    <button
      type="button"
      class="icon-button"
      aria-label="上一個符合訊息"
      [disabled]="!matches().length"
      (click)="move(-1)"
    >
      <nx-icon name="left" /></button
    ><button
      type="button"
      class="icon-button"
      aria-label="下一個符合訊息"
      [disabled]="!matches().length"
      (click)="move(1)"
    >
      <nx-icon name="chevron" /></button
    ><button type="button" class="icon-button" aria-label="關閉訊息搜尋" (click)="close.emit()">
      <nx-icon name="close" />
    </button>
  </div>`,
})
export class ConversationFind {
  readonly messages = input.required<Message[]>();
  readonly close = output<void>();
  readonly found = output<{ ids: string[]; active: string | null }>();
  readonly query = signal('');
  readonly index = signal(0);
  readonly matches = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    return query
      ? this.messages().filter((message) => message.content.toLocaleLowerCase().includes(query))
      : [];
  });
  private readonly search = viewChild<ElementRef<HTMLInputElement>>('search');
  focus() {
    this.search()?.nativeElement.focus();
  }
  searchChanged(event: Event) {
    this.query.set((event.target as HTMLInputElement).value);
    this.index.set(0);
    this.emit();
  }
  keydown(event: KeyboardEvent) {
    if (event.isComposing) return;
    if (event.key === 'Escape') this.close.emit();
    else if (event.key === 'Enter') {
      event.preventDefault();
      this.move(event.shiftKey ? -1 : 1);
    }
  }
  move(direction: number) {
    const count = this.matches().length;
    this.index.update((index) => (count ? (index + direction + count) % count : 0));
    this.emit();
  }
  private emit() {
    this.found.emit({
      ids: this.matches().map((message) => message.id),
      active: this.matches()[this.index()]?.id ?? null,
    });
  }
}
