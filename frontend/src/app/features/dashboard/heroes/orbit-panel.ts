import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../../../shared/ui/icon';
import { StatusBadge } from '../../../shared/ui/status-badge';

export interface OrbitDetail {
  title: string;
  icon: string;
  stateText: string;
  tone: 'danger' | 'info' | 'neutral';
  metrics: { key: string; label: string; text: string }[];
  link: { path: string; query: Record<string, string> } | null;
}

/** The selected feature's figures beside the star map. */
@Component({
  selector: 'nx-orbit-panel',
  imports: [Icon, RouterLink, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './orbit-panel.scss',
  host: { 'aria-live': 'polite' },
  template: `<header>
      <nx-icon [name]="detail().icon" />
      <h3>{{ detail().title }}</h3>
      <nx-status-badge [tone]="detail().tone">{{ detail().stateText }}</nx-status-badge>
    </header>
    <dl>
      @for (m of detail().metrics; track m.key) {
        <div>
          <dt>{{ m.label }}</dt>
          <dd>{{ m.text }}</dd>
        </div>
      }
    </dl>
    @if (detail().link; as link) {
      <a [routerLink]="link.path" [queryParams]="link.query"
        >前往{{ detail().title }}<nx-icon name="chevron"
      /></a>
    }`,
})
export class OrbitPanel {
  readonly detail = input.required<OrbitDetail>();
}
