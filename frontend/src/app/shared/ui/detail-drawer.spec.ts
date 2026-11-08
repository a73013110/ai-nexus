import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ClientIssues } from '@core/errors/client-issues';
import { DetailDrawer, type DrawerSize } from './detail-drawer';
import { DrawerFoot } from './drawer-foot';
import { DrawerSummary } from './drawer-summary';
import { ProductTour } from './product-tour';

@Component({
  selector: 'nx-detail-drawer-host',
  imports: [DetailDrawer, DrawerFoot, DrawerSummary],
  template: `<nx-detail-drawer
    title="文件"
    preferenceKey="spec"
    [defaultSize]="defaultSize()"
    [tour]="tour()"
  >
    @if (showSummary()) {
      <nx-drawer-summary heading="報告.docx" />
    }
    內容
    <nx-drawer-foot note="說明"><button type="button">動作</button></nx-drawer-foot>
  </nx-detail-drawer>`,
})
class Host {
  readonly tour = signal<{ steps: [] } | null>({ steps: [] });
  readonly showSummary = signal(false);
  readonly defaultSize = signal<DrawerSize>('standard');
}

describe('DetailDrawer', () => {
  beforeEach(() => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    localStorage.removeItem('ai-nexus.drawer.spec');
    TestBed.configureTestingModule({
      providers: [
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
  });
  afterEach(() => vi.unstubAllGlobals());

  function render(setup?: (host: Host) => void) {
    const fixture = TestBed.createComponent(Host);
    setup?.(fixture.componentInstance);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    return { fixture, root, dialog: root.querySelector('dialog')! };
  }

  it('shows the compact tour trigger before the close button only when a tour is given', () => {
    const { fixture, root } = render();
    const actions = root.querySelector('.ui-drawer-actions')!;
    const trigger = actions.querySelector('nx-product-tour-button');
    expect(trigger?.querySelector('button')?.className).toContain('icon-button');
    expect(trigger?.nextElementSibling?.getAttribute('aria-label')).toBe('關閉文件');
    fixture.componentInstance.tour.set(null);
    fixture.detectChanges();
    expect(actions.querySelector('nx-product-tour-button')).toBeNull();
  });

  it('uses the default size until the user picks one, then remembers it', () => {
    const { dialog } = render((host) => host.defaultSize.set('wide'));
    expect(dialog.dataset['size']).toBe('wide');

    TestBed.resetTestingModule();
    localStorage.setItem('ai-nexus.drawer.spec', 'standard');
    TestBed.configureTestingModule({
      providers: [
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    expect(render((host) => host.defaultSize.set('wide')).dialog.dataset['size']).toBe('standard');
  });

  it('toggles the size and stores the choice', () => {
    const { fixture, dialog } = render();
    const resize = dialog.querySelector<HTMLButtonElement>('.drawer-resize')!;
    expect(resize.getAttribute('aria-pressed')).toBe('false');
    resize.click();
    fixture.detectChanges();
    expect(dialog.dataset['size']).toBe('wide');
    expect(resize.getAttribute('aria-pressed')).toBe('true');
    expect(localStorage.getItem('ai-nexus.drawer.spec')).toBe('wide');
  });

  it('demotes the title to a caption only while a summary is shown', () => {
    const { fixture, dialog } = render();
    const header = dialog.querySelector('.ui-drawer-header')!;
    expect(header.classList.contains('has-summary')).toBe(false);
    fixture.componentInstance.showSummary.set(true);
    fixture.detectChanges();
    expect(header.classList.contains('has-summary')).toBe(true);
    expect(header.querySelector('nx-drawer-summary h3')?.textContent).toBe('報告.docx');
    expect(dialog.getAttribute('aria-labelledby')).toBe(dialog.querySelector('h2')?.id);
  });

  it('keeps the foot outside the scrolling body', () => {
    const { dialog } = render();
    const body = dialog.querySelector('.ui-drawer-body')!;
    expect(body.querySelector('nx-drawer-foot')).toBeNull();
    expect(body.nextElementSibling?.tagName).toBe('NX-DRAWER-FOOT');
    expect(body.nextElementSibling?.querySelector('.dialog-note')?.textContent).toBe('說明');
  });
});
