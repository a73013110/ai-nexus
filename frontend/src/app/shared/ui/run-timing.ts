import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { RunTimingDto } from '../../core/api/schema';
import { formatDuration, formatNumber } from '../browser/format';

@Component({
  selector: 'nx-run-timing',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (value(); as timing) {
    <details class="run-timing">
      <summary>耗時 {{ duration(timing.totalMilliseconds) }}</summary>
      <span
        >排隊 {{ duration(timing.queueMilliseconds) }} ·
        {{
          timing.generationMilliseconds == null
            ? '未開始生成'
            : '生成 ' + duration(timing.generationMilliseconds)
        }}
        @if (timing.inputTokens != null && timing.outputTokens != null) {
          · 輸入 {{ number(timing.inputTokens) }} / 輸出 {{ number(timing.outputTokens) }} tokens
        }
      </span>
    </details>
  }`,
  styles: `
    .run-timing {
      font-size: 0.8125rem;
      color: var(--muted);
    }
    summary {
      cursor: pointer;
      padding: 0.4rem 0;
    }
    span {
      display: block;
      padding-bottom: 0.4rem;
    }
  `,
})
export class RunTimingDisplay {
  readonly value = input<RunTimingDto | null | undefined>(null);
  readonly duration = formatDuration;
  readonly number = formatNumber;
}
