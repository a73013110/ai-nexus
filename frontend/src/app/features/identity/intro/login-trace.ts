import { ChangeDetectionStrategy, Component } from '@angular/core';
import { brandPath, brandTips } from '@shared/graphics/brand-logo';

/**
 * 登入訊號所描繪的外框（signal-mark.ts）。標記移入字標後仍會保留，因此品牌區保有其重量；
 * 細緻指標的光會讓它的線條變暖。
 */
@Component({
  selector: 'svg[nxLoginTrace]',
  styleUrl: './login-trace.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { viewBox: '-1 -1 2 2', 'aria-hidden': 'true' },
  template: `<svg:path [attr.d]="outline" />
    @for (tip of tips; track $index) {
      <svg:circle [attr.cx]="tip.x" [attr.cy]="tip.y" r="0.035" />
    }`,
})
export class LoginTrace {
  readonly outline = brandPath;
  readonly tips = brandTips;
}
