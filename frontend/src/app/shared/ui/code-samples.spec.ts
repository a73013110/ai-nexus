import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { CodeSamples, fillPlaceholders, selectedCodeSample, type CodeSample } from './code-samples';

const samples: CodeSample[] = [
  {
    id: 'java',
    label: 'Java',
    note: '需要 Java 8 以上。',
    files: [
      { name: 'Client.java', language: 'java', source: 'String url = "{{baseUrl}}";\n' },
      { name: 'Usage.java', language: 'java', source: 'client.verify();\n' },
    ],
  },
  {
    id: 'csharp',
    label: 'C#',
    files: [{ name: 'Client.cs', language: 'cs', source: 'var key = "{{apiKey}}";' }],
  },
];

@Component({
  selector: 'nx-code-samples-host',
  imports: [CodeSamples],
  template: `<nx-code-samples
      label="範例語言"
      [samples]="samples"
      [values]="values()"
      preferenceKey="spec"
    />
    <nx-code-samples label="另一組範例" [samples]="samples" preferenceKey="spec" />`,
})
class Host {
  readonly samples = samples;
  readonly values = signal<Record<string, string>>({ baseUrl: 'https://ainexus.example/' });
}

describe('fillPlaceholders', () => {
  it('normalizes Windows line endings from checked-out source files', () => {
    expect(fillPlaceholders('a\r\nb\rc\n', {})).toBe('a\nb\nc\n');
  });

  it('fills known names and keeps the rest for the reader to fill', () => {
    expect(fillPlaceholders('{{baseUrl}} {{apiKey}} {{ spaced }}', { baseUrl: 'https://a/' })).toBe(
      'https://a/ {{apiKey}} {{ spaced }}',
    );
  });
});

describe('CodeSamples', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    localStorage.removeItem('ai-nexus.code-samples.spec');
  });

  it('shows one tab per language and every file of the chosen one, filled in', async () => {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const [first] = (fixture.nativeElement as HTMLElement).querySelectorAll('nx-code-samples');
    const tabs = [...first.querySelectorAll<HTMLButtonElement>('[role=tab]')];
    expect(tabs.map((tab) => tab.textContent?.trim())).toEqual(['Java', 'C#']);
    expect(first.querySelector('[role=tablist]')?.getAttribute('aria-label')).toBe('範例語言');
    const panel = first.querySelector('[role=tabpanel]')!;
    expect(panel.getAttribute('aria-labelledby')).toBe(tabs[0].id);
    expect(panel.querySelector('.code-samples-note')?.textContent).toBe('需要 Java 8 以上。');
    expect([...panel.querySelectorAll('.embed-label')].map((label) => label.textContent)).toEqual([
      'Client.java',
      'Usage.java',
    ]);
    expect(panel.querySelector('code')?.textContent).toBe(
      'String url = "https://ainexus.example/";',
    );
    expect(panel.querySelector('[aria-label="複製Client.java"]')).not.toBeNull();
  });

  it('switches every sample with the same key and remembers the choice', async () => {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const [first, second] = (fixture.nativeElement as HTMLElement).querySelectorAll(
      'nx-code-samples',
    );
    first.querySelectorAll<HTMLButtonElement>('[role=tab]')[1].click();
    await fixture.whenStable();
    expect(localStorage.getItem('ai-nexus.code-samples.spec')).toBe('csharp');
    expect(selectedCodeSample(samples, 'spec')?.id).toBe('csharp');
    for (const samples of [first, second])
      expect(samples.querySelector('[role=tab][aria-selected=true]')?.textContent?.trim()).toBe(
        'C#',
      );
    // 沒給的值保留佔位，讀者看得出要自己填。
    expect(first.querySelector('[role=tabpanel] code')?.textContent).toBe(
      'var key = "{{apiKey}}";',
    );
  });
});
