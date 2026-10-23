import { uniqueId } from '../browser/unique-id';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  afterRenderEffect,
  booleanAttribute,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';

/**
 * 原生核取方塊的語意，搭配一致的整列指標目標。只有方框顯示勾選狀態：
 * 列表中的列保持同一背景，因此 hover（`--hover`）不會與它混淆。
 * `mixed` 是表頭全選的「部分已選」（原生 indeterminate，報讀為 mixed）；`hideLabel` 只留方框，
 * 名稱仍給輔助技術（表格的選取欄，姓名另有欄位顯示）。
 */
@Component({
  selector: 'nx-checkbox',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './checkbox.scss',
  template: `<label
    class="selection-control"
    [class.is-disabled]="disabled()"
    [class.is-pending]="pending()"
    [class.is-label-hidden]="hideLabel()"
  >
    <span class="selection-box"
      ><input
        #box
        type="checkbox"
        [attr.aria-label]="label()"
        [attr.aria-describedby]="description() ? id : null"
        [checked]="checked()"
        [indeterminate]="mixed() && !checked()"
        [disabled]="disabled()"
        [attr.aria-busy]="pending() || null"
        (change)="change($event)" />
      <span class="selection-indicator" aria-hidden="true"
        ><nx-icon [name]="mixed() && !checked() ? 'minus' : 'check'" /></span
    ></span>
    @if (!hideLabel()) {
      <span class="selection-copy"
        ><strong>{{ label() }}</strong>
        @if (description()) {
          <small [id]="id">{{ description() }}</small>
        }
      </span>
    }
    @if (pending()) {
      <nx-icon name="loading" class="selection-pending" />
    }
  </label>`,
})
export class Checkbox {
  readonly id = uniqueId('nx-checkbox');
  readonly label = input.required<string>();
  readonly description = input('');
  readonly checked = input(false);
  readonly disabled = input(false);
  readonly mixed = input(false);
  /**
   * 立即生效的勾選正在儲存：旁邊轉圈並忽略再次切換。不用 `disabled`，焦點才留在方框上；
   * 可還原的設定改用樂觀更新，不必等。
   */
  readonly pending = input(false);
  readonly hideLabel = input(false, { transform: booleanAttribute });
  readonly checkedChange = output<boolean>();

  private readonly injector = inject(Injector);
  private readonly box = viewChild.required<ElementRef<HTMLInputElement>>('box');

  constructor() {
    // 送出中保留使用者點出來的狀態；結束（或輸入改變）後依輸入校正，失敗時方框才跳回。
    afterRenderEffect(() => {
      const checked = this.checked();
      const mixed = this.mixed();
      if (!this.pending()) this.sync(checked, mixed);
    });
  }

  /**
   * 受控元件：方框先照使用者點的顯示（不先還原，否則畫面更新前讀到的仍是舊狀態），再通知使用端；
   * 下一次繪製後依輸入校正。使用端拒絕（例如超過人數上限、沒有可選的人）時輸入沒變、綁定不會重寫，
   * 校正才把方框拉回；使用端開始儲存（`pending`）就等它結束。送出中再點的一律拉回。
   */
  change(event: Event) {
    const box = event.target as HTMLInputElement;
    const ignored = this.pending();
    if (!ignored) this.checkedChange.emit(box.checked);
    afterNextRender(
      () => {
        if (ignored || !this.pending()) this.sync(this.checked(), this.mixed());
      },
      { injector: this.injector },
    );
  }

  private sync(checked: boolean, mixed: boolean) {
    const box = this.box().nativeElement;
    box.checked = checked;
    box.indeterminate = mixed && !checked;
  }
}
