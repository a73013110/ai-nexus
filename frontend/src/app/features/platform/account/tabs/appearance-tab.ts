import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { SettingGroup, SettingRow } from '@shared/ui/setting-row';
import { SettingsStore } from '../settings-store';

const pixels = (values: number[]) =>
  values.map((value) => ({ value: String(value), label: `${value} px` }));

/** 主題、動態效果與閱讀版面；字級、行距與寬度只影響對話與文件正文，附即時預覽。 */
@Component({
  selector: 'nx-appearance-tab',
  imports: [SettingGroup, SettingRow],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './appearance-tab.scss',
  template: `<nx-setting-group heading="工作區外觀">
      <nx-setting-row
        label="主題"
        description="配合環境光線，或跟隨作業系統。"
        [options]="themes"
        [value]="store.draft().appearance.theme"
        (valueChange)="store.appearance('theme', $event)"
      />
      <nx-setting-row
        label="減少動態效果"
        description="保留狀態提示，減少進場和持續動畫。"
        [checked]="store.draft().appearance.reducedMotion"
        (checkedChange)="store.appearance('reducedMotion', $event)"
      />
    </nx-setting-group>
    <nx-setting-group heading="閱讀與版面">
      <nx-setting-row
        label="對話字級"
        description="對話正文與訊息輸入使用相同字級。"
        [options]="fontSizes"
        [value]="'' + store.draft().readingFontSize"
        (valueChange)="store.update('readingFontSize', +$event)"
      />
      <nx-setting-row
        label="正文行距"
        description="為長篇內容保留清楚的行間空間。"
        [options]="lineHeights"
        [value]="'' + store.draft().readingLineHeight"
        (valueChange)="store.update('readingLineHeight', +$event)"
      />
      <nx-setting-row
        label="內容寬度"
        description="對話與輸入區同步調整；窄版適合專注閱讀，寬版適合表格。"
        [options]="widths"
        [value]="store.draft().readingWidth"
        (valueChange)="store.update('readingWidth', $event)"
      />
      <nx-setting-row
        label="訊息密度"
        description="調整段落之間的留白。"
        [options]="densities"
        [value]="store.draft().density"
        (valueChange)="store.update('density', $event)"
      />
      <nx-setting-row
        label="側欄寬度"
        description="讓較長的對話名稱更容易瀏覽。"
        [options]="sidebarWidths"
        [value]="'' + store.draft().sidebarWidth"
        (valueChange)="store.update('sidebarWidth', +$event)"
      />
    </nx-setting-group>
    <section class="reading-preview" aria-label="閱讀預覽">
      <header><strong>即時預覽</strong><span>文字與程式碼</span></header>
      <h2>讓每一段文字，都容易閱讀。</h2>
      <p>
        AI Nexus
        將想法、文件與工作成果連在一起。你可以調整閱讀節奏，保留足夠的留白，也讓重要資訊更容易被看見。
      </p>
      <footer><span>程式碼</span><code>const nextStep = '開始';</code></footer>
    </section>`,
})
export class AppearanceTab {
  readonly store = inject(SettingsStore);
  readonly themes = [
    { value: 'system', label: '跟隨系統' },
    { value: 'light', label: '淺色' },
    { value: 'dark', label: '深色' },
  ];
  readonly fontSizes = pixels([12, 13, 14, 15, 16, 17, 18, 20, 22, 24]);
  readonly lineHeights = [1, 1.2, 1.4, 1.5, 1.6, 1.8, 2, 2.2].map((ratio) => ({
    value: String(ratio),
    label: `${ratio} 倍`,
  }));
  readonly widths = [
    { value: 'narrow', label: '專注 · 窄' },
    { value: 'standard', label: '標準' },
    { value: 'wide', label: '寬廣' },
  ];
  readonly densities = [
    { value: 'comfortable', label: '舒適', description: '適合長文閱讀' },
    { value: 'compact', label: '緊湊', description: '一次看見更多內容' },
  ];
  readonly sidebarWidths = pixels([240, 264, 288, 320, 360]);
}
