import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon } from '@shared/ui/icon';
import { StatusBadge } from '@shared/ui/status-badge';
import { formatBytes } from '@shared/browser/format';
import type { DependencyTraffic, MonitoredSubsystem, OnlineSession } from '@core/api/schema';
import {
  presenceState,
  dependencyStatus,
  monitoringTime,
  monitoringLatency,
  monitoringFeatureNames,
  subsystemStatus,
  factText,
  factTone,
  toneOf,
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
  /** A subsystem by id; null once it is gone from the snapshot. */
  readonly subsystemId = input<string | null>(null);
  readonly liveSubsystem = input<MonitoredSubsystem | null>(null);
  readonly bytes = formatBytes;
  readonly stateName = presenceState;
  readonly dependencyStatus = dependencyStatus;
  readonly time = monitoringTime;
  readonly latency = monitoringLatency;
  readonly featureName = monitoringFeatureNames();
  readonly subsystemStatus = subsystemStatus;
  readonly factText = factText;
  readonly factTone = factTone;
  readonly tone = toneOf;
}
