import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CopyValue } from '@shared/ui/copy-value';
import type { BadgeTone } from '@shared/ui/count-badge';
import { Icon } from '@shared/ui/icon';
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

/** 詳情抽屜 `drawer-summary` 的內容：狀態、時間、標題與副標。 */
export interface MonitoringSummary {
  tone: BadgeTone;
  status: string;
  /** ISO 時間；沒有觀測時為空。 */
  at: string;
  title: string;
  subtitle: string;
}

/** 依選取的工作階段、子系統或周邊服務組出抽屜摘要；已離開快照的項目只留名稱與說明。 */
export function monitoringSummary(
  selected: OnlineSession | null,
  session: OnlineSession | null,
  subsystemId: string | null,
  subsystem: MonitoredSubsystem | null,
  dependency: DependencyTraffic | null,
): MonitoringSummary | null {
  if (selected) {
    const segment = session ?? selected;
    return {
      tone: session?.state === 'active' ? 'success' : 'neutral',
      status: session ? presenceState(session.state) : '已離線',
      at: session?.lastSeenAt ?? '',
      title: segment.displayName,
      subtitle: segment.account + (segment.testingIdentity ? ' · 測試身分' : ''),
    };
  }
  if (subsystemId)
    return subsystem
      ? {
          tone: toneOf(subsystem.status),
          status: subsystemStatus(subsystem.status),
          at: subsystem.observedAt ?? '',
          title: subsystem.name,
          subtitle: subsystem.summary || '尚未取得這個子系統的狀態。',
        }
      : { tone: 'neutral', status: '已移除', at: '', title: subsystemId, subtitle: '' };
  if (dependency)
    return {
      tone: toneOf(dependency.status),
      status: dependencyStatus(dependency.status),
      at: dependency.lastSeenAt ?? '',
      title: dependency.name,
      subtitle: dependency.kind === 'database' ? 'SQL 資料庫' : 'HTTP 服務',
    };
  return null;
}

/**
 * 監控詳情的內容：主要數據 → 關聯（可複製的識別碼）→ 收合的原始資料。狀態與標題由頁面放在抽屜的
 * `drawer-summary`（`monitoringSummary`），與日誌、稽核的詳情同一骨架。
 */
@Component({
  selector: 'nx-monitoring-inspector',
  imports: [CopyValue, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './monitoring-inspector.html',
  styleUrl: './monitoring-inspector.scss',
})
export class MonitoringInspector {
  readonly selectedSession = input<OnlineSession | null>(null);
  readonly liveSession = input<OnlineSession | null>(null);
  readonly liveDependency = input<DependencyTraffic | null>(null);
  /** 依 id 取得的子系統；從快照中消失後為 null。 */
  readonly subsystemId = input<string | null>(null);
  readonly liveSubsystem = input<MonitoredSubsystem | null>(null);
  readonly bytes = formatBytes;
  readonly time = monitoringTime;
  readonly latency = monitoringLatency;
  readonly featureName = monitoringFeatureNames();
  readonly factText = factText;
  readonly factTone = factTone;
  raw(value: unknown) {
    return JSON.stringify(value, null, 2);
  }
}
