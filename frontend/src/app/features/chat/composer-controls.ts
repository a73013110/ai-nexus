import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { ContextUsage, Model, ModelPolicy } from '../../core/api/types';
import { Icon } from '../../shared/ui/icon';
import { Disclosure } from '../../shared/ui/disclosure';
import { Select } from '../../shared/ui/select';

@Component({
  selector: 'nx-composer-controls',
  imports: [Icon, Disclosure, Select],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="composer-controls">
    @if (policy().allowModelSelection) {
      <nx-select
        class="composer-select model-control"
        label="選擇模型"
        [placeholder]="modelPlaceholder()"
        [value]="modelId()"
        [disabled]="disabled() || loading() || !models().length"
        [options]="modelOptions()"
        [searchable]="true"
        (valueChange)="modelChange.emit($event)"
      />
    } @else {
      <span class="model-lock" title="模型由系統指定"
        ><nx-icon name="lock" />{{
          selected()
            ? policy().showModelNames
              ? selected()!.displayName
              : '系統指定'
            : modelPlaceholder()
        }}</span
      >
    }
    @if (selected()?.reasoningEfforts?.length) {
      <nx-select
        class="composer-select reasoning-control"
        label="思考強度"
        [value]="effort()"
        [disabled]="disabled()"
        [options]="effortOptions()"
        (valueChange)="effortChange.emit($event)"
      />
    }
    <div class="composer-tools">
      <button
        type="button"
        class="search-toggle"
        aria-label="搜尋網路"
        [attr.aria-pressed]="webSearch()"
        [title]="webSearchNotice()"
        [disabled]="disabled() || !webSearchAvailable()"
        (click)="webSearchChange.emit(!webSearch())"
      >
        <nx-icon name="globe" /><span>搜尋網路</span>
      </button>
      <details class="context-details" nxDisclosure>
        <summary
          class="context-trigger"
          [attr.aria-label]="'上下文用量：' + (usage() ? percent() + '%（預估）' : '尚未取得')"
          title="查看 Context 用量"
        >
          <svg
            viewBox="0 0 24 24"
            class="context-ring"
            [class.warning]="usage()?.budgetExceeded"
            aria-hidden="true"
          >
            <circle class="context-ring-track" cx="12" cy="12" r="8" />
            <circle
              class="context-ring-used"
              cx="12"
              cy="12"
              r="8"
              pathLength="100"
              [attr.stroke-dasharray]="percent() + ' ' + (100 - percent())"
            />
          </svg>
          <span>{{ usage() ? percent() + '%' : 'Context' }}</span>
        </summary>
        <div class="context-panel">
          <strong>Context 用量</strong>
          @if (usage(); as context) {
            <p class="context-value">
              約 {{ context.estimatedInputTokens.toLocaleString() }}
              <span>/ {{ context.contextTokens.toLocaleString() }} tokens</span>
            </p>
            <meter min="0" max="100" [value]="percent()" aria-label="預估上下文用量"></meter>
            <p>預留 {{ context.reservedOutputTokens.toLocaleString() }} tokens 給回答。</p>
            @if (context.reservedKnowledgeTokens) {
              <p>
                其中約 {{ context.reservedKnowledgeTokens.toLocaleString() }} tokens
                預留給知識來源。
              </p>
            }
            @if (context.reservedWebSearchTokens) {
              <p>
                約 {{ context.reservedWebSearchTokens.toLocaleString() }} tokens
                預留給網路摘要（不會在預覽時搜尋）。
              </p>
            }
            @if (context.droppedMessages) {
              <p>這次會略過最早 {{ context.droppedMessages }} 則上文；原始歷史仍保留。</p>
            }
            @if (context.budgetExceeded) {
              <p class="error-note">本次提問超出可用預算，請縮短內容。</p>
            }
            <p class="context-explanation">
              包含系統指令、目前分支與草稿，以保守 UTF-8 預算估算；實際 token 數依模型而異。
            </p>
          } @else {
            <p>選定可用模型後會取得預估用量。</p>
          }
        </div>
      </details>
    </div>
  </div>`,
})
export class ComposerControls {
  readonly models = input.required<Model[]>();
  readonly policy = input.required<ModelPolicy>();
  readonly modelId = input.required<string>();
  readonly effort = input('auto');
  readonly disabled = input(false);
  readonly loading = input(false);
  readonly loadFailed = input(false);
  readonly usage = input<ContextUsage | null>(null);
  readonly modelChange = output<string>();
  readonly effortChange = output<string>();
  readonly webSearch = input(false);
  readonly webSearchAvailable = input(false);
  readonly webSearchNotice = input('');
  readonly webSearchChange = output<boolean>();
  readonly selected = computed(() => this.models().find((x) => x.id === this.modelId()));
  readonly modelPlaceholder = computed(() =>
    this.loading()
      ? '正在載入模型…'
      : this.loadFailed()
        ? '模型清單載入失敗'
        : this.models().length
          ? '請選擇模型'
          : '沒有可用模型',
  );
  readonly modelOptions = computed(() =>
    this.models().map((x) => ({
      value: x.id,
      label: x.displayName,
      description: `${x.contextTokens.toLocaleString()} Context${x.supportsImages ? ' · 圖片分析' : ''}`,
    })),
  );
  readonly effortOptions = computed(() => [
    { value: 'auto', label: '自動思考' },
    ...(this.selected()?.reasoningEfforts || []).map((value) => ({
      value,
      label: this.effortLabel(value),
    })),
  ]);
  readonly percent = computed(() => {
    const value = this.usage();
    return value
      ? Math.min(100, Math.round((value.estimatedInputTokens / value.contextTokens) * 100))
      : 0;
  });
  effortLabel(value: string) {
    return (
      (
        { minimal: '快速回應', low: '輕度思考', medium: '標準思考', high: '深入思考' } as Record<
          string,
          string
        >
      )[value] ?? value
    );
  }
}
