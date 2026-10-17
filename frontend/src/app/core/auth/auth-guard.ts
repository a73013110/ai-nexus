import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth-service';

/** 即使 API 無法使用，也要在建構任何私有工作區前確認身分。 */
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
