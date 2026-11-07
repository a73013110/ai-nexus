import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { ApiClientDto, IssuedApiClientKeyDto } from '@core/api/schema';
import { STALE_WRITE_MESSAGE } from '@core/errors/safe-errors';
import { ACTION_SKIPPED, action, type ActionOutcome } from '@shared/browser/action';
import { ToastService } from '@shared/ui/toast-service';
import { IdentityApi } from '../identity-api';

/**
 * 外部系統頁提供（`providers`）的狀態：系統清單與寫入。寫入成功或失敗都重讀，讓列上的版本回到伺服器的值；
 * 重讀完之前列上的版本是舊的，所以 `busy` 期間停用所有帶版本的動作。
 */
@Injectable()
export class ApiClientsStore {
  private readonly api = inject(IdentityApi);
  private readonly toast = inject(ToastService);
  private readonly read = apiResource({
    feature: 'identity.api-clients',
    loader: (_, signal) => this.api.apiClients(signal),
  });
  readonly clients = computed(() => this.read.value());
  readonly loading = this.read.loading;
  readonly refreshing = this.read.refreshing;
  readonly readError = this.read.error;
  private readonly writing = action();
  readonly busy = computed(() => this.writing.pending() || this.read.refreshing());
  /** 列上的動作（重新產生、刪除）失敗或版本衝突：頁內 Notice；對話框裡的失敗留在對話框。 */
  readonly notice = signal('');

  /** 按下動作的那一列（`client:<id>`）轉圈，其他列照 `busy` 停用。 */
  working(key: string) {
    return this.writing.pendingFor(key);
  }
  reload() {
    this.read.reload();
  }
  /** 送出的版本已過期：對話框已關閉，由頁面說明並重讀。 */
  stale() {
    this.notice.set(STALE_WRITE_MESSAGE);
    this.read.reload();
  }

  create(name: string) {
    return this.write(() => this.api.createApiClient(name), `已新增外部系統「${name}」`);
  }
  rename(client: ApiClientDto, name: string) {
    return this.write(
      () => this.api.renameApiClient(client.id, name, client.rowVersion),
      `已將外部系統改名為「${name}」`,
    );
  }
  reissue(client: ApiClientDto): Promise<ActionOutcome<IssuedApiClientKeyDto>> {
    return this.write(
      () => this.api.reissueApiClientKey(client.id, client.rowVersion),
      `已重新產生「${client.name}」的金鑰`,
      `client:${client.id}`,
    );
  }
  remove(client: ApiClientDto) {
    return this.write(
      () => this.api.deleteApiClient(client.id, client.rowVersion),
      `已刪除外部系統「${client.name}」`,
      `client:${client.id}`,
    );
  }

  private async write<T>(run: () => Promise<T>, done: string, key = '') {
    if (this.busy()) return ACTION_SKIPPED;
    this.notice.set('');
    const outcome = await this.writing.run(run, { key });
    if (outcome.ok) this.toast.success(done);
    if (outcome.ok || outcome.error) this.read.reload();
    return outcome;
  }
}
