import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
} from '@angular/core';

/**
 * The foot of a settings form that edits something saved: note | revert | save. The buttons keep
 * their place; with nothing changed both rest and the note says so. `note` replaces it (e.g. while
 * writes are refused). Placed inside the `form`, whose submit is the save.
 */
@Component({
  selector: 'nx-dialog-save-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'dialog-foot' },
  styleUrl: './dialog-save-bar.scss',
  template: `<span class="dialog-note" role="status" [class.is-dirty]="dirty() && !note()">{{
      note() || (dirty() ? '有未儲存的變更' : '尚未變更')
    }}</span>
    <button
      type="button"
      class="secondary-button"
      [disabled]="!dirty() || saving()"
      (click)="undo()"
    >
      還原
    </button>
    <button type="submit" class="primary-button" [disabled]="!dirty() || saving() || disabled()">
      {{ saving() ? '儲存中…' : label() }}
    </button>`,
})
export class DialogSaveBar {
  readonly dirty = input.required<boolean>();
  /** The save button's text, e.g. 「儲存角色」. */
  readonly label = input.required<string>();
  readonly saving = input(false);
  /** Saving is refused (e.g. while testing another identity); revert stays available. */
  readonly disabled = input(false);
  readonly note = input('');
  readonly revert = output<void>();
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  /** The button disables itself, so focus moves to the form's first field instead of the page. */
  undo() {
    this.revert.emit();
    this.host.nativeElement
      .closest('form')
      ?.querySelector<HTMLElement>('.dialog-body :is(input, select, textarea)')
      ?.focus();
  }
}
