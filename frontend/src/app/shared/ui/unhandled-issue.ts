import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ClientIssues } from '../../core/api/client-issues';
import { Notice } from './notice';
import { ViewportInset } from '../browser/viewport-inset';

@Component({
  selector: 'nx-unhandled-issue',
  imports: [Notice, ViewportInset],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (issues.notice()) {
    <nx-notice
      nxViewportInset="--global-issue-height"
      class="global-notice"
      [message]="issues.notice()"
      [dismissible]="true"
      (dismissed)="issues.notice.set('')"
    />
  }`,
  styles: `
    :host {
      display: block;
    }
  `,
})
export class UnhandledIssue {
  readonly issues = inject(ClientIssues);
}
