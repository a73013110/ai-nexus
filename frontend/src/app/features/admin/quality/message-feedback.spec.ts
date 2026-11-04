import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { MessageDto } from '@core/api/schema';
import { QualityApi } from './quality-api';
import { MessageFeedback } from './message-feedback';

const message = (feedbackRating: number) =>
  ({ id: 'm1', role: 'assistant', status: 'completed', feedbackRating }) as MessageDto;

function setup(rating: number) {
  // 渲染時共用元件會查媒體條件；jsdom 沒有 matchMedia。
  vi.stubGlobal('matchMedia', () => ({
    matches: false,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
  }));
  const api = {
    feedback: vi.fn(async () => ({})),
    removeFeedback: vi.fn(async () => undefined),
    feedbackFor: vi.fn(async () => undefined),
  };
  TestBed.configureTestingModule({ providers: [{ provide: QualityApi, useValue: api }] });
  const fixture = TestBed.createComponent(MessageFeedback);
  fixture.componentRef.setInput('message', message(rating));
  const rated = vi.fn();
  fixture.componentInstance.rated.subscribe(rated);
  return { api, component: fixture.componentInstance, rated };
}

describe('MessageFeedback', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('rates with PUT', async () => {
    const { api, component, rated } = setup(0);
    component.rate(1);
    await vi.waitFor(() => expect(rated).toHaveBeenCalledWith({ id: 'm1', rating: 1 }));
    expect(api.feedback).toHaveBeenCalledWith('m1', 1, '', '');
    expect(api.removeFeedback).not.toHaveBeenCalled();
    expect(component.rating()).toBe(1);
  });

  // 再按一次已選的評分是取消：走 DELETE（204），不是送 0 的 PUT。
  it('removes the rating when the selected button is pressed again', async () => {
    const { api, component, rated } = setup(-1);
    component.rate(-1);
    await vi.waitFor(() => expect(rated).toHaveBeenCalledWith({ id: 'm1', rating: 0 }));
    expect(api.removeFeedback).toHaveBeenCalledWith('m1');
    expect(api.feedback).not.toHaveBeenCalled();
    expect(component.rating()).toBe(0);
    expect(component.error()).toBe('');
  });
});
