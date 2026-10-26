import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { ApprovalTimeline, type ApprovalStep } from './approval-timeline';

function render(steps: ApprovalStep[], summary = '') {
  const fixture = TestBed.createComponent(ApprovalTimeline);
  fixture.componentRef.setInput('steps', steps);
  fixture.componentRef.setInput('summary', summary);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('ApprovalTimeline', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('marks each step by state and reads the state aloud', () => {
    const root = render(
      [
        { key: '1', state: 'done', title: '送出', person: '林予安', time: '10/20 09:30' },
        {
          key: '2',
          state: 'current',
          title: '會簽',
          person: '張文彥',
          note: '代理：吳思妤',
          waiting: '已等候 2 天',
          time: '10/20 09:31',
          memo: '請補件',
        },
        { key: '3', state: 'pending', title: '決行' },
      ],
      '第 2 關，共 3 關',
    );
    const steps = [...root.querySelectorAll('.ui-approval-step')];
    expect(steps.map((step) => step.getAttribute('data-state'))).toEqual([
      'done',
      'current',
      'pending',
    ]);
    expect(steps.map((step) => step.querySelector('.sr-only')!.textContent)).toEqual([
      '（已完成）',
      '（目前關卡）',
      '（尚未輪到）',
    ]);
    expect(steps[0].querySelector('nx-icon')).not.toBeNull();
    expect(steps[0].querySelector('strong')!.textContent).toBe('林予安');
    expect(steps[0].querySelector('.is-title')!.textContent).toBe('送出');
    // 目前關卡在時間的位置寫已等候多久，送達時間留在提示。
    const waiting = steps[1].querySelector('.ui-approval-waiting')!;
    expect(waiting.textContent).toBe('已等候 2 天');
    expect(waiting.getAttribute('title')).toBe('10/20 09:31');
    expect(steps[1].querySelector('time')).toBeNull();
    expect(steps[1].textContent).toContain('代理：吳思妤');
    expect(steps[1].querySelector('.ui-approval-memo')!.textContent).toBe('請補件');
    // 沒有處理人時以關卡名稱當主文。
    expect(steps[2].querySelector('strong')!.textContent).toBe('決行');
    expect(steps[2].querySelector('time')).toBeNull();
    expect(root.querySelector('.ui-approval-progress')!.textContent).toBe('第 2 關，共 3 關');
    const [done, current] = [...root.querySelectorAll<HTMLElement>('.ui-approval-meter i')];
    expect(done.style.width).toMatch(/^33\.3/);
    expect(current.style.width).toMatch(/^33\.3/);
  });

  it('draws no progress without a summary', () => {
    const root = render([{ key: '1', state: 'done', title: '送出' }]);
    expect(root.querySelector('.ui-approval-meter')).toBeNull();
    expect(root.querySelector('.ui-approval-heading')!.textContent).toBe('簽核流程');
  });

  it('says so when there are no steps', () => {
    expect(render([]).querySelector('.ui-approval-empty')!.textContent).toBe('沒有簽核紀錄。');
  });
});
