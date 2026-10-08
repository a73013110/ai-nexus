import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  model,
  output,
  signal,
} from '@angular/core';
import { Card } from '../../../shared/ui/card';
import { Icon } from '../../../shared/ui/icon';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { CountBadge } from '../../../shared/ui/count-badge';
import { SearchField } from '../../../shared/ui/search-field';
import { Select } from '../../../shared/ui/select';
import type { OnlineSession } from './monitoring-store';
import { presenceState, monitoringTime, monitoringFeature } from './monitoring-format';

@Component({
  selector: 'nx-monitoring-sessions',
  imports: [Card, Icon, StatusBadge, CountBadge, SearchField, Select],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './monitoring-sessions.html',
  styles: ':host { display: block; min-width: 0; }',
})
export class MonitoringSessions {
  readonly sessions = input.required<OnlineSession[]>();
  readonly timeoutSeconds = input.required<number>();
  readonly selected = output<OnlineSession>();
  readonly feature = model('all');
  readonly query = signal('');
  readonly state = signal('all');
  readonly sort = signal<'name' | 'requests'>('name');
  readonly page = signal(0);
  readonly featureName = monitoringFeature;
  readonly stateName = presenceState;
  readonly time = monitoringTime;
  readonly states = [
    { value: 'all', label: '所有在線狀態' },
    { value: 'active', label: '使用中' },
    { value: 'idle', label: '閒置' },
    { value: 'background', label: '背景分頁' },
  ];
  readonly featureOptions = computed(() => [
    { value: 'all', label: '所有功能' },
    ...[...new Set(this.sessions().map((s) => s.feature))]
      .sort()
      .map((id) => ({ value: id, label: this.featureName(id) })),
  ]);
  readonly filtered = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    return this.sessions()
      .filter(
        (s) =>
          (this.state() === 'all' || s.state === this.state()) &&
          (this.feature() === 'all' || s.feature === this.feature()) &&
          (!query ||
            [s.displayName, s.account, s.address, s.browser, s.device]
              .join(' ')
              .toLocaleLowerCase()
              .includes(query)),
      )
      .sort((a, b) =>
        this.sort() === 'requests'
          ? b.requests - a.requests || a.id.localeCompare(b.id)
          : a.displayName.localeCompare(b.displayName, 'zh-TW') || a.id.localeCompare(b.id),
      );
  });
  readonly lastPage = computed(() => Math.max(0, Math.ceil(this.filtered().length / 10) - 1));
  readonly currentPage = computed(() => Math.min(this.page(), this.lastPage()));
  readonly visible = computed(() =>
    this.filtered().slice(this.currentPage() * 10, this.currentPage() * 10 + 10),
  );

  constructor() {
    effect(() => {
      this.feature();
      this.page.set(0);
    });
  }
  setFilter(kind: 'query' | 'state' | 'feature', value: string) {
    this[kind].set(value);
    this.page.set(0);
  }
  showSession(session: OnlineSession) {
    this.selected.emit(session);
  }
}
