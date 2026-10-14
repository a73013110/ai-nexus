import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ThemeService } from '@core/preferences/theme-service';
import { ScrollArea } from './scroll-area';

@Component({
  selector: 'nx-scroll-area-host',
  imports: [ScrollArea],
  template: `<nx-scroll-area label="清單"><p>內容</p></nx-scroll-area>`,
})
class Host {}

const frame = () => new Promise((done) => requestAnimationFrame(done));

describe('ScrollArea', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });
  const setup = (sizes: { viewport: number; content: number; scroll: number }) => {
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe() {}
        disconnect() {}
      },
    );
    TestBed.configureTestingModule({
      providers: [{ provide: ThemeService, useValue: { scrollBehavior: () => 'instant' } }],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const area = fixture.debugElement.children[0].componentInstance as ScrollArea;
    const root = fixture.nativeElement as HTMLElement;
    const viewport = root.querySelector<HTMLElement>('.scroll-area-viewport')!;
    const content = root.querySelector<HTMLElement>('.scroll-area-content')!;
    Object.defineProperty(viewport, 'clientHeight', { value: sizes.viewport });
    Object.defineProperty(viewport, 'scrollHeight', { value: sizes.scroll });
    Object.defineProperty(content, 'offsetHeight', { value: sizes.content });
    return area;
  };

  it('offers the lower edge when the content is taller than the viewport', async () => {
    const area = setup({ viewport: 100, content: 300, scroll: 300 });
    area.scheduleMeasure();
    await frame();
    expect(area.below()).toBe(true);
    expect(area.above()).toBe(false);
  });

  it('ignores overflow that only a transformed child adds while it animates', async () => {
    const area = setup({ viewport: 100, content: 100, scroll: 107 });
    area.scheduleMeasure();
    await frame();
    expect(area.overflow()).toBe(false);
  });
});
