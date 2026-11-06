import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * - `rest`：靜止，例如已完成回答的作者標記。
 * - `idle`：待命，久久沿邊換一格（助理圓鈕）。
 * - `working`：模型工作中，沿邊換位後收成一排依序跳起，再展回三角。
 * - `waiting`：排隊中，只有明暗呼吸、不移動，和工作中一眼分得出來。
 */
export type AiMarkState = 'rest' | 'idle' | 'working' | 'waiting';

// 幾何只寫在這裡，樣式只管時間軸。viewBox 24×24，三角形重心在正中央，旋轉換位時不會偏。
const VERTICES = [
  [12, 4.5],
  [18.5, 15.75],
  [5.5, 15.75],
] as const;
// 收成一排時各頂點落在 y=12 的哪個 x；左右略往外推，三點才不會擠在一起。
const ROW_X = [12, 19, 5] as const;
const ROW_Y = 12;
const ROW_ORDER = [...ROW_X].sort((a, b) => a - b);

// 每個點一輪結束時停在下一個頂點：三點外觀相同，下一輪從原位重播看起來仍是連續的。
const NODES = VERTICES.map(([x, y], index) => {
  const next = (index + 1) % VERTICES.length;
  const [nextX, nextY] = VERTICES[next];
  return {
    x,
    y,
    style: {
      '--hop': `${nextX - x}px ${nextY - y}px`,
      '--row': `${ROW_X[next] - x}px ${ROW_Y - y}px`,
      '--order': ROW_ORDER.indexOf(ROW_X[next]),
    },
  };
});
const EDGES = `M${VERTICES.map(([x, y]) => `${x} ${y}`).join('L')}Z`;

/**
 * AI 的識別：三個互連的節點。模型活動（生成、附件處理、背景工作）與助理圓鈕都用它，
 * 不另畫訊號或光點。大小跟 `--icon-size`（同 `nx-icon`），顏色預設 `--activity`、可由 `color` 覆寫；
 * 個別節點可由 `--ai-mark-node-1..3` 上色（助理圓鈕用，見 `styles/aurora.scss`）。
 */
@Component({
  selector: 'nx-ai-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './ai-mark.scss',
  host: { 'aria-hidden': 'true', '[attr.data-state]': 'state()' },
  template: `<svg viewBox="0 0 24 24">
    <path class="ai-mark-edges" [attr.d]="edges" />
    @for (node of nodes; track $index) {
      <g class="ai-mark-node" [style]="node.style">
        <circle [attr.cx]="node.x" [attr.cy]="node.y" r="2.5" />
      </g>
    }
  </svg>`,
})
export class AiMark {
  readonly state = input<AiMarkState>('rest');
  protected readonly nodes = NODES;
  protected readonly edges = EDGES;
}
