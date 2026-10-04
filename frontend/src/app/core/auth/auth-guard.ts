import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth-service';

/** Confirm identity before constructing any private workspace, even when the API is unavailable. */
export const authenticated: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  try {
    if ((await auth.load()).authenticated) return true;
  } catch {
    auth.session.set(null);
  }
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
