import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FeaturePage } from '../../../shared/ui/feature-page';
import { WorkspaceSession } from '../../../core/auth/workspace-session';
import { ViewScope } from '../../../shared/browser/view-scope';
import { IssueCode } from '../../../shared/ui/issue-code';
import { safeMessage } from '../../../core/api/safe-errors';
import { formatBytes, formatDate } from '../../../shared/browser/format';
import {
  SystemLogsApi,
  LogEntry,
  LogDetail,
  LogFilter,
  LogPage,
  LogHealth,
} from './system-logs-api';

function localInput(date: Date) {
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
}
@Component({
  selector: 'nx-system-logs-page',
  imports: [FeaturePage, FormsModule, RouterLink, IssueCode],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './system-logs-page.html',
  styleUrl: './system-logs-page.scss',
})
export class SystemLogsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(SystemLogsApi);
  private readonly view = inject(ViewScope);
  readonly page = signal<LogPage | null>(null);
  readonly detail = signal<LogDetail | null>(null);
  readonly related = signal<LogEntry[]>([]);
  readonly health = signal<LogHealth | null>(null);
  readonly loading = signal(false);
  readonly detailLoading = signal(false);
  readonly exporting = signal(false);
  readonly error = signal('');
  readonly detailError = signal('');
  readonly timezone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  readonly date = formatDate;
  readonly bytes = formatBytes;
  readonly fromLocal = signal(localInput(new Date(Date.now() - 24 * 60 * 60_000)));
  readonly toLocal = signal(localInput(new Date(Date.now() + 60_000)));
  filter: Omit<LogFilter, 'from' | 'to'> = {
    level: '',
    category: '',
    eventName: '',
    issueCode: '',
    traceId: '',
    jobId: '',
    runId: '',
    errorCode: '',
    instance: '',
    text: '',
  };
  private submitted: LogFilter | null = null;
  private controller?: AbortController;
  private detailController?: AbortController;
  private version = 0;
  private detailVersion = 0;
  readonly properties = computed(() => {
    try {
      return JSON.stringify(JSON.parse(this.detail()?.propertiesJson ?? '{}'), null, 2);
    } catch {
      return '{}';
    }
  });
  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.controller?.abort();
      this.detailController?.abort();
    });
    void this.initialize();
  }
  private async initialize() {
    try {
      const guard = this.view.guard();
      await this.session.load();
      if (guard() && this.session.has('logs.query')) await this.search();
    } catch (error) {
      this.error.set(safeMessage(error));
    }
  }
  async search(cursor?: string | null) {
    this.controller?.abort();
    const controller = (this.controller = new AbortController());
    const version = ++this.version;
    const guard = this.view.guard();
    this.loading.set(true);
    this.error.set('');
    this.page.set(null);
    try {
      if (!cursor) {
        this.submitted = {
          ...this.filter,
          from: new Date(this.fromLocal()).toISOString(),
          to: new Date(this.toLocal()).toISOString(),
        };
        this.detail.set(null);
        this.related.set([]);
      }
      const page = await this.api.list(this.submitted!, cursor, controller.signal);
      if (guard() && version === this.version) {
        this.page.set(page);
        this.health.set(page.health ?? null);
      }
    } catch (error) {
      if (!controller.signal.aborted && guard() && version === this.version)
        this.error.set(safeMessage(error));
    } finally {
      if (guard() && version === this.version) this.loading.set(false);
    }
  }
  async inspect(entry: LogEntry) {
    if (!entry.logId || !this.session.has('logs.detail')) return;
    this.detailController?.abort();
    const controller = (this.detailController = new AbortController());
    const version = ++this.detailVersion;
    const guard = this.view.guard();
    this.detailLoading.set(true);
    this.detailError.set('');
    this.detail.set(null);
    this.related.set([]);
    try {
      const detail = await this.api.detail(entry.logId, controller.signal);
      const filter = {
        ...this.submitted!,
        issueCode: '',
        category: '',
        level: '',
        eventName: '',
        errorCode: '',
        instance: '',
        text: '',
        traceId: entry.traceId ?? '',
        jobId: entry.jobId ?? '',
        runId: entry.runId ?? '',
      };
      const page =
        entry.traceId || entry.jobId || entry.runId
          ? await this.api.list(filter, undefined, controller.signal)
          : null;
      if (guard() && version === this.detailVersion) {
        this.detail.set(detail);
        this.related.set(page ? [...(page.events ?? [])].reverse() : [entry]);
      }
    } catch (error) {
      if (!controller.signal.aborted && guard() && version === this.detailVersion)
        this.detailError.set(safeMessage(error));
    } finally {
      if (guard() && version === this.detailVersion) this.detailLoading.set(false);
    }
  }
  async refreshHealth() {
    const guard = this.view.guard();
    try {
      const health = await this.api.health();
      if (guard()) this.health.set(health);
    } catch (error) {
      if (guard()) this.error.set(safeMessage(error));
    }
  }
  async export() {
    if (!this.submitted || !this.session.has('logs.export')) return;
    const guard = this.view.guard();
    this.exporting.set(true);
    this.error.set('');
    try {
      const response = await this.api.export(this.submitted);
      const blob = await response.blob();
      if (!guard()) return;
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'ai-nexus-logs.csv';
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      if (guard()) this.error.set(safeMessage(error));
    } finally {
      if (guard()) this.exporting.set(false);
    }
  }
}
