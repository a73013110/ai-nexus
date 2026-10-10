import type { CanDeactivateFn } from '@angular/router';

export const pendingChanges: CanDeactivateFn<{ canLeave(): boolean | Promise<boolean> }> = (
  component,
) => component.canLeave();
