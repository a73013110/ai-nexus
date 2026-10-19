import { describe, expect, it } from 'vitest';
import { UrlSegment, type Route, type UrlSegmentGroup } from '@angular/router';
import { optionalId } from './optional-id';

const match = (...paths: string[]) =>
  optionalId('retrieval')(
    paths.map((path) => new UrlSegment(path, {})),
    {} as UrlSegmentGroup,
    {} as Route,
  );

describe('optional id route matcher', () => {
  it('matches the module path and its detail path with one route', () => {
    expect(match()?.posParams).toEqual({});
    expect(match('abc')?.posParams?.['id']?.path).toBe('abc');
  });
  it('rejects deeper paths and the fixed sibling paths it reserves', () => {
    expect(match('abc', 'extra')).toBeNull();
    expect(match('retrieval')).toBeNull();
  });
});
