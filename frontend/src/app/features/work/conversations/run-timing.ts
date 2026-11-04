import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { RunTimingDto } from '@core/api/schema';
import { formatDuration, formatNumber } from '@shared/browser/format';
import { InfoPopover } from '@shared/ui/info-popover';

/** 回答的總耗時；點開看排隊、生成與 tokens。與費用（`ChargeLabel`）同一種資訊鈕，並排在回答下方。 */
@Component({
  selector: 'nx-run-timing',
  imports: [InfoPopover],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (value(); as timing) {
    <nx-info-popover
      icon="clock"
      [label]="duration(timing.totalMilliseconds)"
      description="這次回答的耗時與用量"
    >
      <table class="info-table">
        <tbody>
          <tr>
            <th scope="row">排隊</th>
            <td>{{ duration(timing.queueMilliseconds) }}</td>
          </tr>
          <tr>
            <th scope="row">生成</th>
            <td>
              {{
                timing.generationMilliseconds == null
                  ? '未開始生成'
                  : duration(timing.generationMilliseconds)
              }}
            </td>
          </tr>
          @if (timing.inputTokens != null && timing.outputTokens != null) {
            <tr>
              <th scope="row">tokens</th>
              <td>
                輸入 {{ number(timing.inputTokens) }} · 輸出 {{ number(timing.outputTokens) }}
              </td>
            </tr>
          }
        </tbody>
      </table>
    </nx-info-popover>
  }`,
})
export class RunTimingDisplay {
  readonly value = input<RunTimingDto | null | undefined>(null);
  readonly duration = formatDuration;
  readonly number = formatNumber;
}
