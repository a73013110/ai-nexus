import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { DesignSection } from './design-section';

/** Colour, type, spacing, radius and motion tokens, rendered from the live CSS variables. */
@Component({
  selector: 'nx-design-foundations',
  imports: [DesignSection],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './design-foundations.scss',
  template: `<nx-design-section
      id="design-colors"
      title="色彩"
      api="tokens.scss"
      description="中性表面與文字層級為主；顏色只用於選取、焦點與真實狀態。"
      [stacked]="true"
    >
      @for (group of colors; track group.name) {
        <div class="design-swatches" [attr.aria-label]="group.name" role="list">
          @for (swatch of group.tokens; track swatch.token) {
            <div class="design-swatch" role="listitem">
              <span
                class="design-color"
                [style.background]="'var(' + swatch.token + ')'"
                aria-hidden="true"
              ></span>
              <strong>{{ swatch.label }}</strong>
              <code>{{ swatch.token }}</code>
            </div>
          }
        </div>
      }
      <button type="button" class="quiet-button design-export" (click)="exportTokens.emit()">
        匯出目前 tokens
      </button>
    </nx-design-section>
    <nx-design-section
      id="design-type"
      title="字級"
      api="--text-*"
      description="操作尺度固定；只有閱讀正文跟隨個人設定。"
      [stacked]="true"
    >
      <dl class="design-scale">
        @for (step of type; track step.token) {
          <div>
            <dt>
              <code>{{ step.token }}</code
              ><small>{{ step.use }}</small>
            </dt>
            <dd [class]="step.className" [style.font-size]="'var(' + step.token + ')'">
              {{ step.sample }}
            </dd>
          </div>
        }
      </dl>
    </nx-design-section>
    <nx-design-section
      id="design-space"
      title="間距、圓角與面積"
      api="--p-space-* · --p-radius-*"
      description="4px 基準；桌面控制項 34px，觸控與窄螢幕 44px。"
    >
      <div class="design-spacing" aria-label="間距 4 到 32 像素">
        @for (space of spaces; track space) {
          <span><i [style.width.px]="space" aria-hidden="true"></i>{{ space }}</span>
        }
      </div>
      <div class="design-radii">
        @for (radius of radii; track radius) {
          <span [style.border-radius]="'var(--p-radius-' + radius + ')'">{{ radius }}</span>
        }
      </div>
    </nx-design-section>
    <nx-design-section
      id="design-motion"
      title="動效"
      api="motion.scss"
      description="交代東西從哪來、變成什麼；彈簧用於形狀與選取，減少動態時直接到位。"
    >
      @for (curve of motion; track curve.token) {
        <button
          type="button"
          class="design-motion"
          [style.--curve]="'var(' + curve.token + ')'"
          [style.--duration]="'var(' + curve.duration + ')'"
        >
          <span class="design-motion-track"><i></i></span>
          <strong>{{ curve.label }}</strong>
          <code>{{ curve.token }}</code>
        </button>
      }
    </nx-design-section>`,
})
export class DesignFoundations {
  readonly exportTokens = output<void>();
  readonly colors = [
    {
      name: '表面',
      tokens: [
        { token: '--canvas', label: '頁面' },
        { token: '--sidebar', label: '側欄' },
        { token: '--surface', label: '閱讀面' },
        { token: '--surface-subtle', label: '低層次' },
        { token: '--hover', label: '懸停' },
        { token: '--line', label: '分隔線' },
      ],
    },
    {
      name: '文字與語意',
      tokens: [
        { token: '--ink', label: '主要文字' },
        { token: '--secondary', label: '次要文字' },
        { token: '--accent', label: '主要操作' },
        { token: '--signal', label: '處理訊號' },
        { token: '--activity', label: '模型活動' },
        { token: '--warning', label: '注意' },
        { token: '--error', label: '錯誤' },
      ],
    },
  ];
  readonly type = [
    { token: '--p-font-2xl', use: '頁面標題', sample: '介面元件', className: 'is-title' },
    { token: '--text-heading', use: '區塊標題', sample: '先讓資訊看得清楚', className: '' },
    {
      token: '--text-body',
      use: '閱讀正文（個人設定）',
      sample: '正文預設 15px，字級、行距與寬度可在個人設定調整。',
      className: 'design-reading',
    },
    { token: '--text-ui', use: '緊湊操作', sample: '傳達下一個動作', className: '' },
    { token: '--text-caption', use: '輔助資訊', sample: '時間、狀態與來源', className: '' },
  ];
  readonly spaces = [4, 8, 12, 16, 24, 32];
  readonly radii = ['sm', 'md', 'lg', 'pill'];
  readonly motion = [
    { token: '--ease', duration: '--motion', label: '進場' },
    { token: '--ease-exit', duration: '--motion', label: '離場' },
    { token: '--ease-spring', duration: '--motion-morph', label: '彈簧' },
  ];
}
