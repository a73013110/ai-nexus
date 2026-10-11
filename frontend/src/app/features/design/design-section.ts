import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * One entry of the component workbench: a heading row (name, one-line purpose, source component)
 * and a bordered stage holding the live examples. Notes projected with `[design-notes]` sit under
 * the stage. The page's index links to the `id`.
 */
@Component({
  selector: 'nx-design-section',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'design-section', '[attr.aria-labelledby]': "id() + '-title'", role: 'region' },
  template: `<header>
      <h3 [id]="id() + '-title'" tabindex="-1">{{ title() }}</h3>
      @if (api()) {
        <code>{{ api() }}</code>
      }
      @if (description()) {
        <p>{{ description() }}</p>
      }
    </header>
    <div class="design-stage" [class.is-stacked]="stacked()"><ng-content /></div>
    <ng-content select="[design-notes]" />`,
  styles: `
    :host {
      display: block;
      scroll-margin-top: var(--p-space-4);
    }
    header {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      column-gap: var(--p-space-2);
      margin-bottom: var(--p-space-2);
    }
    h3 {
      margin: 0;
      font-size: var(--text-ui);
      font-weight: 600;
      outline-offset: 2px;
    }
    code {
      padding: 0 var(--p-space-1);
      border-radius: var(--p-radius-sm);
      background: var(--hover);
      color: var(--secondary);
      font: var(--p-font-xs) var(--font-code);
    }
    p {
      flex-basis: 100%;
      margin: 2px 0 0;
      color: var(--secondary);
      font-size: var(--text-caption);
    }
    .design-stage {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--p-space-3) var(--p-space-4);
      min-width: 0;
      padding: var(--p-space-4);
      border: 1px solid var(--line);
      border-radius: var(--p-radius-lg);
      background: var(--surface);
    }
    .design-stage.is-stacked {
      flex-direction: column;
      align-items: stretch;
    }
  `,
})
export class DesignSection {
  readonly id = input.required<string>();
  readonly title = input.required<string>();
  readonly description = input('');
  /** The production component or class the examples use. */
  readonly api = input('');
  /** Examples one per row instead of wrapping side by side. */
  readonly stacked = input(false);
}

/** A labelled example inside a stage: the variant name above the live component. */
@Component({
  selector: 'nx-design-example',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'design-example' },
  template: `<span class="design-example-label">{{ label() }}</span><ng-content />`,
  styles: `
    :host {
      display: inline-flex;
      flex-direction: column;
      align-items: flex-start;
      gap: var(--p-space-2);
      min-width: 0;
      max-width: 100%;
    }
    .design-example-label {
      color: var(--muted);
      font-size: var(--p-font-xs);
    }
  `,
})
export class DesignExample {
  readonly label = input.required<string>();
}
