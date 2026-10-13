import type { UrlMatcher, UrlSegment } from '@angular/router';

/**
 * `/repositories`, `/repositories/:owner/:name` and its views `/:view` and `/:view/:item` as one
 * route, so moving between a repository's code, history and reviews keeps the page and its reads.
 */
export const repositoryRoute: UrlMatcher = (segments) => {
  if (segments[0]?.path !== 'repositories' || segments.length === 2 || segments.length > 5)
    return null;
  const [, owner, name, view, item] = segments;
  const posParams: Record<string, UrlSegment> = {};
  if (owner && name) Object.assign(posParams, { owner, name });
  if (view) posParams['view'] = view;
  if (item) posParams['item'] = item;
  return { consumed: segments, posParams };
};
