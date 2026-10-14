import { Notice } from '../../shared/ui/notice';
import { Field } from '../../shared/ui/field';
import { ClientValidationError } from '../../core/errors/safe-errors';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { EffectiveModelPolicyDto, ModelDto, ModelPolicyRequest } from '../../core/api/schema';
import { Checkbox } from '../../shared/ui/checkbox';
import { DataTable } from '../../shared/ui/data-table';
import { InfoPopover } from '../../shared/ui/info-popover';
import { formatModelName, formatNumber } from '../../shared/browser/format';

export interface ModelPolicyDraft {
  restricted: boolean;
  modelIds: string[];
  tokenLimits: Record<string, string>;
}
export function modelPolicyDraft(policy?: ModelPolicyRequest | null): ModelPolicyDraft {
  return {
    restricted: policy?.allowedModelIds != null,
    modelIds: policy?.allowedModelIds ? [...policy.allowedModelIds] : [],
    tokenLimits: Object.fromEntries(
      Object.entries(policy?.dailyTokenLimits ?? {}).map(([id, value]) => [id, String(value)]),
    ),
  };
}
export function modelPolicyRequest(draft: ModelPolicyDraft): ModelPolicyRequest {
  const limits: Record<string, number> = {};
  for (const [id, text] of Object.entries(draft.tokenLimits)) {
    if (text.trim() === '') continue;
    const value = Number(text);
    if (!Number.isSafeInteger(value) || value < 0 || value > 1_000_000_000_000)
      throw new ClientValidationError('tokenLimit');
    limits[id] = value;
  }
  return { allowedModelIds: draft.restricted ? draft.modelIds : null, dailyTokenLimits: limits };
}

/**
 * 群組或單一使用者的模型授權與每日 token 預算。不自帶標題：兩個使用它的對話框都以「AI 模型」頁籤
 * 呈現，名稱由頁籤提供。填欄位需要的規則留在畫面上，其餘的合併規則收進「額度規則」。
 */
@Component({
  selector: 'nx-model-policy-editor',
  imports: [Notice, Field, Checkbox, DataTable, InfoPopover],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './model-policy-editor.scss',
  template: `
    <div class="model-policy-head">
      <nx-checkbox
        label="限制可用模型"
        [description]="
          personal()
            ? '只能限縮群組授權；勾選的模型仍需群組與平台授權。'
            : '只授予勾選的模型；未限制時授予所有平台可用模型。'
        "
        [checked]="value().restricted"
        [disabled]="disabled()"
        (checkedChange)="change({ restricted: $event })"
      />
      <nx-info-popover label="額度規則" panelLabel="模型授權與額度規則" align="right" [width]="340">
        <ul class="form-note-list">
          @if (personal()) {
            <li>個人 token 上限優先於群組，可提高或降低；個人模型清單只能限縮群組授權。</li>
          } @else {
            <li>使用者的模型授權取所有有效群組的聯集。</li>
            <li>各模型只合併有授權的有效群組額度，取最高值，無上限優先；依每位使用者計算。</li>
          }
          <li>聊天、OCR、文字工具與評測共用預算。</li>
          <li>執行中及未回報完整用量的請求保留預估額度；實際用量只計模型回報。</li>
        </ul>
      </nx-info-popover>
    </div>
    <nx-data-table class="model-policy-table" label="模型授權與額度" [stacked]="true">
      <table>
        <colgroup>
          <col />
          @if (effective()) {
            <col class="model-policy-effective-column" />
          }
          <col class="model-policy-limit-column" />
        </colgroup>
        <thead>
          <tr>
            <th scope="col">模型</th>
            @if (effective()) {
              <th scope="col">目前生效</th>
            }
            <th scope="col">每日 token 上限</th>
          </tr>
        </thead>
        <tbody>
          @for (model of models(); track model.id) {
            <tr>
              <td class="model-policy-name">
                @if (value().restricted) {
                  <nx-checkbox
                    [label]="name(model)"
                    [description]="kind(model)"
                    [checked]="value().modelIds.includes(model.id)"
                    [disabled]="disabled()"
                    (checkedChange)="allow(model.id, $event)"
                  />
                } @else {
                  <strong>{{ name(model) }}</strong>
                  <small>{{ kind(model) }}</small>
                }
              </td>
              @if (effective(); as policy) {
                <td data-label="目前生效">
                  @if (budget(model.id); as current) {
                    <span
                      >{{
                        current.dailyTokenLimit == null
                          ? '不限制'
                          : format(current.dailyTokenLimit) + ' tokens / 日'
                      }}
                      ·
                      {{
                        current.source === 'personal'
                          ? '個人設定'
                          : current.source === 'group'
                            ? '群組設定'
                            : '平台預設'
                      }}</span
                    >
                    <small
                      >已回報 {{ format(current.usedTokens) }} · 預留
                      {{ format(current.reservedTokens) }} · 剩餘
                      {{
                        current.remainingTokens == null ? '不限' : format(current.remainingTokens)
                      }}</small
                    >
                    @if (
                      policy.allowedModelIds != null && !policy.allowedModelIds.includes(model.id)
                    ) {
                      <small class="model-policy-blocked">目前授權未允許此模型</small>
                    }
                  } @else {
                    <small>—</small>
                  }
                </td>
              }
              <td data-label="每日 token 上限">
                <input
                  nxField
                  type="number"
                  min="0"
                  max="1000000000000"
                  step="1"
                  [attr.aria-label]="'每日 token 上限：' + name(model)"
                  [placeholder]="personal() ? '繼承群組' : '不限制'"
                  [value]="value().tokenLimits[model.id] ?? ''"
                  [readOnly]="disabled()"
                  (input)="limit(model.id, $any($event.target).value)"
                />
              </td>
            </tr>
          } @empty {
            <tr>
              <td [attr.colspan]="effective() ? 3 : 2">
                目前沒有設定模型，請先設定地端模型供應商。
              </td>
            </tr>
          }
        </tbody>
      </table>
    </nx-data-table>
    @if (value().restricted && !value().modelIds.length) {
      <nx-notice
        tone="danger"
        [message]="
          personal()
            ? '尚未勾選模型；儲存後將禁止此帳號使用所有模型。'
            : '尚未勾選模型；此群組不授予模型，使用者仍可由其他群組取得授權。'
        "
      />
    }
    <p class="form-note">
      每日上限合計輸入與輸出 tokens，台北時間 08:00 重設；{{
        personal()
          ? '0 禁止此帳號生成，留空繼承群組。'
          : '0 表示此群組不提供額度，留空表示此群組不限額度。'
      }}
    </p>
  `,
})
export class ModelPolicyEditor {
  readonly models = input.required<ModelDto[]>();
  readonly value = input.required<ModelPolicyDraft>();
  readonly valueChange = output<ModelPolicyDraft>();
  readonly effective = input<EffectiveModelPolicyDto | null>(null);
  readonly personal = input(false);
  readonly disabled = input(false);
  readonly name = formatModelName;
  readonly format = formatNumber;
  kind(model: ModelDto) {
    return model.supportsImages ? '文字與圖片' : '文字';
  }
  budget(id: string) {
    return this.effective()?.models.find((model) => model.modelId === id);
  }
  change(patch: Partial<ModelPolicyDraft>) {
    this.valueChange.emit({ ...this.value(), ...patch });
  }
  allow(id: string, checked: boolean) {
    this.change({
      modelIds: checked
        ? [...this.value().modelIds, id]
        : this.value().modelIds.filter((value) => value !== id),
    });
  }
  limit(id: string, value: string) {
    this.change({ tokenLimits: { ...this.value().tokenLimits, [id]: value } });
  }
}
