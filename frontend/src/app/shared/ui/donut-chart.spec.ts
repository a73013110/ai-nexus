import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { DonutChart } from './donut-chart';

const segments = [
  { key: 'cloud', label: '雲端助理', value: 62 },
  { key: 'local', label: '本機模型', value: 31 },
];

describe('DonutChart', () => {
  afterEach(() => TestBed.resetTestingModule());

  function setup() {
    const fixture = TestBed.createComponent(DonutChart);
    fixture.componentRef.setInput('label', '模型分布');
    fixture.componentRef.setInput('segments', []);
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();
    return { fixture, host: fixture.nativeElement as HTMLElement };
  }

  it('draws a ring skeleton instead of the empty copy while it loads', () => {
    const { fixture, host } = setup();
    const status = host.querySelector('[role="status"]')!;
    expect(status.textContent).toContain('正在載入模型分布…');
    expect(status.querySelector('.donut-skeleton')).not.toBeNull();
    expect(host.textContent).not.toContain('此期間尚無資料');

    fixture.componentRef.setInput('loading', false);
    fixture.detectChanges();
    expect(host.querySelector('.skeleton')).toBeNull();
    expect(host.textContent).toContain('此期間尚無資料');
  });

  it('keeps the parts and dims them while refreshing', () => {
    const { fixture, host } = setup();
    fixture.componentRef.setInput('loading', false);
    fixture.componentRef.setInput('segments', segments);
    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();
    const figure = host.querySelector('figure')!;
    expect(figure.classList).toContain('is-refreshing');
    expect(figure.getAttribute('aria-busy')).toBe('true');
    expect(host.querySelectorAll('.donut-part')).toHaveLength(2);
  });
});
