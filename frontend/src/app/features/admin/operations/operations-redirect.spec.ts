import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { operationsRedirect } from './operations-redirect';

@Component({ template: '' })
class Blank {}

async function land(url: string) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'admin/operations', component: Blank },
        { path: 'admin', canActivate: [operationsRedirect], component: Blank },
      ]),
    ],
  });
  const router = TestBed.inject(Router);
  await router.navigateByUrl(url);
  return router.url;
}

describe('operations redirect', () => {
  afterEach(() => TestBed.resetTestingModule());
  it('moves the old usage and retrieval tabs to platform operations', async () => {
    expect(await land('/admin?tab=usage')).toBe('/admin/operations?tab=usage');
    TestBed.resetTestingModule();
    expect(await land('/admin?tab=retrieval')).toBe('/admin/operations?tab=retrieval');
  });
  it('keeps access management tabs on the admin page', async () => {
    expect(await land('/admin')).toBe('/admin');
    TestBed.resetTestingModule();
    expect(await land('/admin?tab=roles')).toBe('/admin?tab=roles');
  });
});
