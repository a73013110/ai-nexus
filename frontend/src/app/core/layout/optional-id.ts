import type { UrlMatcher, UrlMatchResult } from '@angular/router';

/**
 * Matches `/<path>` and `/<path>/:id` with one route, so the router reuses the page component
 * when an id appears or changes instead of destroying and recreating it.
 */
export function optionalId(path: string): UrlMatcher {
  return (segments): UrlMatchResult | null => {
    if (segments[0]?.path !== path || segments.length > 2) return null;
    return { consumed: segments, posParams: segments[1] ? { id: segments[1] } : {} };
  };
}
