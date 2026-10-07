import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { ClientIssues } from '../../core/api/client-issues';
import { IssueCode } from './issue-code';

@Component({
  selector: 'nx-unhandled-issue',
  imports: [IssueCode],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (issues.notice()) {
    <div #banner class="error-banner" role="alert">
      <span>{{ issues.notice() }}</span
      ><nx-issue-code [message]="issues.notice()" /><button
        class="secondary-button"
        (click)="issues.notice.set('')"
      >
        關閉提示
      </button>
    </div>
  }`,
  styles: `
    :host {
      display: block;
    }
    .error-banner {
      flex-wrap: wrap;
      max-height: 40dvh;
      overflow: auto;
      padding: var(--p-space-3) var(--p-space-4);
    }
    .error-banner > span {
      min-width: min(100%, 16rem);
      overflow-wrap: anywhere;
    }
    button {
      min-height: var(--button-target);
    }
  `,
})
export class UnhandledIssue {
  readonly issues = inject(ClientIssues);
  private readonly banner = viewChild<ElementRef<HTMLElement>>('banner');
  constructor() {
    effect((onCleanup) => {
      const element = this.banner()?.nativeElement;
      if (!element) {
        document.documentElement.style.removeProperty('--global-issue-height');
        return;
      }
      let frame = 0;
      const measure = () => {
        const height = element.getBoundingClientRect().height + 'px';
        if (document.documentElement.style.getPropertyValue('--global-issue-height') !== height)
          document.documentElement.style.setProperty('--global-issue-height', height);
      };
      measure();
      const observer = new ResizeObserver(() => {
        cancelAnimationFrame(frame);
        frame = requestAnimationFrame(measure);
      });
      observer.observe(element);
      onCleanup(() => {
        observer.disconnect();
        cancelAnimationFrame(frame);
      });
    });
    inject(DestroyRef).onDestroy(() =>
      document.documentElement.style.removeProperty('--global-issue-height'),
    );
  }
}
