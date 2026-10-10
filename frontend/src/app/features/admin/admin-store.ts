import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '../../core/api/api-resource';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ClientValidationError } from '../../core/errors/safe-errors';
import { ViewScope } from '../../shared/browser/view-scope';
import { AdminApi } from './admin-api';

/** The catalog and user list shared by the management tabs and editors of one admin page. */
@Injectable()
export class AdminStore {
  private readonly api = inject(AdminApi);
  private readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  /** The account is read fresh: this page changes what it may do. */
  private readonly catalogRead = apiResource({
    freshAccount: true,
    loader: () => {
      if (!this.session.has('admin')) throw new ClientValidationError('adminAccess');
      return this.api.catalog();
    },
  });
  readonly catalog = computed(() => this.catalogRead.value() ?? null);
  private readonly ready = computed(() => !!this.catalog());
  readonly search = signal('');
  private readonly query = signal('');
  private readonly typing = signal(false);
  readonly offset = signal(0);
  readonly usersRead = apiResource({
    params: () => (this.ready() ? { search: this.query(), offset: this.offset() } : undefined),
    loader: ({ search, offset }) => this.api.users(search, offset),
  });
  readonly users = computed(() => this.usersRead.value() ?? null);
  readonly loading = this.catalogRead.loading;
  readonly loadingUsers = computed(() => this.typing() || this.usersRead.refreshing());
  readonly actionError = signal('');
  readonly error = computed(
    () => this.actionError() || this.catalogRead.error() || this.usersRead.error(),
  );
  readonly notice = signal('');

  load() {
    this.actionError.set('');
    this.catalogRead.reload();
    this.usersRead.reload();
  }
  find(value: string) {
    this.search.set(value);
    this.typing.set(true);
    this.scope.later(
      () => {
        this.typing.set(false);
        this.offset.set(0);
        this.query.set(value);
      },
      250,
      'admin-users-search',
    );
  }
  /** Show the user page at `offset`, reading it again when it is already shown. */
  readUsers(offset = this.offset()) {
    if (offset === this.offset()) this.usersRead.reload();
    else this.offset.set(offset);
  }
  /** Authorization changed: read the catalog, the shown users and this account's grants again. */
  saved(notice: string) {
    this.notice.set(notice);
    this.catalogRead.reload();
    this.usersRead.reload();
    void this.session.load(true);
  }
  roleNames(ids: string[]) {
    return ids.map((id) => this.catalog()?.roles.find((x) => x.id === id)?.name || id);
  }
  groupNames(ids: string[]) {
    return ids.map((id) => this.catalog()?.groups.find((x) => x.id === id)?.name || id);
  }
}
