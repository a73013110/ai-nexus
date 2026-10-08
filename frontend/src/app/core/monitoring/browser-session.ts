import { Injectable } from '@angular/core';

/** An opaque tab identifier, independent of the authenticated identity and computer hostname. */
@Injectable({ providedIn: 'root' })
export class BrowserSession {
  readonly id = crypto.randomUUID();
}
