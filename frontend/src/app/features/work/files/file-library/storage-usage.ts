import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { AttachmentStorageDto } from '@core/api/schema';
import { formatBytes, formatNumber } from '@shared/browser/format';

/** 原始檔案配額，一行呈現：細長的計量條、已用量與上限，以及剩餘量。首次載入時先畫同高的骨架佔位。 */
@Component({
  selector: 'nx-storage-usage',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (storage(); as value) {
      <div
        class="storage-usage"
        [class.is-over]="value.usedBytes > value.limitBytes"
        title="包含草稿；同一原檔的多處引用只計一次。"
      >
        <meter
          min="0"
          [max]="value.limitBytes || 1"
          [value]="value.usedBytes"
          [attr.aria-label]="
            '原檔容量：已用 ' + bytes(value.usedBytes) + '，上限 ' + bytes(value.limitBytes)
          "
        ></meter>
        <span [title]="exact(value.usedBytes)"
          >已用 <strong>{{ bytes(value.usedBytes) }}</strong> / {{ bytes(value.limitBytes) }}</span
        >
        <span [title]="exact(value.remainingBytes)">剩餘 {{ bytes(value.remainingBytes) }}</span>
        @if (value.usedBytes > value.limitBytes) {
          <span>已超額，請刪除檔案或調整上限</span>
        }
      </div>
    } @else if (loading()) {
      <span class="skeleton storage-skeleton" role="status"
        ><span class="sr-only">正在載入容量…</span></span
      >
    }`,
  styles: `
    .storage-usage {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--p-space-1) var(--p-space-3);
      color: var(--muted);
      font-size: var(--text-caption);
      font-variant-numeric: tabular-nums;
    }
    meter {
      width: 8rem;
      height: 0.375rem;
      accent-color: var(--accent);
    }
    strong {
      color: var(--ink);
      font-weight: 600;
    }
    .is-over {
      color: var(--error);
    }
    .storage-skeleton {
      width: 18rem;
      max-width: 100%;
      height: 1.25rem;
    }
  `,
})
export class StorageUsage {
  readonly storage = input<AttachmentStorageDto | null | undefined>(null);
  /** 容量還沒讀到；佔住同一行，讀到後不推動版面。 */
  readonly loading = input(false);
  readonly bytes = formatBytes;
  exact(value: number) {
    return `${formatNumber(value)} bytes`;
  }
}
