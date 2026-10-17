import { DestroyRef, Injectable, inject, signal, type Signal } from '@angular/core';
import type { Command } from '@shared/ui/command-palette';

/** 頁面為 Ctrl+K 加入的內容：其動作排在功能之前，其項目排在之後。 */
export interface CommandSource {
  actions: Signal<Command[]>;
  entries?: Signal<Command[]>;
  run(id: string): void;
}

/** 殼層的指令面板；頁面提供指令，而不是各自擁有一個面板。 */
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
  /** 在呼叫頁面的生命週期內登記其指令。 */
  provide(source: CommandSource) {
    this.source.set(source);
    inject(DestroyRef).onDestroy(() => {
      if (this.source() === source) this.source.set(null);
    });
  }
}
