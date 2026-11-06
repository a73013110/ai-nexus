import { Directive, output } from '@angular/core';

/** 滑鼠一格約 100px；觸控板的捲動量小而連續，累積到這個量才算一步。 */
const STEP_DISTANCE = 60;
/** 走一步後這段時間內的捲動（觸控板的慣性）不再算，免得輕輕一滑就跳好幾步。 */
const STEP_COOLDOWN_MS = 150;

/**
 * 把直向滾輪換成一步一步的前後切換（往下是下一個，送出 1；往上送出 -1），例如日曆換月：
 * `<table (nxWheelStep)="shiftMonth($event)">`。在元素上捲動不會捲動頁面或面板；
 * 按著 Ctrl 是縮放、橫向捲動留給原本的捲動，都不處理。
 */
@Directive({ selector: '[nxWheelStep]', host: { '(wheel)': 'wheel($event)' } })
export class WheelStep {
  readonly nxWheelStep = output<1 | -1>();
  private delta = 0;
  private steppedAt = -Infinity;

  wheel(event: WheelEvent) {
    if (event.ctrlKey || Math.abs(event.deltaY) <= Math.abs(event.deltaX)) return;
    event.preventDefault();
    if (event.timeStamp - this.steppedAt < STEP_COOLDOWN_MS) return;
    const unit = event.deltaMode === WheelEvent.DOM_DELTA_LINE ? 16 : event.deltaMode ? 400 : 1;
    this.delta += event.deltaY * unit;
    if (Math.abs(this.delta) < STEP_DISTANCE) return;
    const step = this.delta > 0 ? 1 : -1;
    this.delta = 0;
    this.steppedAt = event.timeStamp;
    this.nxWheelStep.emit(step);
  }
}
