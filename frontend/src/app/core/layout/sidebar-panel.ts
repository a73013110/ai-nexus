import { DestroyRef, Directive, Injectable, TemplateRef, inject, signal } from '@angular/core';

/** The page-specific content shown under the shared navigation, such as chat history. */
@Injectable({ providedIn: 'root' })
export class SidebarPanel {
  readonly template = signal<TemplateRef<unknown> | null>(null);
}

/**
 * Declares the current page's sidebar content. The template keeps its page context, so bindings
 * and outputs inside it talk to the page while the shell owns where it renders.
 */
@Directive({ selector: 'ng-template[nxSidebarPanel]' })
export class SidebarPanelContent {
  constructor() {
    const panel = inject(SidebarPanel);
    const template = inject(TemplateRef);
    panel.template.set(template);
    inject(DestroyRef).onDestroy(() => {
      if (panel.template() === template) panel.template.set(null);
    });
  }
}
