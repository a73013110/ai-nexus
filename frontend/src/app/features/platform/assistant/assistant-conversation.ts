import { computed, DestroyRef, inject, signal } from '@angular/core';
import type {
  AssistantEventDto,
  AssistantFact,
  AssistantQuestion,
  ModelsDto,
} from '@core/api/schema';
import { WorkspaceApi } from '@core/api/workspace-api';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { isCancellation, safeMessage } from '@core/errors/safe-errors';
import { sseEvents } from '@core/stream/json-events';
import { ModelChoice } from '@features/platform/inference/model-choice';
import { formatModelName } from '@shared/browser/format';

/** 與後端 `AssistantQuestionValidator` 相同的上限；超過的先在這裡截掉，不讓整個提問被拒。 */
export const ASSISTANT_LIMITS = { question: 500, answer: 4000, turns: 6 } as const;

/** 助理的對話怎麼送出：提問連同所在頁面「目前」的條件送到助理端點。 */
export interface AssistantSource {
  /** 送出提問，回傳 SSE 回應（`grounding`、`tool`、`delta`…、`done`）。 */
  ask(question: AssistantQuestion, signal: AbortSignal): Promise<Response>;
  /** 目前頁面與查詢條件的鍵；換了頁或改了條件，之前的輪次只留在畫面上，不再當成上下文送出。 */
  context(): string;
  /** 可用模型（已依個人的模型政策過濾）。 */
  models(): Promise<ModelsDto>;
  /** 使用者的預設模型；政策允許選擇時優先使用。 */
  preferredModelId(): string | null | undefined;
}

export type AssistantStatus = 'waiting' | 'streaming' | 'done' | 'stopped' | 'failed';

/**
 * 一問一答。`facts` 是伺服器交給模型的依據（畫面資料與每次查詢的結果依序接在一起），回答裡的 `[n]` 就是 `facts[n-1]`；
 * `scopes` 是每份資料的範圍說明，`activity` 是正在執行的查詢（例：「查詢判定過程」）。
 */
export interface AssistantExchange {
  readonly id: number;
  readonly question: string;
  readonly context: string;
  /** 回答的模型名稱；政策不顯示模型名稱時為 null。 */
  readonly model: string | null;
  readonly answer: string;
  readonly scopes: readonly string[];
  readonly activity: string | null;
  readonly facts: readonly AssistantFact[];
  readonly partial: boolean;
  readonly truncated: boolean;
  readonly status: AssistantStatus;
  readonly error: string | null;
}

/**
 * 助理的對話狀態：只在記憶體，重新整理就沒有。模型選擇是自己的一份（`ModelChoice`），不影響對話頁。
 * 同時只有一個提問在跑；先前輪次只送已完成、頁面與條件相同的最近幾輪。
 */
export class AssistantConversation {
  readonly model = new ModelChoice();
  readonly modelsLoading = signal(false);
  readonly modelsError = signal<string | null>(null);
  private readonly turns = signal<readonly AssistantExchange[]>([]);
  readonly exchanges = this.turns.asReadonly();
  /** 目前模型的顯示名稱；政策不顯示模型名稱時為 null。 */
  readonly modelName = computed(() => {
    const model = this.model.current();
    return model && this.model.policy().showModelNames ? formatModelName(model) : null;
  });
  readonly busy = computed(() =>
    this.turns().some((turn) => turn.status === 'waiting' || turn.status === 'streaming'),
  );
  private controller: AbortController | null = null;
  private models: Promise<void> | null = null;
  private sequence = 0;

  constructor(private readonly source: AssistantSource) {}

  /** 第一次開啟面板時載入模型；失敗後再開會重試。 */
  prepare() {
    this.models ??= this.loadModels();
    return this.models;
  }

  async ask(text: string) {
    const question = text.trim().slice(0, ASSISTANT_LIMITS.question);
    if (!question || this.busy()) return;
    const context = this.source.context();
    const history = this.turns()
      .filter((turn) => turn.status === 'done' && turn.context === context)
      .slice(-ASSISTANT_LIMITS.turns)
      .map((turn) => ({
        question: turn.question,
        answer: turn.answer.slice(0, ASSISTANT_LIMITS.answer),
      }));
    const id = ++this.sequence;
    this.turns.update((turns) => [
      ...turns,
      {
        id,
        question,
        context,
        model: null,
        answer: '',
        scopes: [],
        activity: null,
        facts: [],
        partial: false,
        truncated: false,
        status: 'waiting',
        error: null,
      },
    ]);
    const controller = (this.controller = new AbortController());
    try {
      // 輪次先加上去（`busy` 立即成立，連按不會送兩次），模型清單第一次才在這裡等。
      await this.prepare();
      if (controller.signal.aborted) throw new DOMException('已停止', 'AbortError');
      this.patch(id, () => ({ model: this.modelName() }));
      const response = await this.source.ask(
        { text: question, modelId: this.model.modelId() || null, history },
        controller.signal,
      );
      for await (const { event, data } of sseEvents<AssistantEventDto>(response, controller.signal))
        if (event === 'grounding')
          this.patch(id, (turn) => ({
            scopes: data.scope ? [...turn.scopes, data.scope] : turn.scopes,
            facts: [...turn.facts, ...(data.facts ?? [])],
            partial: turn.partial || !!data.partial,
            status: 'streaming',
          }));
        else if (event === 'tool')
          this.patch(id, () => ({ activity: data.tool, status: 'streaming' }));
        else if (event === 'delta' && data.text)
          this.patch(id, (turn) => ({ answer: turn.answer + data.text, activity: null }));
        else if (event === 'done')
          this.patch(id, () => ({ truncated: !!data.truncated, status: 'done', activity: null }));
      // 連線在 `done` 之前結束：留下已收到的部分，標成中斷。
      this.patch(id, (turn) =>
        turn.status === 'done'
          ? {}
          : { status: 'failed', activity: null, error: '回答中斷，請再問一次。' },
      );
    } catch (error) {
      this.patch(id, () =>
        controller.signal.aborted || isCancellation(error)
          ? { status: 'stopped', activity: null }
          : { status: 'failed', activity: null, error: safeMessage(error) },
      );
    } finally {
      if (this.controller === controller) this.controller = null;
    }
  }

  stop() {
    this.controller?.abort();
  }

  /** 清空對話（停止進行中的回答）；模型選擇保留。 */
  reset() {
    this.stop();
    this.turns.set([]);
  }

  private async loadModels() {
    this.modelsLoading.set(true);
    this.modelsError.set(null);
    try {
      this.model.adopt(await this.source.models(), this.source.preferredModelId());
    } catch (error) {
      this.models = null;
      this.modelsError.set(safeMessage(error));
    } finally {
      this.modelsLoading.set(false);
    }
  }

  private patch(id: number, change: (turn: AssistantExchange) => Partial<AssistantExchange>) {
    this.turns.update((turns) =>
      turns.map((turn) => (turn.id === id ? { ...turn, ...change(turn) } : turn)),
    );
  }
}

/**
 * 在元件的注入脈絡建立助理的對話：模型清單取自 `/models`、預設模型取自個人偏好，元件銷毀時停止進行中的回答。
 * 呼叫端只需給送出提問的方式與目前條件的鍵。
 */
export function assistantConversation(
  source: Pick<AssistantSource, 'ask' | 'context'>,
): AssistantConversation {
  const api = inject(WorkspaceApi);
  const session = inject(WorkspaceSession);
  const conversation = new AssistantConversation({
    ...source,
    models: () => api.models(),
    preferredModelId: () => session.me()?.preferences.defaultModelId,
  });
  inject(DestroyRef).onDestroy(() => conversation.stop());
  return conversation;
}
