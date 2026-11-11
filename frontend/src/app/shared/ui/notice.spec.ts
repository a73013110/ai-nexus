import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { Notice } from './notice';

@Component({
  selector: 'nx-notice-host',
  imports: [Notice],
  template: `<nx-notice
    message="讀取失敗"
    [retryable]="retryable()"
    [retrying]="refreshing()"
    (retry)="retries = retries + 1"
  />`,
})
class Host {
  readonly retryable = signal(true);
  readonly refreshing = signal(false);
  retries = 0;
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  return { fixture, host: fixture.componentInstance, button: () => root.querySelector('button') };
}

describe('Notice', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('emits retry from the built-in button', () => {
    const { host, button } = setup();
    expect(button()?.textContent?.trim()).toBe('重試');
    button()!.click();
    expect(host.retries).toBe(1);
  });

  it('shows the read in progress and ignores further clicks', () => {
    const { fixture, host, button } = setup();
    host.refreshing.set(true);
    fixture.detectChanges();
    expect(button()?.textContent?.trim()).toBe('重試中…');
    expect(button()?.getAttribute('aria-busy')).toBe('true');
    button()!.click();
    expect(host.retries).toBe(0);
  });

  it('has no retry button unless retryable', () => {
    const { fixture, host, button } = setup();
    host.retryable.set(false);
    fixture.detectChanges();
    expect(button()).toBeNull();
  });
});
