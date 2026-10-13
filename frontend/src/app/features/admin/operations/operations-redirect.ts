import { inject } from '@angular/core';
import { type CanActivateFn, RedirectCommand, Router } from '@angular/router';

/** Access management tabs that moved to platform operations; old bookmarks still land there. */
const movedTabs = new Set(['usage', 'retrieval']);

/** Sends `/admin?tab=usage|retrieval` to `/admin/operations`, replacing the old history entry. */
export const operationsRedirect: CanActivateFn = (route) => {
  const tab = route.queryParamMap.get('tab');
  if (!tab || !movedTabs.has(tab)) return true;
  const target = inject(Router).createUrlTree(['/admin/operations'], { queryParams: { tab } });
  return new RedirectCommand(target, { replaceUrl: true });
};
