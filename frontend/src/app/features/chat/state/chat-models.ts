import { computed, inject, Injectable, signal } from '@angular/core';
import { NexusApi } from '@core/api/nexus-api';
import type { ModelDto, ModelPolicyDto, ModelsDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';

const CHECKING_SEARCH = { available: false, notice: '正在確認網路搜尋設定…' };

/** ModelDto 目錄、所選的模型與強度，以及請求是否可加入網路搜尋。 */
@Injectable({ providedIn: 'root' })
export class ChatModels {
  private readonly api = inject(NexusApi);
  private readonly auth = inject(AuthService);
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
  readonly webSearchEnabled = signal(false);
  readonly webSearchStatus = signal(CHECKING_SEARCH);
  readonly current = computed(() => this.models().find((item) => item.id === this.modelId()));

  /** 政策允許選擇時為使用者的預設模型；否則為政策預設，再沒有則為第一個。 */
  adopt(catalog: ModelsDto, preferredModelId: string | null | undefined, preferredEffort: string) {
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

  async loadWebSearch() {
    const current = this.auth.guard();
    try {
      const status = await this.api.webSearchStatus();
      if (current()) this.webSearchStatus.set(status);
    } catch {
      if (current())
        this.webSearchStatus.set({
          available: false,
          notice: '暫時無法確認搜尋服務，請重新連線。',
        });
    }
  }

  reset() {
    this.models.set([]);
    this.webSearchEnabled.set(false);
    this.webSearchStatus.set(CHECKING_SEARCH);
  }
}
