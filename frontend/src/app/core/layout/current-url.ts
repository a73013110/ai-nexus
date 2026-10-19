import { inject, type Signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';

/** 導覽完成後的網址（含轉址後的結果），給依目錄推得位置的元件用；須在注入脈絡中呼叫。 */
export function currentUrl(): Signal<string> {
  const router = inject(Router);
  return toSignal(
    router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: router.url },
  );
}
