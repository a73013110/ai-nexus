import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { HtmlPreview } from './html-preview';

@Component({
  selector: 'nx-html-preview-host',
  imports: [HtmlPreview],
  template: `<nx-html-preview
    url="/api/v1/attachments/1/render"
    [gated]="gated()"
    [running]="running()"
    (run)="runs = runs + 1"
    (viewSource)="sources = sources + 1"
  />`,
})
class Host {
  readonly gated = signal(false);
  readonly running = signal(false);
  runs = 0;
  sources = 0;
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const frames = () => [...root.querySelectorAll('iframe')];
  return { fixture, host: fixture.componentInstance, root, frames };
}

describe('HtmlPreview', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('renders one script-free sandbox without throwing', () => {
    const { frames } = setup();
    expect(frames()).toHaveLength(1);
    const frame = frames()[0];
    expect(frame.getAttribute('sandbox')).toBe('');
    expect(frame.getAttribute('src')).toBe('/api/v1/attachments/1/render');
    expect(frame.getAttribute('referrerpolicy')).toBe('no-referrer');
  });

  it('recreates the iframe with only allow-scripts when scripts run', () => {
    const { fixture, host, frames } = setup();
    const before = frames()[0];
    host.running.set(true);
    fixture.detectChanges();
    expect(frames()).toHaveLength(1);
    const after = frames()[0];
    expect(after).not.toBe(before);
    expect(after.getAttribute('sandbox')).toBe('allow-scripts');
    expect(after.getAttribute('sandbox')).not.toContain('allow-same-origin');
    host.running.set(false);
    fixture.detectChanges();
    expect(frames()[0].getAttribute('sandbox')).toBe('');
  });

  it('offers view source and run while gated', () => {
    const { fixture, host, root } = setup();
    host.gated.set(true);
    fixture.detectChanges();
    const [source, run] = [...root.querySelectorAll<HTMLButtonElement>('.html-gate button')];
    expect(source.textContent?.trim()).toBe('檢視原始碼');
    source.click();
    run.click();
    expect(host.sources).toBe(1);
    expect(host.runs).toBe(1);
  });
});
