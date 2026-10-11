import { describe, expect, it } from 'vitest';
import { UrlSegment, type Route, type UrlSegmentGroup } from '@angular/router';
import { optionalId } from './optional-id';

const match = (...paths: string[]) =>
  optionalId('chat')(
    paths.map((path) => new UrlSegment(path, {})),
    {} as UrlSegmentGroup,
    {} as Route,
  );

describe('optional id route matcher', () => {
  it('matches the list and detail paths with one route', () => {
    expect(match('chat')?.posParams).toEqual({});
    expect(match('chat', 'abc')?.posParams?.['id']?.path).toBe('abc');
  });
  it('rejects other pages and deeper paths', () => {
    expect(match('files')).toBeNull();
    expect(match('chat', 'abc', 'extra')).toBeNull();
    expect(match()).toBeNull();
  });
});
