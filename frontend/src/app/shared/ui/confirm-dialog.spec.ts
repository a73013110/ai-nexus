import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { ConfirmDialog } from './confirm-dialog';

function open() {
  const fixture = TestBed.createComponent(ConfirmDialog);
  fixture.detectChanges();
  const dialog = fixture.componentInstance.dialog().nativeElement;
  // jsdom 沒有實作 <dialog> 的開關；這裡只看按鈕與回答。
  dialog.showModal ??= () => dialog.setAttribute('open', '');
  dialog.close ??= () => dialog.removeAttribute('open');
  return { fixture, element: fixture.nativeElement as HTMLElement };
}

describe('ConfirmDialog', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('confirms only after the typed text matches', async () => {
    const { fixture, element } = open();
    const answer = fixture.componentInstance.ask({
      title: '重設日曆？',
      message: '會刪除日曆上的全部事件。',
      confirm: '重設',
      danger: true,
      typed: 'team-cal',
    });
    fixture.detectChanges();
    const confirm = element.querySelector<HTMLButtonElement>('.danger-button')!;
    const input = element.querySelector<HTMLInputElement>('input')!;
    expect(element.querySelector('.confirmation-typed')?.textContent).toBe('team-cal');
    expect(document.activeElement).toBe(input);
    expect(confirm.disabled).toBe(true);

    input.value = 'team';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    fixture.componentInstance.answer(true);
    expect(confirm.disabled).toBe(true);

    input.value = ' team-cal ';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(confirm.disabled).toBe(false);
    confirm.click();
    expect(await answer).toBe(true);
  });

  it('starts every question with an empty field and needs no text when none is asked', async () => {
    const { fixture, element } = open();
    void fixture.componentInstance.ask({ title: 'A', message: '', confirm: '確定', typed: 'x' });
    fixture.componentInstance.entered.set('x');
    const plain = fixture.componentInstance.ask({ title: 'B', message: '', confirm: '確定' });
    fixture.detectChanges();
    expect(fixture.componentInstance.entered()).toBe('');
    expect(element.querySelector('input')).toBeNull();
    element.querySelector<HTMLButtonElement>('.primary-button')!.click();
    expect(await plain).toBe(true);
  });
});
