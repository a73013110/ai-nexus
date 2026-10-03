import { Directive, input, output, signal } from '@angular/core';

@Directive({
  selector: '[nxFileDrop]',
  host: {
    '[class.is-dragging]': 'dragging()',
    '(dragenter)': 'enter($event)',
    '(dragover)': 'over($event)',
    '(dragleave)': 'leave($event)',
    '(drop)': 'drop($event)',
    '(paste)': 'paste($event)',
  },
})
export class FileDrop {
  readonly nxFileDrop = input(true);
  readonly filesDropped = output<File[]>();
  readonly dragging = signal(false);
  private depth = 0;
  enter(event: DragEvent) {
    if (event.dataTransfer?.types.includes('Files')) {
      event.preventDefault();
      if (this.nxFileDrop()) {
        this.depth++;
        this.dragging.set(true);
      }
    }
  }
  over(event: DragEvent) {
    if (event.dataTransfer?.types.includes('Files')) {
      event.preventDefault();
      event.dataTransfer.dropEffect = this.nxFileDrop() ? 'copy' : 'none';
    }
  }
  leave(event: DragEvent) {
    if (event.dataTransfer?.types.includes('Files') && --this.depth <= 0) {
      this.depth = 0;
      this.dragging.set(false);
    }
  }
  drop(event: DragEvent) {
    if (event.dataTransfer?.types.includes('Files')) {
      event.preventDefault();
      this.depth = 0;
      this.dragging.set(false);
      if (this.nxFileDrop() && event.dataTransfer.files.length)
        this.filesDropped.emit(Array.from(event.dataTransfer.files));
    }
  }
  paste(event: ClipboardEvent) {
    if (!this.nxFileDrop()) return;
    const images = Array.from(event.clipboardData?.files ?? []).filter((file) =>
      file.type.startsWith('image/'),
    );
    if (!images.length) return;
    event.preventDefault();
    this.filesDropped.emit(
      images.map(
        (file, index) =>
          new File(
            [file],
            `貼上圖片-${Date.now()}-${index}.${file.type === 'image/jpeg' ? 'jpg' : file.type.split('/')[1]}`,
            { type: file.type },
          ),
      ),
    );
  }
}
