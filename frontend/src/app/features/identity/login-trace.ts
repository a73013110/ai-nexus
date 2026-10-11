import { ChangeDetectionStrategy, Component } from '@angular/core';
import { nexusPath, nexusTips } from '../../shared/graphics/nexus-logo';

/**
 * The outline the sign-in signals trace (signal-mark.ts). It stays after the mark has moved into
 * the wordmark, so the brand side keeps its weight; a fine pointer's light warms its line.
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
  readonly outline = nexusPath;
  readonly tips = nexusTips;
}
