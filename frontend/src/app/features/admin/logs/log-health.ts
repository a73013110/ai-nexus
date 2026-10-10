import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  linkedSignal,
  signal,
} from '@angular/core';
import { apiResource } from '../../../core/api/api-resource';
import type { DiagnosticHealthDto } from '../../../core/api/schema';
import { formatBytes, formatDate } from '../../../shared/browser/format';
import { Disclosure } from '../../../shared/ui/disclosure';
import { Icon } from '../../../shared/ui/icon';
import { Notice } from '../../../shared/ui/notice';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { SystemLogsApi } from './system-logs-api';

/** Health of the log pipeline, as reported with each page or read on request. */
@Component({
  selector: 'nx-log-health',
  imports: [Disclosure, Icon, Notice, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './log-health.scss',
  templateUrl: './log-health.html',
})
export class LogHealth {
  private readonly api = inject(SystemLogsApi);
  /** Health reported with the latest page of events. */
  readonly reported = input<DiagnosticHealthDto | null>(null);
  private readonly requests = signal(0);
  private readonly read = apiResource({
    feature: 'logs.query',
    params: () => this.requests() || undefined,
    loader: () => this.api.health(),
  });
  /** The newer of the reported and the requested health. */
  readonly health = linkedSignal<
    { reported: DiagnosticHealthDto | null; fresh?: DiagnosticHealthDto },
    DiagnosticHealthDto | null
  >({
    source: () => ({ reported: this.reported(), fresh: this.read.value() }),
    computation: (source, previous) =>
      source.fresh && source.fresh !== previous?.source.fresh
        ? source.fresh
        : previous && source.reported === previous.source.reported
          ? previous.value
          : source.reported,
  });
  readonly refreshing = this.read.refreshing;
  readonly error = this.read.error;
  readonly date = formatDate;
  readonly bytes = formatBytes;
  refresh() {
    if (!this.refreshing()) this.requests.update((value) => value + 1);
  }
}
