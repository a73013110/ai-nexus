import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

@Component({
  selector: 'nx-text-highlight',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@for (part of parts(); track $index) {
    @if (part.match) {
      <mark>{{ part.text }}</mark>
    } @else {
      {{ part.text }}
    }
  }`,
})
export class TextHighlight {
  readonly text = input.required<string>();
  readonly query = input('');
  readonly parts = computed(() => {
    const text = this.text(),
      query = this.query().trim().toLowerCase();
    if (!query) return [{ text, match: false }];
    const lower = text.toLowerCase(),
      parts: { text: string; match: boolean }[] = [];
    let start = 0,
      next: number;
    while ((next = lower.indexOf(query, start)) >= 0 && parts.length < 2000) {
      if (next > start) parts.push({ text: text.slice(start, next), match: false });
      parts.push({ text: text.slice(next, next + query.length), match: true });
      start = next + query.length;
    }
    parts.push({ text: text.slice(start), match: false });
    return parts;
  });
}
