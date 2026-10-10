import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ClientIssues } from './client-issues';
import { Notice } from '../../shared/ui/notice';
import { ViewportInset } from '../../shared/browser/viewport-inset';

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
