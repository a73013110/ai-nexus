import { computed, signal } from '@angular/core';
import type { ModelDto, ModelPolicyDto, ModelsDto } from '@core/api/schema';
import { formatModelName, formatNumber } from '@shared/browser/format';

/** `nx-select` 的模型選項：名稱與上下文大小（支援圖片時註明）。對話與助理同一種寫法。 */
export const modelOptions = (models: readonly ModelDto[]) =>
  models.map((model) => ({
    value: model.id,
    label: formatModelName(model),
    description: `${formatNumber(model.contextTokens)} Context${model.supportsImages ? ' · 圖片分析' : ''}`,
  }));

/**
 * 可用模型（`/models`，已依個人的模型政策過濾）與目前選的模型、思考強度。
 * 對話的 `ChatModels` 與助理各有一份：在助理換模型不會改到對話正在用的模型。
 */
export class ModelChoice {
  readonly models = signal<ModelDto[]>([]);
  readonly modelId = signal('');
  readonly policy = signal<ModelPolicyDto>({
    allowModelSelection: true,
    showModelNames: true,
    defaultModelId: null,
    maxInputCharacters: 12000,
  });
  readonly reasoningEffort = signal('auto');
  readonly notice = signal<string | null>(null);
  readonly current = computed(() => this.models().find((item) => item.id === this.modelId()));
  readonly options = computed(() => modelOptions(this.models()));

  /** 政策允許選擇時為使用者的預設模型；否則為政策預設，再沒有則為第一個。 */
  adopt(catalog: ModelsDto, preferredModelId: string | null | undefined, preferredEffort = 'auto') {
    const models = catalog.models as ModelDto[];
    this.models.set(models);
    this.policy.set(catalog.policy as ModelPolicyDto);
    this.notice.set(catalog.notice ?? null);
    this.modelId.set(
      catalog.policy.allowModelSelection && models.some((item) => item.id === preferredModelId)
        ? preferredModelId!
        : models.some((item) => item.id === catalog.policy.defaultModelId)
          ? catalog.policy.defaultModelId!
          : (models[0]?.id ?? ''),
    );
    const model = this.current();
    this.reasoningEffort.set(
      model?.reasoningEfforts.includes(preferredEffort)
        ? preferredEffort
        : (model?.defaultReasoningEffort ?? 'auto'),
    );
  }

  /** 政策固定了模型，或 id 不在目錄中時回傳 false。 */
  choose(id: string) {
    const model = this.models().find((item) => item.id === id);
    if (!this.policy().allowModelSelection || !model) return false;
    this.modelId.set(id);
    this.reasoningEffort.set(model.defaultReasoningEffort);
    return true;
  }
}
