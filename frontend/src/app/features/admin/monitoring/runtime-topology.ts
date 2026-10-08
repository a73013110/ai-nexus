import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Icon } from '../../../shared/ui/icon';
import { GraphNode } from '../../../shared/ui/graph-node';
import type { DependencyTraffic, MonitoringSnapshot, OnlineSession } from './monitoring-store';
import { dependencyStatus, presenceState } from './monitoring-format';

@Component({
  selector: 'nx-runtime-topology',
  imports: [Icon, GraphNode],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './runtime-topology.html',
  styleUrl: './runtime-topology.scss',
})
export class RuntimeTopology {
  readonly snapshot = input.required<MonitoringSnapshot>();
  readonly moving = input(true);
  readonly sessionSelected = output<OnlineSession>();
  readonly dependencySelected = output<DependencyTraffic>();
  readonly status = dependencyStatus;
  readonly state = presenceState;
  readonly clients = computed(() => {
    const users = new Map<string, { session: OnlineSession; count: number }>();
    for (const session of this.snapshot().sessions) {
      const user = users.get(session.userId);
      if (user) {
        user.count++;
        if (session.state === 'active' && user.session.state !== 'active') user.session = session;
      } else users.set(session.userId, { session, count: 1 });
    }
    return [...users.values()].slice(0, 6);
  });
  readonly dependencies = computed(() =>
    this.snapshot()
      .dependencies.filter((d) => !d.id.endsWith('.other') || d.lastSeenAt)
      .slice(0, 6),
  );
  y(index: number, count: number) {
    return count === 1 ? 50 : 14 + (index * 72) / (count - 1);
  }
  clientPath(index: number) {
    return `M 230 ${this.y(index, this.clients().length) * 4.2} C 350 ${this.y(index, this.clients().length) * 4.2}, 340 210, 436 210`;
  }
  dependencyPath(index: number) {
    return `M 564 210 C 660 210, 650 ${this.y(index, this.dependencies().length) * 4.2}, 770 ${this.y(index, this.dependencies().length) * 4.2}`;
  }
  initial(name: string) {
    return [...name].slice(0, 2).join('');
  }
}
