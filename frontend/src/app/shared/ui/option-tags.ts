import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { BadgeTone } from './count-badge';

/** 選項的狀態附註（停用、已隱藏、尚未綁帳號…）；名稱與說明只寫識別資訊，狀態一律放這裡。 */
export interface OptionTag {
  label: string;
  /** 預設 `neutral`（淡灰）；只有要使用者留意的狀態才換色。 */
  tone?: BadgeTone;
}

/** 由條件組出淡灰標籤，不成立的條件略過：`optionTags(!user.enabled && '停用')`。 */
export function optionTags(...labels: (string | false | null | undefined)[]): OptionTag[] {
  return labels.filter((label): label is string => !!label).map((label) => ({ label }));
}

/**
 * 選項名稱旁的狀態標籤，`Select`、`TreeSelect` 的選項與觸發鈕共用。
 * 與表格的 `StatusBadge` 區分：沒有圓點、字重較輕，只是附註；文字是選項名稱的一部分，前面補報讀器才聽得到的分隔。
 */
@Component({
  selector: 'nx-option-tags',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: inline-flex;
      flex: none;
      align-items: center;
      gap: var(--p-space-1);
    }
    .option-tag {
      --tag-color: var(--secondary);
      padding: 0 var(--p-space-2);
      border-radius: var(--radius-full);
      /* 疊在透明上：選項滑過、已選或觸發鈕的底色變了，標籤仍比底色深一階。 */
      background: color-mix(in srgb, var(--tag-color) 12%, transparent);
      color: var(--tag-color);
      font-size: var(--p-font-xs);
      font-weight: 400;
      line-height: 1.6;
      white-space: nowrap;
    }
    .option-tag[data-tone='info'] {
      --tag-color: var(--info);
    }
    .option-tag[data-tone='success'] {
      --tag-color: var(--success);
    }
    .option-tag[data-tone='warning'] {
      --tag-color: var(--warning);
    }
    .option-tag[data-tone='danger'] {
      --tag-color: var(--danger);
    }
  `,
  template: `@for (tag of tags(); track tag.label) {
    <span class="sr-only">，</span
    ><span class="option-tag" [attr.data-tone]="tag.tone ?? 'neutral'">{{ tag.label }}</span>
  }`,
})
export class OptionTags {
  readonly tags = input.required<readonly OptionTag[]>();
}
