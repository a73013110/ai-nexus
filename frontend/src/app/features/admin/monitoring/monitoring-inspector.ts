import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon } from '../../../shared/ui/icon';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { formatBytes } from '../../../shared/browser/format';
import type { OnlineSession, DependencyTraffic } from './monitoring-store';
import {
  presenceState,
  dependencyStatus,
  monitoringTime,
  monitoringLatency,
  monitoringFeature,
} from './monitoring-format';

@Component({
  selector: 'nx-monitoring-inspector',
  imports: [Icon, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './monitoring-inspector.html',
  styleUrl: './monitoring-inspector.scss',
})
export class MonitoringInspector {
  readonly selectedSession = input<OnlineSession | null>(null);
  readonly liveSession = input<OnlineSession | null>(null);
  readonly liveDependency = input<DependencyTraffic | null>(null);
  readonly bytes = formatBytes;
  readonly stateName = presenceState;
  readonly dependencyStatus = dependencyStatus;
  readonly time = monitoringTime;
  readonly latency = monitoringLatency;
  readonly featureName = monitoringFeature;
}
