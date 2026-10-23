import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * 確定沒有資料時的空狀態，標題、說明與動作由功能投影提供。不是載入容器：讀取中用骨架
 * （首次載入）或 `.is-refreshing`（重讀），不先顯示這裡的「沒有…」。
 */
@Component({
  selector: 'nx-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-empty-state', role: 'status' },
  styleUrl: './empty-state.scss',
  template: `<ng-content />`,
})
export class EmptyState {}
