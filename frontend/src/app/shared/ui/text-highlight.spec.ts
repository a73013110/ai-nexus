import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { TextHighlight } from './text-highlight';

@Component({
  selector: 'nx-text-highlight-host',
  imports: [TextHighlight],
  template: `<span><nx-text-highlight [text]="text()" [query]="query()" /></span>`,
})
class Host {
  readonly text = signal('林同事');
  readonly query = signal('同事');
}

describe('TextHighlight', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('marks matches without adding whitespace around the unmatched parts', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toBe('林同事');
    expect([...element.querySelectorAll('mark')].map((mark) => mark.textContent)).toEqual(['同事']);

    fixture.componentInstance.query.set('');
    fixture.detectChanges();
    expect(element.textContent).toBe('林同事');
    expect(element.querySelector('mark')).toBeNull();
  });
});
