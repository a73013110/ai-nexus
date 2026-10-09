import { computed, inject, Injectable, signal } from '@angular/core';
import { NexusApi } from '../../core/api/nexus-api';
import type { Model, ModelPolicy, Models } from '../../core/api/types';
import { AuthService } from '../../core/auth/auth-service';

const CHECKING_SEARCH = { available: false, notice: '正在確認網路搜尋設定…' };

/** Model catalog, the chosen model and effort, and whether web search may join the request. */
@Injectable({ providedIn: 'root' })
export class ChatModels {
  private readonly api = inject(NexusApi);
  private readonly auth = inject(AuthService);
  readonly models = signal<Model[]>([]);
  readonly modelId = signal('');
  readonly policy = signal<ModelPolicy>({
    allowModelSelection: true,
    showModelNames: true,
    defaultModelId: null,
    maxInputCharacters: 12000,
  });
  readonly reasoningEffort = signal('auto');
  readonly notice = signal<string | null>(null);
  readonly webSearchEnabled = signal(false);
  readonly webSearchStatus = signal(CHECKING_SEARCH);
  readonly current = computed(() => this.models().find((x) => x.id === this.modelId()));

  /** The user's default model when policy allows a choice, else the policy default, else the first. */
  adopt(catalog: Models, preferredModelId: string | null | undefined, preferredEffort: string) {
    const models = catalog.models as Model[];
    this.models.set(models);
    this.policy.set(catalog.policy as ModelPolicy);
    this.notice.set(catalog.notice ?? null);
    this.modelId.set(
      catalog.policy.allowModelSelection && models.some((x) => x.id === preferredModelId)
        ? preferredModelId!
        : models.some((x) => x.id === catalog.policy.defaultModelId)
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

  /** Returns false when policy fixes the model or the id is not in the catalog. */
  choose(id: string) {
    const model = this.models().find((x) => x.id === id);
    if (!this.policy().allowModelSelection || !model) return false;
    this.modelId.set(id);
    this.reasoningEffort.set(model.defaultReasoningEffort);
    return true;
  }

  async loadWebSearch() {
    const generation = this.auth.generation();
    try {
      const status = await this.api.webSearchStatus();
      if (generation === this.auth.generation()) this.webSearchStatus.set(status);
    } catch {
      if (generation === this.auth.generation())
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
