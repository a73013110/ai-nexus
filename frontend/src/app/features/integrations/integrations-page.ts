import { Notice } from '../../shared/ui/notice';
import { Card } from '../../shared/ui/card';
import { EmptyState } from '../../shared/ui/empty-state';
import { FilterPanel } from '../../shared/ui/filter-panel';
import { Field } from '../../shared/ui/field';
import { ClientValidationError } from '../../core/api/safe-errors';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ConversationDraftTransfer } from '../../core/preferences/conversation-draft-transfer';
import type { ExternalSource, SourceRecord, SourceDetail } from '../../core/api/types';
import { FeaturePage } from '../../shared/ui/feature-page';
import { Select } from '../../shared/ui/select';
import { Icon } from '../../shared/ui/icon';
import { MarkdownView } from '../../shared/ui/markdown-view';
import { ViewScope } from '../../shared/browser/view-scope';
import { formatDate } from '../../shared/browser/format';
import { IntegrationsApi } from './integrations-api';

@Component({
  selector: 'nx-integrations-page',
  imports: [Notice, Card, EmptyState, FilterPanel, Field, FeaturePage, Select, Icon, MarkdownView],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './integrations-page.html',
})
export class IntegrationsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(IntegrationsApi);
  private readonly scope = inject(ViewScope);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly transfer = inject(ConversationDraftTransfer);
  readonly sources = signal<ExternalSource[]>([]);
  readonly current = signal<ExternalSource | null>(null);
  readonly rows = signal<SourceRecord[]>([]);
  readonly detail = signal<SourceDetail | null>(null);
  readonly query = signal('');
  readonly kind = signal('all');
  readonly loading = signal(true);
  readonly searching = signal(false);
  readonly reading = signal(false);
  readonly busy = signal(false);
  readonly searched = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly kindOptions = computed(() => [
    { value: 'all', label: '全部類型' },
    ...(this.current()?.kinds ?? []).map((x) => ({ value: x, label: this.label(x) })),
  ]);
  private revision = 0;
  private readSequence = 0;
  constructor() {
    void this.load();
  }
  private guard() {
    const revision = this.revision,
      valid = this.scope.guard();
    return () => valid() && revision === this.revision;
  }
  async load() {
    const valid = this.scope.guard();
    try {
      await this.session.load();
      if (!valid() || !this.session.me()) return;
      if (!this.session.has('integrations')) throw new ClientValidationError('featureAccess');
      const sources = await this.api.list();
      if (!valid()) return;
      this.sources.set(sources);
      const chosen =
        sources.find((x) => x.id === this.route.snapshot.queryParamMap.get('source')) ?? sources[0];
      if (chosen) {
        this.select(chosen);
        const id = this.route.snapshot.queryParamMap.get('record');
        if (id && chosen.canQuery) await this.read(id);
      }
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  select(source: ExternalSource) {
    if (this.busy()) return;
    ++this.revision;
    ++this.readSequence;
    this.current.set(source);
    this.rows.set([]);
    this.detail.set(null);
    this.query.set('');
    this.kind.set('all');
    this.searched.set(false);
    this.error.set('');
    this.notice.set('');
    this.searching.set(false);
    this.reading.set(false);
  }
  async search(event: Event) {
    event.preventDefault();
    const source = this.current(),
      valid = this.guard();
    if (!source?.canQuery || this.searching() || this.query().trim().length < 2) return;
    ++this.readSequence;
    this.detail.set(null);
    this.reading.set(false);
    this.searching.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      const rows = await this.api.search(source.id, this.query(), this.kind());
      if (!valid()) return;
      this.rows.set(rows);
      this.searched.set(true);
      if (rows.length === 1) await this.read(rows[0].id);
    } catch (e) {
      if (valid()) {
        this.rows.set([]);
        this.error.set(this.scope.message(e));
      }
    } finally {
      if (valid()) this.searching.set(false);
    }
  }
  async read(id: string, check = false) {
    if (this.busy()) return;
    const source = this.current(),
      sequence = ++this.readSequence,
      alive = this.guard();
    if (!source?.canQuery) return;
    const valid = () => alive() && sequence === this.readSequence;
    this.reading.set(!check);
    if (!check) {
      this.detail.set(null);
      this.error.set('');
    }
    try {
      const detail = await this.api.get(source.id, id);
      if (!valid()) return;
      this.detail.set(detail);
      this.scope.later(
        () => {
          if (valid() && document.visibilityState === 'visible') void this.read(id, true);
          else if (valid())
            this.scope.later(
              () => {
                if (valid()) void this.read(id, true);
              },
              30000,
              'source-check',
            );
        },
        30000,
        'source-check',
      );
    } catch (e) {
      if (valid()) {
        this.detail.set(null);
        this.error.set(this.scope.message(e));
      }
    } finally {
      if (valid()) this.reading.set(false);
    }
  }
  async use(action: 'import' | 'chat') {
    const detail = this.detail(),
      source = this.current(),
      valid = this.guard();
    if (!detail || !source || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      if (action === 'import') {
        const artifact = await this.api.import(source.id, detail.record.id, detail.record.revision);
        if (valid()) await this.router.navigate(['/artifacts', artifact.resource.id]);
      } else {
        const next = await this.api.chat(source.id, detail.record.id, detail.record.revision);
        if (valid()) {
          this.transfer.put(next.conversation.id, next.prompt);
          await this.router.navigate(['/chat', next.conversation.id]);
        }
      }
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  label(kind: string) {
    const names: Record<string, string> = {
      document: '公文',
      reference: '規範／表單',
      organization: '單位資料',
      approved: '核准',
      returned: '退回',
      version: '版本更新',
      created: '建立',
    };
    return names[kind] ?? kind;
  }
  readonly date = formatDate;
}
