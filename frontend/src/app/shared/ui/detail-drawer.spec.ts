import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ClientIssues } from '@core/errors/client-issues';
import { DetailDrawer } from './detail-drawer';
import { ProductTour } from './product-tour';

@Component({
  selector: 'nx-detail-drawer-host',
  imports: [DetailDrawer],
  template: `<nx-detail-drawer title="文件" [tour]="tour()">內容</nx-detail-drawer>`,
})
class Host {
  readonly tour = signal<{ steps: [] } | null>({ steps: [] });
}

describe('DetailDrawer', () => {
  beforeEach(() => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    TestBed.configureTestingModule({
      providers: [
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
  });
  afterEach(() => vi.unstubAllGlobals());

  it('shows the compact tour trigger before the close button only when a tour is given', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const actions = (fixture.nativeElement as HTMLElement).querySelector('.ui-drawer-actions')!;
    const trigger = actions.querySelector('nx-product-tour-button');
    expect(trigger?.querySelector('button')?.className).toContain('icon-button');
    expect(trigger?.nextElementSibling?.getAttribute('aria-label')).toBe('關閉文件');
    fixture.componentInstance.tour.set(null);
    fixture.detectChanges();
    expect(actions.querySelector('nx-product-tour-button')).toBeNull();
  });
});
