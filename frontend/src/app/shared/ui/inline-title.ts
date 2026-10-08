import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';
import { Field } from './field';

let sequence = 0;
@Component({
  selector: 'nx-inline-title',
  imports: [Icon, Field],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (editing()) {
      <div class="inline-title-editor" #editor (focusout)="leave($event)">
        <label class="sr-only" [for]="id">目前對話名稱</label>
        <input
          nxField
          #field
          [id]="id"
          [value]="draft()"
          maxlength="120"
          (input)="draft.set($any($event.target).value)"
          (keydown)="key($event)"
          [readOnly]="saving()"
          [attr.aria-invalid]="!!error()"
          [attr.aria-describedby]="id + '-help'"
        />
        <button
          type="button"
          class="icon-button"
          aria-label="儲存對話名稱"
          [disabled]="saving()"
          (click)="commit()"
        >
          <nx-icon name="check" />
        </button>
        <button
          type="button"
          class="icon-button"
          aria-label="取消修改名稱"
          [disabled]="saving()"
          (click)="cancel()"
        >
          <nx-icon name="close" />
        </button>
        <span
          [id]="id + '-help'"
          class="inline-title-help"
          [class.inline-error]="error()"
          role="status"
          >{{ error() || (saving() ? '正在儲存…' : 'Enter 儲存 · Esc 取消') }}</span
        >
      </div>
    } @else {
      <div class="inline-title-display">
        <h1
          tabindex="0"
          (dblclick)="begin()"
          (keydown)="displayKey($event)"
          [attr.aria-label]="value()"
          aria-description="雙擊或按 Enter 修改名稱"
          title="雙擊修改名稱"
        >
          {{ value() }}
        </h1>
        <button
          type="button"
          class="icon-button title-edit"
          aria-label="修改目前對話名稱"
          [disabled]="disabled()"
          (click)="begin()"
        >
          <nx-icon name="edit" />
        </button>
      </div>
    }`,
})
export class InlineTitle {
  readonly id = `nx-title-${++sequence}`;
  readonly value = input.required<string>();
  readonly scope = input.required<string>();
  readonly disabled = input(false);
  readonly save = input.required<(value: string) => Promise<boolean>>();
  readonly editing = signal(false);
  readonly draft = signal('');
  readonly saving = signal(false);
  readonly error = signal('');
  private readonly field = viewChild<ElementRef<HTMLInputElement>>('field');
  private readonly editor = viewChild<ElementRef<HTMLElement>>('editor');
  private version = 0;
  constructor() {
    effect(() => {
      this.scope();
      this.version++;
      this.editing.set(false);
      this.saving.set(false);
      this.error.set('');
    });
  }
  begin() {
    if (this.disabled()) return;
    this.draft.set(this.value());
    this.error.set('');
    this.editing.set(true);
    requestAnimationFrame(() => {
      this.field()?.nativeElement.focus();
      this.field()?.nativeElement.select();
    });
  }
  displayKey(event: KeyboardEvent) {
    if (event.key === 'Enter' || event.key === 'F2' || event.key === ' ') {
      event.preventDefault();
      this.begin();
    }
  }
  key(event: KeyboardEvent) {
    if (event.isComposing) return;
    if (event.key === 'Enter') {
      event.preventDefault();
      void this.commit();
    }
    if (event.key === 'Escape') {
      event.preventDefault();
      this.cancel();
    }
  }
  leave(event: FocusEvent) {
    if (
      this.editing() &&
      !this.editor()?.nativeElement.contains(event.relatedTarget as Node | null)
    )
      void this.commit();
  }
  cancel() {
    if (!this.saving()) this.editing.set(false);
  }
  async commit() {
    if (!this.editing() || this.saving()) return;
    const title = this.draft().trim();
    if (!title) {
      this.error.set('請輸入對話名稱。');
      return;
    }
    if (title === this.value()) {
      this.editing.set(false);
      return;
    }
    const version = this.version;
    this.saving.set(true);
    try {
      const success = await this.save()(title);
      if (version !== this.version) return;
      if (success) this.editing.set(false);
      else this.error.set('名稱未儲存，請再試一次。');
    } catch {
      if (version === this.version) this.error.set('名稱未儲存，請再試一次。');
    } finally {
      if (version === this.version) this.saving.set(false);
    }
  }
}
