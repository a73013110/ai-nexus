import { ChangeDetectionStrategy, Component, ViewEncapsulation } from '@angular/core';

/** Shared empty/loading layout, with headings, descriptions and actions projected by the feature. */
@Component({
  selector: 'nx-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-empty-state', role: 'status' },
  encapsulation: ViewEncapsulation.None,
  styleUrl: './empty-state.scss',
  template: `<ng-content />`,
})
export class EmptyState {}
