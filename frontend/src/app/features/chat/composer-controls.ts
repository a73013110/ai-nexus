import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { ContextUsage, Model, ModelPolicy } from '../../core/api/types';
import { Icon } from '../../shared/ui/icon';

@Component({
  selector: 'nx-composer-controls',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="composer-controls">
    @if (policy().allowModelSelection) {
      <label class="composer-select model-control">
        <span class="sr-only">選擇模型</span>
        <select
          aria-label="選擇模型"
          [value]="modelId()"
          [disabled]="disabled() || !models().length"
          (change)="modelChanged($event)"
        >
          @for (model of models(); track model.id) {
            <option [value]="model.id" [selected]="modelId() === model.id">
              {{ model.displayName }}
            </option>
          } @empty {
            <option value="">模型未就緒</option>
          }
        </select>
      </label>
    } @else {
      <span class="model-lock" title="模型由系統指定"
        ><nx-icon name="lock" />{{
          policy().showModelNames ? selected()?.displayName || '模型未就緒' : '系統指定'
        }}</span
      >
    }
    @if (selected()?.reasoningEfforts?.length) {
      <label class="composer-select reasoning-control">
        <span class="sr-only">思考強度</span>
        <select
          aria-label="思考強度"
          [value]="effort()"
          [disabled]="disabled()"
          (change)="effortChanged($event)"
        >
          <option value="auto" [selected]="effort() === 'auto'">自動思考</option>
          @for (value of selected()!.reasoningEfforts; track value) {
            <option [value]="value" [selected]="effort() === value">
              {{ effortLabel(value) }}
            </option>
          }
        </select>
      </label>
    }
    <details class="context-details" (keydown.escape)="closeContext($event)">
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
  </div>`,
})
export class ComposerControls {
  readonly models = input.required<Model[]>();
  readonly policy = input.required<ModelPolicy>();
  readonly modelId = input.required<string>();
  readonly effort = input('auto');
  readonly disabled = input(false);
  readonly usage = input<ContextUsage | null>(null);
  readonly modelChange = output<string>();
  readonly effortChange = output<string>();
  readonly selected = computed(() => this.models().find((x) => x.id === this.modelId()));
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
  modelChanged(event: Event) {
    if (event.target instanceof HTMLSelectElement) this.modelChange.emit(event.target.value);
  }
  effortChanged(event: Event) {
    if (event.target instanceof HTMLSelectElement) this.effortChange.emit(event.target.value);
  }
  closeContext(event: Event) {
    const target = event.currentTarget as HTMLDetailsElement;
    target.open = false;
    target.querySelector<HTMLElement>('summary')?.focus();
    event.stopPropagation();
  }
}
