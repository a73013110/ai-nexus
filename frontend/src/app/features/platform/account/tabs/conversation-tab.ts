import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import { WorkspaceApi } from '@core/api/workspace-api';
import { formatModelName } from '@shared/browser/format';
import { keyText } from '@shared/browser/hotkeys';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { SettingGroup, SettingRow } from '@shared/ui/setting-row';
import { SettingsStore } from '../settings-store';

/** 輸入與跟隨的習慣、新對話的模型與思考強度，以及這個部署是否能搜尋網路。 */
@Component({
  selector: 'nx-conversation-tab',
  imports: [Icon, Notice, SettingGroup, SettingRow],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--section-gap);
    }
    .policy-note {
      display: flex;
      align-items: center;
      gap: var(--p-space-2);
      margin: 0;
      color: var(--secondary);
      font-size: var(--text-caption);
    }
  `,
  template: `@if (error()) {
      <nx-notice [message]="error()"
        ><button type="button" notice-actions (click)="store.reload()">重新載入</button>
      </nx-notice>
    }
    <nx-setting-group heading="輸入與瀏覽">
      <nx-setting-row
        label="按 Enter 送出"
        [description]="
          store.draft().enterToSend
            ? 'Shift + Enter 換行，中文組字時不會誤送。'
            : 'Enter 換行，' + sendKeys + ' 送出。'
        "
        [checked]="store.draft().enterToSend"
        (checkedChange)="store.update('enterToSend', $event)"
      />
      <nx-setting-row
        label="自動跟隨新回覆"
        description="往上閱讀較早的內容時暫停跟隨，可按「回到最新」。"
        [checked]="store.draft().autoFollow"
        (checkedChange)="store.update('autoFollow', $event)"
      />
    </nx-setting-group>
    <nx-setting-group heading="新對話預設">
      @if (models()?.policy?.allowModelSelection) {
        <nx-setting-row
          label="預設模型"
          description="只列出管理員允許你使用的模型。"
          [options]="modelOptions()"
          [value]="store.draft().appearance.defaultModelId || ''"
          (valueChange)="store.appearance('defaultModelId', $event || null)"
        />
      } @else if (models()) {
        <p class="policy-note"><nx-icon name="lock" />模型由系統指定。</p>
      }
      <nx-setting-row
        label="預設思考強度"
        description="模型不支援所選強度時，改用模型預設。"
        [options]="efforts"
        [value]="store.draft().defaultReasoningEffort"
        (valueChange)="store.update('defaultReasoningEffort', $event)"
      />
      @if (webSearch(); as search) {
        <nx-setting-row
          [label]="'網路搜尋 · ' + (search.available ? '已啟用' : '尚未啟用')"
          [description]="
            search.available
              ? '預設關閉；在輸入區按「搜尋網路」後，只把該次提問送往搜尋服務。'
              : search.notice
          "
        >
          @if (!search.available) {
            <span setting-detail>
              地端模型本身不會連網，所以輸入區不顯示「搜尋網路」。需要時請管理員部署公司自架的
              SearXNG 並啟用搜尋服務。
            </span>
          }
        </nx-setting-row>
      }
    </nx-setting-group>`,
})
export class ConversationTab {
  readonly store = inject(SettingsStore);
  private readonly api = inject(WorkspaceApi);
  private readonly chat = computed(() => this.store.session.has('chat'));
  private readonly modelsRead = apiResource({
    params: () => this.store.version(),
    loader: () => (this.chat() ? this.api.models() : Promise.resolve(null)),
  });
  /** 輸入區在網路搜尋不可用時不顯示按鈕，原因只在這裡說明。 */
  private readonly webSearchRead = apiResource({
    params: () => this.store.version(),
    loader: () => (this.chat() ? this.api.webSearchStatus() : Promise.resolve(null)),
  });
  readonly models = computed(() => this.modelsRead.value() ?? null);
  readonly webSearch = computed(() => this.webSearchRead.value() ?? null);
  readonly error = computed(() => this.modelsRead.error() || this.webSearchRead.error());
  readonly sendKeys = keyText('mod+enter');
  readonly efforts = [
    { value: 'auto', label: '依模型預設' },
    { value: 'minimal', label: '快速回應' },
    { value: 'low', label: '輕度思考' },
    { value: 'medium', label: '標準思考' },
    { value: 'high', label: '深入思考' },
  ];
  readonly modelOptions = computed(() => [
    { value: '', label: '依系統預設' },
    ...(this.models()?.models ?? []).map((item) => ({
      value: item.id,
      label: formatModelName(item),
    })),
  ]);
}
