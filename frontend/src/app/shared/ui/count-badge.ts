import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type BadgeTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

/** A decorative count by default; the parent control supplies its contextual accessible name. */
@Component({
  selector: 'nx-count-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[hidden]': '!visible()',
    '[attr.data-tone]': 'tone()',
    '[attr.data-size]': 'size()',
    '[class.is-overlay]': 'overlay()',
    '[class.is-dot]': 'dot()',
    '[attr.role]': 'label() ? "img" : null',
    '[attr.aria-label]': 'label() || null',
    '[attr.aria-hidden]': 'label() ? null : "true"',
  },
  template: `@if (!dot()) {
    {{ text() }}
  }`,
  styleUrl: './count-badge.scss',
})
export class CountBadge {
  readonly count = input.required<number>();
  readonly max = input(99);
  readonly tone = input<BadgeTone>('info');
  readonly size = input<'sm' | 'md'>('sm');
  readonly overlay = input(false);
  readonly dot = input(false);
  readonly showZero = input(false);
  readonly label = input('');
  readonly value = computed(() =>
    Number.isFinite(this.count()) ? Math.max(0, Math.trunc(this.count())) : 0,
  );
  readonly visible = computed(() => this.value() > 0 || this.showZero());
  readonly text = computed(() => {
    const max = Number.isFinite(this.max()) ? Math.max(1, Math.trunc(this.max())) : 99;
    return this.value() > max ? `${max.toLocaleString()}+` : this.value().toLocaleString();
  });
}
