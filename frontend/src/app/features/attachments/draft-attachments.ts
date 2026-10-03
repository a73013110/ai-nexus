import { computed, inject, Injectable, signal } from '@angular/core';
import type { Attachment, AttachmentPolicy } from '../../core/api/types';
import { WorkspaceApi } from '../workspace/workspace-api';

@Injectable({ providedIn: 'root' })
export class DraftAttachments {
  private readonly api = inject(WorkspaceApi);
  readonly files = signal<Attachment[]>([]);
  readonly policy = signal<AttachmentPolicy | null>(null);
  readonly uploading = signal(false);
  readonly error = signal('');
  readonly accept = computed(
    () =>
      this.policy()?.extensions.join(',') ?? '.png,.jpg,.jpeg,.webp,.pdf,.docx,.txt,.md,.csv,.json',
  );
  private controller: AbortController | null = null;
  private version = 0;

  async initialize() {
    const version = this.version;
    const policy = await this.api.attachmentPolicy();
    if (version === this.version) this.policy.set(policy);
  }
  reset(files: Attachment[] = []) {
    this.version++;
    this.controller?.abort();
    this.controller = null;
    this.uploading.set(false);
    this.files.set(files);
    this.error.set('');
  }
  async restore(ids: string[]) {
    const version = this.version;
    const results = await Promise.allSettled(ids.map((id) => this.api.attachment(id)));
    if (version !== this.version) return;
    this.files.set(
      results.flatMap((result) => (result.status === 'fulfilled' ? [result.value] : [])),
    );
    if (results.some((result) => result.status === 'rejected'))
      this.error.set('部分草稿附件已無法使用，請重新上傳。');
  }
  async upload(files: FileList | File[]) {
    if (this.uploading() || !this.policy()) return;
    const policy = this.policy()!;
    const candidates = Array.from(files);
    if (candidates.length + this.files().length > policy.maxFilesPerMessage) {
      this.error.set(`每則提問最多 ${policy.maxFilesPerMessage} 個附件。`);
      return;
    }
    if (
      candidates.reduce(
        (sum, file) => sum + file.size,
        this.files().reduce((sum, file) => sum + file.size, 0),
      ) > policy.maxMessageBytes
    ) {
      this.error.set('這次附件總大小超過上限。');
      return;
    }
    const invalid = candidates.find(
      (file) =>
        file.size > policy.maxFileBytes ||
        !policy.extensions.includes('.' + file.name.split('.').pop()?.toLowerCase()),
    );
    if (invalid) {
      this.error.set(
        `「${invalid.name}」格式不支援或超過 ${Math.round(policy.maxFileBytes / 1024 / 1024)} MB。`,
      );
      return;
    }
    const version = this.version;
    const controller = (this.controller = new AbortController());
    this.uploading.set(true);
    this.error.set('');
    try {
      for (const file of candidates) {
        const uploaded = await this.api.upload(file, controller.signal);
        if (version !== this.version) return;
        this.files.update((current) => [...current, uploaded]);
      }
    } catch (error) {
      if (!controller.signal.aborted)
        this.error.set(error instanceof Error ? error.message : '附件上傳失敗，請重試。');
    } finally {
      if (version === this.version) {
        this.uploading.set(false);
        this.controller = null;
      }
    }
  }
  async remove(id: string, savedInHistory = false) {
    const version = this.version;
    // A reused edit attachment belongs to history; remove it from this draft only.
    try {
      if (!savedInHistory) await this.api.removeAttachment(id);
      if (version !== this.version) return;
      this.files.update((files) => files.filter((file) => file.id !== id));
      this.error.set('');
    } catch (error) {
      if (version !== this.version) return;
      this.error.set(error instanceof Error ? error.message : '無法移除附件。');
    }
  }
}
