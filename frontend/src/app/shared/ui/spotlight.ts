import { DestroyRef, Directive, ElementRef, inject } from '@angular/core';

/**
 * 追蹤表面上的指標：寫入 `--spot-x`／`--spot-y`（像素）與 `data-spot`，讓表面能在游標下繪製光線。
 * 樣式屬於宿主；這裡只
 * 每幀量測一次。觸控時表面不亮起。
 */
@Directive({ selector: '[nxSpotlight]' })
export class Spotlight {
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private frame = 0;
  private point: PointerEvent | null = null;

  constructor() {
    const move = (event: PointerEvent) => {
      if (event.pointerType === 'touch') return;
      this.point = event;
      this.frame ||= requestAnimationFrame(() => this.paint());
    };
    const leave = () => {
      cancelAnimationFrame(this.frame);
      this.frame = 0;
      this.point = null;
      delete this.element.dataset['spot'];
    };
    this.element.addEventListener('pointermove', move, { passive: true });
    this.element.addEventListener('pointerleave', leave, { passive: true });
    inject(DestroyRef).onDestroy(() => {
      cancelAnimationFrame(this.frame);
      this.element.removeEventListener('pointermove', move);
      this.element.removeEventListener('pointerleave', leave);
    });
  }

  private paint() {
    this.frame = 0;
    const event = this.point;
    if (!event) return;
    const box = this.element.getBoundingClientRect();
    const pointX = event.clientX - box.left,
      pointY = event.clientY - box.top;
    this.element.dataset['spot'] = '';
    this.element.style.setProperty('--spot-x', `${pointX}px`);
    this.element.style.setProperty('--spot-y', `${pointY}px`);
  }
}
