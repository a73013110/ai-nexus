import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import { TESTING_IDENTITY_NOTE, WorkspaceSession } from '@core/auth/workspace-session';
import type { PriceDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { ToastService } from '@shared/ui/toast-service';
import { BillingApi } from './billing-api';
import { PriceVersionDialog } from './price-version-dialog';

const version: PriceDto = {
  id: 'p1',
  provider: 'ollama',
  modelId: 'qwen3:8b',
  currency: 'TWD',
  kind: 'internal',
  inputPerMillion: 0,
  cachedInputPerMillion: 0,
  outputPerMillion: 0,
  perRequest: 0.3,
  requestCharge: 'attempted',
  effectiveAt: '2026-09-01T00:00:00Z',
  note: '內部成本',
  modelDisplayName: '本地助理',
};

function setup(testing = false) {
  const api = { addPrice: vi.fn(async () => version) };
  const toast = { success: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      { provide: BillingApi, useValue: api },
      { provide: ToastService, useValue: toast },
      {
        provide: WorkspaceSession,
        useValue: {
          testing: signal(testing),
          testingNote: signal(testing ? TESTING_IDENTITY_NOTE : ''),
        },
      },
    ],
  });
  // The draft's logic is under test; the form needs browser layout APIs jsdom lacks.
  TestBed.overrideComponent(PriceVersionDialog, {
    set: {
      template: '<dialog #dialog></dialog><nx-confirm-dialog />',
      imports: [ConfirmDialog],
      providers: [{ provide: ViewScope, useValue: { guard: () => () => true, message: String } }],
    },
  });
  const fixture = TestBed.createComponent(PriceVersionDialog);
  fixture.componentRef.setInput('prices', [version]);
  fixture.componentRef.setInput('targets', [
    { provider: 'google', modelId: 'gemma', displayName: '雲端助理' },
  ]);
  fixture.detectChanges();
  const native = fixture.nativeElement.querySelector('dialog') as HTMLDialogElement;
  const confirm = fixture.debugElement.children[1].componentInstance as ConfirmDialog;
  return { dialog: fixture.componentInstance, api, toast, native, confirm };
}

describe('PriceVersionDialog', () => {
  beforeAll(() => {
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
  });
  afterEach(() => TestBed.resetTestingModule());

  it('starts pristine from a version, so saving and reverting wait for a change', () => {
    const { dialog } = setup();
    dialog.open({ from: 'history', price: version });
    expect(dialog.draft()).toMatchObject({ modelId: 'qwen3:8b', perRequest: 0.3, effectiveAt: '' });
    expect(dialog.detail()).toContain('已帶入此版本');
    expect(dialog.dirty()).toBe(false);
    expect(dialog.canSave()).toBe(false);
    dialog.field('perRequest', 0.5);
    expect(dialog.dirty()).toBe(true);
    expect(dialog.canSave()).toBe(true);
    dialog.revert();
    expect(dialog.draft().perRequest).toBe(0.3);
    expect(dialog.dirty()).toBe(false);
  });

  it('picks the provider’s first model, zeroes a free price and refuses cached above input', () => {
    const { dialog } = setup();
    dialog.open();
    expect(dialog.draft().modelId).toBe('gemma');
    dialog.setProvider('ollama');
    expect(dialog.draft().modelId).toBe('qwen3:8b');
    dialog.field('inputPerMillion', 1);
    dialog.field('cachedInputPerMillion', 2);
    expect(dialog.rateError()).toBeTruthy();
    expect(dialog.canSave()).toBe(false);
    dialog.setKind('free');
    expect(dialog.draft()).toMatchObject({ kind: 'free', inputPerMillion: 0, perRequest: 0 });
    expect(dialog.rateError()).toBe('');
  });

  it('saves a new version effective now and closes', async () => {
    const { dialog, api, toast, native } = setup();
    let emitted = 0;
    dialog.saved.subscribe(() => emitted++);
    dialog.open({ from: 'current', price: version });
    dialog.field('note', ' 新合約 ');
    await dialog.save(new Event('submit'));
    expect(api.addPrice).toHaveBeenCalledWith(
      expect.objectContaining({ modelId: 'qwen3:8b', note: '新合約', perRequest: 0.3 }),
    );
    expect(toast.success).toHaveBeenCalled();
    expect(emitted).toBe(1);
    expect(native.open).toBe(false);
  });

  it('asks before closing with changes and never saves under a test identity', async () => {
    const { dialog, native, confirm } = setup(true);
    dialog.open({ from: 'history', price: version });
    dialog.field('perRequest', 1);
    expect(dialog.canSave()).toBe(false);
    expect(dialog.note()).toBe(TESTING_IDENTITY_NOTE);
    const closing = dialog.cancel(new Event('cancel'));
    expect(confirm.value()?.title).toBe('放棄未儲存的變更？');
    confirm.answer(false);
    await closing;
    expect(native.open).toBe(true);
    const again = dialog.cancel(new Event('cancel'));
    confirm.answer(true);
    await again;
    expect(native.open).toBe(false);
  });
});
