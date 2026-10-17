import { ChangeDetectionStrategy, Component } from '@angular/core';

/** 共用的空狀態／載入版面，標題、說明與動作由功能投影提供。 */
@Component({
  selector: 'nx-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-empty-state', role: 'status' },
  styleUrl: './empty-state.scss',
  template: `<ng-content />`,
})
export class EmptyState {}
