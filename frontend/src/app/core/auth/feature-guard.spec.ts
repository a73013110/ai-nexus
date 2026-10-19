import { TestBed } from '@angular/core/testing';
import type { Route, UrlSegment } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth-service';
import { featureGuard } from './feature-guard';
import { WorkspaceSession } from './workspace-session';

function setup(authenticated: boolean, granted: string[]) {
  const auth = {
    session: () => null,
    load: vi.fn(async () => ({ authenticated })),
  };
  const session = {
    load: vi.fn(async () => ({})),
    has: (id: string) => granted.includes(id),
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthService, useValue: auth },
      { provide: WorkspaceSession, useValue: session },
    ],
  });
  const match = (feature: string) =>
    TestBed.runInInjectionContext(() =>
      featureGuard({ data: { feature } } as Route, [] as UrlSegment[], undefined as never),
    );
  return { match, session };
}

describe('feature guard', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('matches a page only with its grant from /me', async () => {
    const { match } = setup(true, ['chat']);
    expect(await match('chat')).toBe(true);
    expect(await match('audit')).toBe(false);
  });

  it('matches a reader for any signed-in account without reading /me', async () => {
    const { match, session } = setup(true, []);
    expect(await match('knowledge.reader')).toBe(true);
    expect(session.load).not.toHaveBeenCalled();
  });

  it('never matches actions or ids outside the catalog', async () => {
    const { match } = setup(true, ['diagnostics.logs.export', 'admin']);
    expect(await match('diagnostics.logs.export')).toBe(false);
    expect(await match('admin')).toBe(false);
  });

  it('leaves a signed-out visitor to the shell guard that sends them to sign in', async () => {
    const { match, session } = setup(false, []);
    expect(await match('audit')).toBe(true);
    expect(session.load).not.toHaveBeenCalled();
  });
});
