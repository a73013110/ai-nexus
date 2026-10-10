import {
  ApplicationRef,
  ComponentRef,
  createComponent,
  EnvironmentInjector,
  Injector,
} from '@angular/core';
import { MermaidDiagram } from './mermaid-diagram';

/** Lazy Angular widget host. Parsing/sanitization stays in the common Markdown boundary. */
export function mountMarkdownDiagrams(
  host: HTMLElement,
  diagrams: readonly string[],
  injector: Injector,
) {
  const app = injector.get(ApplicationRef);
  const environmentInjector = injector.get(EnvironmentInjector);
  const widgets: ComponentRef<MermaidDiagram>[] = [];
  const cleanup = () => {
    for (const widget of widgets.splice(0)) {
      app.detachView(widget.hostView);
      widget.destroy();
    }
  };
  try {
    for (const slot of host.querySelectorAll<HTMLElement>('.markdown-diagram-slot')) {
      const source = diagrams[Number(slot.dataset['diagramIndex'])];
      if (source === undefined) continue;
      const widget = createComponent(MermaidDiagram, {
        hostElement: slot,
        environmentInjector,
        elementInjector: injector,
      });
      widgets.push(widget);
      widget.setInput('source', source);
      app.attachView(widget.hostView);
      widget.changeDetectorRef.detectChanges();
    }
    return cleanup;
  } catch (error) {
    cleanup();
    throw error;
  }
}
