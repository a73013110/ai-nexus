import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { apiResource } from '../../core/api/api-resource';
import type { RepositoryDetailDto, StartRepositoryChatRequest } from '../../core/api/schema';
import { ConversationDraftTransfer } from '../../core/preferences/conversation-draft-transfer';
import { RepositoriesApi, type RepositoryKey } from './repositories-api';

export const isSha = (value: string) => /^(?:[\da-f]{40}|[\da-f]{64})$/i.test(value);
export const shortSha = (sha: string) => sha.slice(0, 7);

/**
 * The open repository, shared by its views: what it is, which ref is selected and the commit every
 * read pins to, so moving between folders and files never mixes in a newer push.
 */
@Injectable()
export class RepositoryStore {
  private readonly api = inject(RepositoriesApi);
  private readonly router = inject(Router);
  private readonly transfer = inject(ConversationDraftTransfer);
  readonly key = signal<RepositoryKey | null>(null);
  /** The `ref` query: a branch, a tag or a commit; empty means the default branch. */
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
    return [...detail.branches, ...detail.tags].find((x) => x.name === ref)?.commit ?? '';
  });
  /** A ref in the URL that the repository does not have (deleted branch, typo). */
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
  /** Router commands for a view of this repository. */
  link(...segments: (string | number)[]) {
    const key = this.key();
    return key
      ? ['/repositories', key.owner, key.name, ...segments.map(String)]
      : ['/repositories'];
  }
  /** Bundles the source into one attachment of a new conversation and opens it with a draft. */
  async chat(body: StartRepositoryChatRequest, valid: () => boolean) {
    const key = this.key();
    if (!key) return null;
    const result = await this.api.chat(key, body);
    if (!valid()) return null;
    this.transfer.put(result.conversation.id, result.prompt, [result.attachment]);
    await this.router.navigate(['/chat', result.conversation.id]);
    return result;
  }
}
