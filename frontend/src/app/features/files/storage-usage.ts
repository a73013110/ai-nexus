import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { AttachmentStorageDto } from '../../core/api/schema';
import { formatBytes, formatNumber } from '../../shared/browser/format';

@Component({
  selector: 'nx-storage-usage',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (storage(); as value) {
    <div class="storage-usage" aria-label="原檔容量">
      <meter
        min="0"
        [max]="value.limitBytes || 1"
        [value]="value.usedBytes"
        [attr.aria-label]="'已用 ' + bytes(value.usedBytes) + '，上限 ' + bytes(value.limitBytes)"
      ></meter>
      <div class="storage-usage-values">
        <span [title]="exact(value.usedBytes)"
          >已用 <strong>{{ bytes(value.usedBytes) }}</strong></span
        >
        <span [title]="exact(value.limitBytes)"
          >上限 <strong>{{ bytes(value.limitBytes) }}</strong></span
        >
        <span [title]="exact(value.remainingBytes)"
          >剩餘 <strong>{{ bytes(value.remainingBytes) }}</strong></span
        >
      </div>
      <small
        >包含草稿；同一原檔的多處引用只計一次。{{
          value.usedBytes > value.limitBytes ? '目前已超額，請刪除檔案或調整上限。' : ''
        }}</small
      >
    </div>
  }`,
  styles: `
    :host {
      display: block;
    }
    .storage-usage {
      display: grid;
      gap: 0.5rem;
    }
    meter {
      width: 100%;
      height: 0.6rem;
      accent-color: var(--accent);
    }
    .storage-usage-values {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem 1.5rem;
    }
    small {
      color: var(--muted);
    }
  `,
})
export class StorageUsage {
  readonly storage = input<AttachmentStorageDto | null | undefined>(null);
  readonly bytes = formatBytes;
  exact(value: number) {
    return `${formatNumber(value)} bytes`;
  }
}
