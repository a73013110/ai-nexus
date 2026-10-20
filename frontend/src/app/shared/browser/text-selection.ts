import { Directive, ElementRef, HostListener, inject, output } from '@angular/core';

/** 段落工具單次可處理的字元數，與後端 `TransformText.MaxTextCharacters` 一致。 */
export const MAX_SELECTED_TEXT = 8000;

export interface SelectedText {
  text: string;
  sourceId: string;
  top: number;
  bottom: number;
  left: number;
}
@Directive({ selector: '[nxTextSelection]' })
export class TextSelection {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  readonly textSelected = output<SelectedText | null>();
  @HostListener('pointerup') @HostListener('keyup') inspect() {
    const selection = window.getSelection();
    if (!selection?.rangeCount || selection.isCollapsed) {
      this.textSelected.emit(null);
      return;
    }
    const range = selection.getRangeAt(0),
      start = range.startContainer.parentElement?.closest<HTMLElement>('[data-message-id]'),
      end = range.endContainer.parentElement?.closest<HTMLElement>('[data-message-id]');
    if (
      !start ||
      start !== end ||
      !this.host.nativeElement.contains(start) ||
      start.querySelector('.streaming-copy,.waiting-copy')
    ) {
      this.textSelected.emit(null);
      return;
    }
    const text = selection.toString().trim();
    if (!text || text.length > MAX_SELECTED_TEXT) {
      this.textSelected.emit(null);
      return;
    }
    const rect = range.getBoundingClientRect();
    this.textSelected.emit({
      text,
      sourceId: start.dataset['messageId']!,
      left: rect.left,
      top: rect.top,
      bottom: rect.bottom,
    });
  }
}
