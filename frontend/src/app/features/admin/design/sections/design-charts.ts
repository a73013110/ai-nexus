import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { localDateKey } from '@shared/browser/calendar-day';
import { DonutChart } from '@shared/ui/donut-chart';
import { HeatmapCalendar } from '@shared/ui/heatmap-calendar';
import { NumberTicker } from '@shared/ui/number-ticker';
import { Sparkline } from '@shared/ui/sparkline';
import { Spotlight } from '@shared/ui/spotlight';
import { StatGrid, StatTile } from '@shared/ui/stat-tile';
import { DesignExample, DesignSection } from './design-section';

/** `offset` 天前的本地日期。 */
const day = (offset: number) => localDateKey(new Date(Date.now() - offset * 86_400_000));
const TREND = [4, 7, 5, 9, 6, 11, 8, 13, 10, 15, 12, 18];

/** 總覽使用的資料視覺化：計數器、趨勢、分布與每日熱度。 */
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
    StatGrid,
    StatTile,
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
    nx-stat-grid {
      width: 100%;
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
      id="design-stat"
      title="頭條數字"
      api="StatGrid · StatTile"
      description="管理頁摘要的一列數字：標籤、表格數字與一句脈絡，字級隨卡片寬度縮放；手機兩欄。值為 null 時保留同高的載入佔位，只有真實的警告或危險才上色。"
    >
      <nx-stat-grid label="示範摘要">
        <nx-stat-tile label="生成次數" value="18,452" unit="次" hint="完成 17,980 · 失敗 312" />
        <nx-stat-tile label="原檔容量" value="31.23 GB" hint="4,210 份原檔" />
        <nx-stat-tile label="載入中" [value]="null" hint="形狀已知的內容先佔位" />
        <nx-stat-tile label="未定價" value="2" unit="項" hint="費用顯示為未知" tone="warning" />
      </nx-stat-grid>
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
      title="聚光"
      api="nxSpotlight"
      description="指標移動時寫入 --spot-x／--spot-y；外觀由宿主決定，觸控不顯示。"
    >
      <div class="design-spot" nxSpotlight>移動指標看光線</div>
    </nx-design-section>`,
})
export class DesignCharts {
  readonly count = signal(12_480);
  readonly trend = TREND;
  readonly from = day(55);
  readonly through = day(0);
  readonly heat = Array.from({ length: 56 }, (_, index) => ({
    date: day(index),
    value: index % 7 === 5 || index % 7 === 6 ? index % 3 : 4 + ((index * 7) % 11),
  }));
}
