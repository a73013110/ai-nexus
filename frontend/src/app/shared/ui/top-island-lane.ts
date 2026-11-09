import { Injectable, signal } from '@angular/core';

/**
 * 頂端中央只放得下一座島。臨時的提示島（例：鎖定倒數）在這裡登記自己佔用的寬度，
 * 常駐的島（`FloatingPanel` 的 `docked`）碰到就讓到旁邊，提示結束再回原位。
 */
@Injectable({ providedIn: 'root' })
export class TopIslandLane {
  /** 佔用的寬度（px）；0 表示沒有人佔用。 */
  readonly width = signal(0);
}
