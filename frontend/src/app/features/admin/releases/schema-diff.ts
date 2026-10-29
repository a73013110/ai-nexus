import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { diffLines, sideBySide } from '@shared/browser/diff';

/** 變更前後各留幾行相同內容；其餘收成一列，長的 procedure 才不必捲過整份沒變的本文。 */
const CONTEXT = 3;

/**
 * 結構物件的並排差異：左邊是這一版應有的定義，右邊是正式機目前的定義。
 * 新增的物件左邊整份留白，移除的物件右邊整份留白。
 */
@Component({
  selector: 'nx-schema-diff',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './schema-diff.scss',
  template: `<header class="diff-head">
      <span>這一版</span><span>正式機目前</span>
      @if (folded()) {
        <button type="button" class="quiet-button" (click)="expanded.set(!expanded())">
          {{ expanded() ? '只看變更處' : '顯示全部' }}
        </button>
      }
    </header>
    <div class="diff-rows" role="table" aria-label="這一版與正式機目前定義的差異">
      @for (row of rows(); track $index) {
        @if (row.skipped !== undefined) {
          <div class="diff-skip" role="row">
            <span role="cell">⋯ {{ row.skipped }} 行相同</span>
          </div>
        } @else {
          <div class="diff-row" role="row">
            <span class="n" role="cell">{{ row.left?.old }}</span
            ><span class="t" role="cell" [attr.data-kind]="row.left?.kind ?? 'none'">{{
              row.left?.text
            }}</span
            ><span class="n" role="cell">{{ row.right?.new }}</span
            ><span class="t" role="cell" [attr.data-kind]="row.right?.kind ?? 'none'">{{
              row.right?.text
            }}</span>
          </div>
        }
      } @empty {
        <p class="diff-empty">兩邊的定義相同。</p>
      }
    </div>`,
})
export class SchemaDiff {
  readonly expected = input<string | null>(null);
  readonly actual = input<string | null>(null);
  readonly expanded = signal(false);
  private readonly lines = computed(() => diffLines(this.expected() ?? '', this.actual() ?? ''));
  readonly rows = computed(() => sideBySide(this.lines(), this.expanded() ? Infinity : CONTEXT));
  readonly folded = computed(() => sideBySide(this.lines(), CONTEXT).some((row) => row.skipped));
}
