import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';

/** Old bookmarks retain investigation filters without constructing the management workspace. */
export const redirectLegacyAdminAudit: CanActivateFn = (route) => {
  if (route.queryParamMap.get('tab') !== 'audit') return true;
  const { tab, ...queryParams } = route.queryParams;
  return inject(Router).createUrlTree(['/admin/audit'], {
    queryParams,
    fragment: route.fragment ?? undefined,
  });
};
