import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { SelectedText } from '../../shared/browser/text-selection';
import { Icon } from '../../shared/ui/icon';
import { TEXT_ACTIONS, TEXT_ACTION_ICON_PROVIDER } from './text-actions';

@Component({
  selector: 'nx-text-selection-toolbar',
  imports: [Icon],
  providers: [TEXT_ACTION_ICON_PROVIDER],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div
    #panel
    class="paragraph-float"
    role="toolbar"
    aria-label="選取文字操作"
    (pointerdown)="$event.preventDefault()"
    (keydown)="key($event)"
  >
    @for (action of actions; track action.value) {
      <button type="button" (click)="selected.emit(action.value)">
        <nx-icon [name]="action.icon" />{{ action.label }}
      </button>
    }
    @if (canSave()) {
      <button type="button" (click)="selected.emit('save')">
        <nx-icon name="document" />儲存成果
      </button>
    }
    <button
      type="button"
      class="icon-button"
      aria-label="關閉選取文字操作"
      (click)="dismissed.emit()"
    >
      <nx-icon name="close" />
    </button>
  </div>`,
})
export class TextSelectionToolbar {
  readonly selection = input.required<SelectedText>();
  readonly canSave = input(false);
  readonly selected = output<string>();
  readonly dismissed = output<void>();
  readonly actions = TEXT_ACTIONS;
  private readonly panel = viewChild.required<ElementRef<HTMLElement>>('panel');
  constructor() {
    afterRenderEffect(() => this.position(this.selection()));
    const resize = () => this.position(this.selection());
    window.addEventListener('resize', resize);
    inject(DestroyRef).onDestroy(() => window.removeEventListener('resize', resize));
  }
  private position(anchor: SelectedText) {
    const panel = this.panel().nativeElement;
    const width = panel.offsetWidth,
      height = panel.offsetHeight;
    const top = anchor.top - height - 8;
    panel.style.left = `${Math.max(12, Math.min(anchor.left, innerWidth - width - 12))}px`;
    panel.style.top = `${Math.max(12, Math.min(top >= 12 ? top : anchor.bottom + 8, innerHeight - height - 12))}px`;
  }
  key(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.dismissed.emit();
      return;
    }
    const buttons = [...this.panel().nativeElement.querySelectorAll<HTMLButtonElement>('button')];
    const index = buttons.indexOf(event.target as HTMLButtonElement);
    const next =
      event.key === 'ArrowRight'
        ? (index + 1) % buttons.length
        : event.key === 'ArrowLeft'
          ? (index - 1 + buttons.length) % buttons.length
          : event.key === 'Home'
            ? 0
            : event.key === 'End'
              ? buttons.length - 1
              : -1;
    if (next >= 0) {
      event.preventDefault();
      buttons[next].focus();
    }
  }
}
