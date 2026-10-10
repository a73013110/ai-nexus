import { Field } from '../../shared/ui/field';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { RepositoryCommitDto } from '../../core/api/schema';
import { Select } from '../../shared/ui/select';

/** The same searchable commit selection and full-SHA escape hatch for either range endpoint. */
@Component({
  selector: 'nx-repository-commit-picker',
  imports: [Field, Select],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-2);
      min-width: 0;
    }
    details > summary {
      color: var(--secondary);
      cursor: pointer;
      padding-block: var(--p-space-2);
      font-size: var(--text-caption);
    }
    label {
      display: grid;
      gap: var(--p-space-2);
      font-size: var(--text-caption);
    }
    input {
      width: 100%;
      min-width: 0;
      font-family: var(--font-code);
    }
  `,
  template: `<span>{{ label() }}</span>
    <nx-select
      [label]="label()"
      [options]="choices()"
      [value]="value()"
      [disabled]="disabled()"
      [searchable]="true"
      [placeholder]="loading() ? '正在取得近期 commit…' : '選擇近期 commit'"
      [attr.aria-busy]="loading()"
      (valueChange)="valueChange.emit($event)"
    />
    <details>
      <summary>貼上完整 SHA</summary>
      <label
        >完整 SHA<input
          nxField
          [attr.aria-label]="label() + '（完整 SHA）'"
          [disabled]="disabled()"
          maxlength="64"
          placeholder="40／64 位 commit SHA"
          [value]="value()"
          (input)="valueChange.emit($any($event.target).value.trim().toLowerCase())"
      /></label>
    </details>`,
})
export class RepositoryCommitPicker {
  readonly label = input.required<string>();
  readonly value = input('');
  readonly excluded = input('');
  readonly commits = input.required<RepositoryCommitDto[]>();
  readonly disabled = input(false);
  readonly loading = input(false);
  readonly valueChange = output<string>();
  readonly choices = computed(() => {
    const rows = this.commits().map((x) => ({
      value: x.sha,
      label: x.sha.slice(0, 10) + ' · ' + x.message.split('\n')[0],
      description: x.sha,
      disabled: x.sha === this.excluded(),
    }));
    if (
      /^(?:[\da-f]{40}|[\da-f]{64})$/i.test(this.value()) &&
      !rows.some((x) => x.value === this.value())
    )
      rows.unshift({
        value: this.value(),
        label: '自訂版本 · ' + this.value().slice(0, 10),
        description: this.value(),
        disabled: this.value() === this.excluded(),
      });
    return rows;
  });
}
