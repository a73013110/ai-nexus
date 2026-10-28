import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ThemeService } from '../preferences/theme-service';
import { IdentityVeil } from './identity-veil';

@Component({ template: '' })
class Page {}

describe('IdentityVeil', () => {
  const root = document.documentElement;
  afterEach(() => {
    TestBed.resetTestingModule();
    delete root.dataset['identityVeil'];
    root.style.removeProperty('--identity-veil-label');
    sessionStorage.clear();
    document.querySelector('.identity-veil')?.remove();
  });
  const setup = () => {
    const element = document.createElement('div');
    element.className = 'identity-veil';
    element.popover = 'manual';
    element.showPopover = vi.fn();
    document.body.append(element);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', component: Page }]),
        // 減少動態時不等動畫，測試只看狀態交接。
        { provide: ThemeService, useValue: { reducedMotion: () => true } },
      ],
    });
    return TestBed.inject(IdentityVeil);
  };

  it('covers the page and hands the label to the next load', async () => {
    const veil = setup();
    expect(veil.arrived).toBe(false);
    await veil.cover('正在切換為 王小明');
    expect(root.dataset['identityVeil']).toBe('cover');
    // 進 top layer，才蓋得住開著的對話框。
    expect(document.querySelector<HTMLElement>('.identity-veil')!.showPopover).toHaveBeenCalled();
    expect(root.style.getPropertyValue('--identity-veil-label')).toBe('"正在切換為 王小明"');
    expect(JSON.parse(sessionStorage.getItem('ai-nexus.identity-veil')!)).toMatchObject({
      label: '正在切換為 王小明',
    });
  });

  it('reveals the page boot.js covered after the first navigation renders', async () => {
    root.dataset['identityVeil'] = 'boot';
    root.style.setProperty('--identity-veil-label', '"正在返回 測試使用者"');
    const veil = setup();
    expect(veil.arrived).toBe(true);
    expect(veil.covering()).toBe(true);
    veil.revealAfterFirstNavigation();
    await TestBed.inject(Router).navigateByUrl('/work/dashboard');
    TestBed.tick();
    await new Promise((resolve) => setTimeout(resolve));
    expect(veil.covering()).toBe(false);
    expect(root.dataset['identityVeil']).toBeUndefined();
    expect(root.style.getPropertyValue('--identity-veil-label')).toBe('');
  });

  it('leaves an ordinary load alone', async () => {
    const veil = setup();
    veil.revealAfterFirstNavigation();
    await TestBed.inject(Router).navigateByUrl('/');
    TestBed.tick();
    expect(veil.covering()).toBe(false);
    expect(root.dataset['identityVeil']).toBeUndefined();
  });
});
