import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { DonutChart } from '../../shared/ui/donut-chart';
import { HeatmapCalendar } from '../../shared/ui/heatmap-calendar';
import { NumberTicker } from '../../shared/ui/number-ticker';
import { Sparkline } from '../../shared/ui/sparkline';
import { Spotlight } from '../../shared/ui/spotlight';
import { DesignExample, DesignSection } from './design-section';

function day(offset: number) {
  const date = new Date(Date.now() - offset * 86400000);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}
const TREND = [4, 7, 5, 9, 6, 11, 8, 13, 10, 15, 12, 18];

/** Data visualization used by the overview: counters, trends, distributions and day heat. */
@Component({
  selector: 'nx-design-charts',
  imports: [
    DesignExample,
    DesignSection,
    DonutChart,
    HeatmapCalendar,
    NumberTicker,
    Sparkline,
    Spotlight,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-6);
    }
    nx-design-example {
      flex: 1 1 14rem;
    }
    .design-ticker {
      font-size: var(--p-font-3xl);
      font-weight: 700;
      letter-spacing: -0.04em;
    }
    nx-sparkline {
      width: 12rem;
    }
    nx-donut-chart,
    nx-heatmap-calendar {
      width: min(100%, 30rem);
    }
    .design-spot {
      position: relative;
      display: grid;
      place-items: center;
      width: min(100%, 20rem);
      height: 8rem;
      border: 1px solid var(--line);
      border-radius: var(--p-radius-lg);
      background:
        radial-gradient(
          14rem circle at var(--spot-x, 50%) var(--spot-y, 50%),
          color-mix(in srgb, var(--series-5) 18%, transparent),
          transparent 70%
        ),
        var(--surface);
      color: var(--secondary);
      font-size: var(--text-caption);
      transform: perspective(700px) rotateX(var(--tilt-x, 0deg)) rotateY(var(--tilt-y, 0deg));
      transition: transform var(--motion-panel) var(--ease);
    }
  `,
  template: `<nx-design-section
      id="design-ticker"
      title="數字滾動與迷你走勢"
      api="NumberTicker · Sparkline"
      description="數字從前一個值滾到新值，報讀器與減少動態直接取得結果；未知值顯示「—」而不是 0。走勢線是裝飾，意義由旁邊的數字承載。"
    >
      <nx-design-example label="NumberTicker">
        <span class="design-ticker"><nx-number-ticker [value]="count()" /></span>
        <button type="button" class="secondary-button" (click)="count.set(count() + 1234)">
          增加
        </button>
      </nx-design-example>
      <nx-design-example label="未知值">
        <span class="design-ticker"><nx-number-ticker [value]="null" /></span>
      </nx-design-example>
      <nx-design-example label="Sparkline">
        <nx-sparkline [values]="trend" />
      </nx-design-example>
      <nx-design-example label="無填色">
        <nx-sparkline [values]="trend" [area]="false" />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-donut"
      title="環狀分布"
      api="DonutChart"
      description="依序使用 --series-1～7；滑過或聚焦圖例時中央改顯示該部分。圖例即是可讀的數值清單。"
    >
      <nx-design-example label="分布">
        <nx-donut-chart
          label="示範模型分布"
          [segments]="[
            { key: 'a', label: '雲端助理', value: 62 },
            { key: 'b', label: '本機模型', value: 31 },
            { key: 'c', label: '網路搜尋', value: 7 },
          ]"
        />
      </nx-design-example>
      <nx-design-example label="沒有資料">
        <nx-donut-chart label="示範空分布" [segments]="[]" />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-heatmap"
      title="日曆熱圖"
      api="HeatmapCalendar"
      description="每週一欄、週一在上，依最忙的一天分五階（--heat）；每一天可聚焦讀出數值。"
    >
      <nx-heatmap-calendar
        label="示範每日呼叫"
        unit=" 次"
        [points]="heat"
        [from]="from"
        [through]="through"
      />
    </nx-design-section>
    <nx-design-section
      id="design-spotlight"
      title="聚光與傾斜"
      api="nxSpotlight"
      description="指標移動時寫入 --spot-x／--spot-y，給值時一併寫入 --tilt-x／--tilt-y；外觀由宿主決定，觸控與減少動態保持平面。"
    >
      <div class="design-spot" nxSpotlight="6">移動指標看光線與傾斜</div>
    </nx-design-section>`,
})
export class DesignCharts {
  readonly count = signal(12_480);
  readonly trend = TREND;
  readonly from = day(55);
  readonly through = day(0);
  readonly heat = Array.from({ length: 56 }, (_, i) => ({
    date: day(i),
    value: i % 7 === 5 || i % 7 === 6 ? i % 3 : 4 + ((i * 7) % 11),
  }));
}
