import { DestroyRef, Injectable, inject, signal, type Signal } from '@angular/core';
import type { Command } from '../../shared/ui/command-palette';

/** What a page adds to Ctrl+K: its actions come before the features, its entries after. */
export interface CommandSource {
  actions: Signal<Command[]>;
  entries?: Signal<Command[]>;
  run(id: string): void;
}

/** The shell's command palette; pages contribute commands instead of owning a palette. */
@Injectable({ providedIn: 'root' })
export class WorkspaceCommands {
  readonly source = signal<CommandSource | null>(null);
  private opener: (() => void) | null = null;
  attach(opener: () => void) {
    this.opener = opener;
  }
  open() {
    this.opener?.();
  }
  /** Registers the calling page's commands for its lifetime. */
  provide(source: CommandSource) {
    this.source.set(source);
    inject(DestroyRef).onDestroy(() => {
      if (this.source() === source) this.source.set(null);
    });
  }
}
