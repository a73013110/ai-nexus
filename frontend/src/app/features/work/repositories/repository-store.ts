import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { RepositoryDetailDto, StartRepositoryChatRequest } from '@core/api/schema';
import { ConversationDraftTransfer } from '@core/preferences/conversation-draft-transfer';
import { RepositoriesApi, type RepositoryKey } from './repositories-api';
import { routeOf } from '@core/api/catalog';

export const isSha = (value: string) => /^(?:[\da-f]{40}|[\da-f]{64})$/i.test(value);
export const shortSha = (sha: string) => sha.slice(0, 7);

/**
 * 已開啟的儲存庫，由其視圖共用：它是什麼、選取了哪個 ref，以及每次讀取所釘住的提交，
 * 因此在資料夾與檔案之間移動時，不會混入較新的推送。
 */
@Injectable()
export class RepositoryStore {
  private readonly api = inject(RepositoriesApi);
  private readonly router = inject(Router);
  private readonly transfer = inject(ConversationDraftTransfer);
  readonly key = signal<RepositoryKey | null>(null);
  /** `ref` 查詢參數：分支、標籤或提交；空白代表預設分支。 */
  readonly ref = signal('');
  readonly fullName = computed(() => {
    const key = this.key();
    return key ? `${key.owner}/${key.name}` : '';
  });
  private readonly detailRead = apiResource({
    feature: 'repositories',
    params: () => this.key() ?? undefined,
    loader: async (key) => ({ name: `${key.owner}/${key.name}`, value: await this.api.read(key) }),
  });
  readonly detail = computed<RepositoryDetailDto | null>(() => {
    const read = this.detailRead.value();
    return read && read.name === this.fullName() ? read.value : null;
  });
  readonly loading = computed(() => this.detailRead.loading() && !this.detail());
  readonly error = this.detailRead.error;
  readonly refName = computed(() => this.ref() || this.detail()?.repository.defaultBranch || '');
  readonly commit = computed(() => {
    const detail = this.detail(),
      ref = this.ref();
    if (!detail) return '';
    if (!ref) return detail.commit ?? '';
    if (isSha(ref)) return ref.toLowerCase();
    return [...detail.branches, ...detail.tags].find((item) => item.name === ref)?.commit ?? '';
  });
  /** 網址中的 ref 不存在於此儲存庫（分支已刪除或打錯字）。 */
  readonly refMissing = computed(() => !!this.detail() && !!this.ref() && !this.commit());

  reload() {
    this.detailRead.reload();
  }
  setPinned(pinned: boolean) {
    this.detailRead.value.update((read) =>
      read
        ? { ...read, value: { ...read.value, repository: { ...read.value.repository, pinned } } }
        : read,
    );
  }
  /** 此儲存庫視圖的路由指令。 */
  link(...segments: (string | number)[]) {
    const key = this.key();
    return key
      ? [routeOf('repositories'), key.owner, key.name, ...segments.map(String)]
      : [routeOf('repositories')];
  }
  /** 把原始碼打包成新對話的一個附件，並以草稿開啟。 */
  async chat(body: StartRepositoryChatRequest, valid: () => boolean) {
    const key = this.key();
    if (!key) return null;
    const result = await this.api.chat(key, body);
    if (!valid()) return null;
    this.transfer.put(result.conversation.id, result.prompt, [result.attachment]);
    await this.router.navigate([routeOf('chat'), result.conversation.id]);
    return result;
  }
}
