import { Injectable } from '@angular/core';

/** 不透明的分頁識別碼，獨立於已驗證的身分與電腦主機名稱。 */
@Injectable({ providedIn: 'root' })
export class BrowserSession {
  readonly id = crypto.randomUUID();
}
